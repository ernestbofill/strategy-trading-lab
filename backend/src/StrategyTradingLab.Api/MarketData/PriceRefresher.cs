using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Auth;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Strategies;

namespace StrategyTradingLab.Api.MarketData;

public enum RefreshOutcome { Updated, FullHistory, Failed }

/// <summary>What the refresh did for one asset or currency pair. <c>Problem</c> says why it failed.</summary>
public record RefreshResult(string Name, RefreshOutcome Outcome, int Rows, string? Problem = null);

/// <summary>
/// Brings the stored daily prices and euro exchange rates up to date, the way the POC does: a daily run fetches the last
/// month; if that disagrees with what is stored (a split or a dividend re-adjusted the history) or nothing is stored,
/// the whole history (<see cref="HistoryStart"/>) is fetched and replaces it. One failing symbol never stops the others, and an asset keeps why its last fetch failed
/// (<see cref="Asset.PricesProblem"/>). Only the watched assets are fetched (<see cref="Watched"/>), with the exchange
/// rates of their currencies and the accounts' ones.
/// </summary>
public class PriceRefresher(AppDbContext db, IMarketDataSource source, ILogger<PriceRefresher> logger)
{
    public const string BaseCurrency = "EUR";
    /// <summary>A stored close further than this from the fresh one means the history was re-adjusted (0.1%, the POC's).</summary>
    public const decimal Tolerance = 0.001m;
    /// <summary>
    /// A history starts this many years before the earliest strategy that lists the asset (or before today). The longest
    /// rule window is 300 trading days, about 14 months; the rest is room to spare.
    /// </summary>
    public const int HistoryYears = 2;
    /// <summary>Politeness towards an unofficial endpoint.</summary>
    public TimeSpan Pause { get; set; } = TimeSpan.FromMilliseconds(300);
    public TimeProvider Time { get; set; } = TimeProvider.System;

    public async Task<IReadOnlyList<RefreshResult>> RunAsync(CancellationToken cancellationToken)
    {
        var results = new List<RefreshResult>();
        var assets = await Watched().OrderBy(a => a.Symbol).ToListAsync(cancellationToken);
        foreach (var asset in assets)
        {
            results.Add(await GuardedAsync(asset.Symbol, () => RefreshAssetAsync(asset, cancellationToken), cancellationToken));
            await Task.Delay(Pause, cancellationToken);
        }
        foreach (var quote in Quotes(assets.Select(a => a.Currency)))
        {
            results.Add(await GuardedAsync($"{BaseCurrency}{quote}", () => RefreshRateAsync(quote, cancellationToken), cancellationToken));
            await Task.Delay(Pause, cancellationToken);
        }
        return results;
    }

    /// <summary>The currencies whose euro rates are kept: those of the given assets and the accounts' ones.</summary>
    public static IEnumerable<string> Quotes(IEnumerable<string> assetCurrencies) =>
        assetCurrencies.Concat(AccountRules.Currencies).Where(c => c != BaseCurrency).Distinct().Order();

    /// <summary>
    /// The assets fetched every day: those on some running strategy's list, ticked or not (its trades still need their
    /// prices), and the index fund results are compared with. An asset nobody watches isn't fetched; once a strategy
    /// lists it, the next run catches up (after a gap of over a month it fetches the whole history again).
    /// </summary>
    public IQueryable<Asset> Watched()
    {
        var listed = Listings(db);
        return db.Assets.Where(a => a.Active && (a.Id == StrategyBook.IndexFundId || listed.Any(sa => sa.AssetId == a.Id)));
    }

    /// <summary>The running strategies' list entries, ticked or not: what makes an asset watched (one per strategy and asset).</summary>
    public static IQueryable<StrategyAsset> Listings(AppDbContext db) =>
        db.StrategyAssets.Where(sa => db.Strategies.Any(s => s.Id == sa.StrategyId && s.Status == StrategyStatus.Running));

    async Task<RefreshResult> GuardedAsync(string name, Func<Task<RefreshResult>> refresh, CancellationToken cancellationToken)
    {
        try
        {
            return await refresh();
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            db.ChangeTracker.Clear();
            logger.LogWarning("Prices for {Name} weren't updated: {Reason}", name, error.Message);
            return new RefreshResult(name, RefreshOutcome.Failed, 0, error.Message);
        }
    }

    /// <summary>Refreshes one asset's prices, and keeps on the asset why Yahoo's answer couldn't be used, or that it could.</summary>
    public Task<RefreshResult> RefreshAssetAsync(Asset asset, CancellationToken cancellationToken) =>
        RecordedAsync(asset, replaceFrom: null, cancellationToken);

    /// <summary>
    /// Replaces all of one asset's stored prices with the days from <paramref name="since"/> on (the admin's Download), or
    /// from <see cref="HistoryStart"/> when that is earlier, so a strategy's rules always have their window.
    /// </summary>
    public Task<RefreshResult> ReplaceAssetAsync(Asset asset, DateOnly since, CancellationToken cancellationToken) =>
        RecordedAsync(asset, since, cancellationToken);

    async Task<RefreshResult> RecordedAsync(Asset asset, DateOnly? replaceFrom, CancellationToken cancellationToken)
    {
        RefreshResult result;
        try
        {
            result = await LoadAssetAsync(asset, replaceFrom, cancellationToken);
        }
        catch (MarketDataException error)
        {
            db.ChangeTracker.Clear();
            await db.Assets.Where(a => a.Id == asset.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.PricesProblem, error.Message), cancellationToken);
            throw;
        }
        await db.Assets.Where(a => a.Id == asset.Id).ExecuteUpdateAsync(s => s
            .SetProperty(a => a.PricesProblem, (string?)null)
            .SetProperty(a => a.PricesCheckedAt, Time.GetUtcNow()), cancellationToken);
        return result;
    }

    async Task<RefreshResult> LoadAssetAsync(Asset asset, DateOnly? replaceFrom, CancellationToken cancellationToken)
    {
        // Asked to replace it, or nothing stored (a new asset): straight to the whole history, without asking for the last month first.
        var full = replaceFrom is not null || !await db.Prices.AnyAsync(p => p.AssetId == asset.Id, cancellationToken);
        DateOnly? keepFrom = replaceFrom;
        IReadOnlyList<Candle> days = [];
        var from = DateOnly.MinValue;
        if (!full)
        {
            days = await source.DailyAsync(asset.Symbol, since: null, adjusted: true, cancellationToken);
            if (days.Count == 0) throw new MarketDataException($"Yahoo returned no days for {asset.Symbol}.");
            from = days[0].Date;
            // Projected in the database, so the stored rows aren't tracked (they would clash with the rows added below).
            var stored = (await db.Prices.Where(p => p.AssetId == asset.Id && p.Date >= from)
                .Select(p => new { p.Date, p.Close }).ToListAsync(cancellationToken)).ToDictionary(p => p.Date, p => p.Close);
            full = NeedsFullHistory(days, stored);
            // A re-adjusted history (a split, a dividend) is fetched again over the span already stored, so a longer
            // download by hand isn't cut back.
            if (full) keepFrom = await db.Prices.Where(p => p.AssetId == asset.Id).MinAsync(p => (DateOnly?)p.Date, cancellationToken);
        }
        if (full)
        {
            var since = Earlier(keepFrom, await HistoryStartAsync(asset.Id, cancellationToken));
            days = await source.DailyAsync(asset.Symbol, since, adjusted: true, cancellationToken);
            if (days.Count == 0) throw new MarketDataException($"Yahoo returned no history for {asset.Symbol}.");
        }

        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            await db.Prices.Where(p => p.AssetId == asset.Id && (full || p.Date >= from)).ExecuteDeleteAsync(cancellationToken);
            Add(asset, days);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        db.ChangeTracker.Clear();
        return new RefreshResult(asset.Symbol, full ? RefreshOutcome.FullHistory : RefreshOutcome.Updated, days.Count);
    }

    /// <summary>
    /// The first day of an asset's whole history: <see cref="HistoryYears"/> before the earliest strategy that lists it or
    /// trade of it (any for the index fund, which all results are compared with), so its trades always have their prices.
    /// A trade the user records can be dated before the strategy started.
    /// </summary>
    Task<DateOnly> HistoryStartAsync(Guid assetId, CancellationToken cancellationToken) =>
        assetId == StrategyBook.IndexFundId
            ? HistoryStartAsync(db.Strategies, db.UserTrades, cancellationToken)
            : HistoryStartAsync(db.Strategies.Where(s => db.StrategyAssets.Any(sa => sa.StrategyId == s.Id && sa.AssetId == assetId)),
                db.UserTrades.Where(t => t.AssetId == assetId), cancellationToken);

    async Task<DateOnly> HistoryStartAsync(IQueryable<Strategy> strategies, IQueryable<UserTrade> trades, CancellationToken cancellationToken)
    {
        var started = await strategies.MinAsync(s => (DateOnly?)s.StartedOn, cancellationToken);
        var traded = await trades.MinAsync(t => (DateOnly?)t.TradeDate, cancellationToken);
        return HistoryStart(traded is { } day && (started is null || day < started) ? day : started, Today());
    }

    /// <summary><see cref="HistoryYears"/> before the earliest start or trade, or before today when there is none (or it is later).</summary>
    public static DateOnly HistoryStart(DateOnly? earliestStart, DateOnly today) =>
        (earliestStart is { } start && start < today ? start : today).AddYears(-HistoryYears);

    DateOnly Today() => DateOnly.FromDateTime(Time.GetUtcNow().UtcDateTime);

    /// <summary>The asked-for start when it is earlier than the one the strategies need, so a download never cuts their window.</summary>
    static DateOnly Earlier(DateOnly? asked, DateOnly needed) => asked is { } day && day < needed ? day : needed;

    /// <summary>
    /// True when the fresh days can't just be added to the stored ones: no day is both stored and fresh (the first run, or
    /// a gap of over a month), or one differs by more than the tolerance. The latest day is left out of the comparison,
    /// because a bar still in progress legitimately changes.
    /// </summary>
    public static bool NeedsFullHistory(IReadOnlyList<Candle> recent, IReadOnlyDictionary<DateOnly, decimal> stored)
    {
        var latest = recent[^1].Date;
        var shared = recent.Where(c => c.Date != latest && stored.ContainsKey(c.Date)).ToList();
        return shared.Count == 0 || shared.Any(c => Math.Abs(stored[c.Date] / c.Close - 1) > Tolerance);
    }

    void Add(Asset asset, IEnumerable<Candle> candles) =>
        db.Prices.AddRange(candles.Select(c => new PriceDaily
            { AssetId = asset.Id, Date = c.Date, Open = c.Open, High = c.High, Low = c.Low, Close = c.Close, Volume = c.Volume }));

    public Task<RefreshResult> RefreshRateAsync(string quote, CancellationToken cancellationToken) =>
        LoadRateAsync(quote, replaceFrom: null, cancellationToken);

    /// <summary>
    /// Replaces all of one currency's stored rates with the days from <paramref name="since"/> on (the admin's Download
    /// all), or from the strategies' start when that is earlier.
    /// </summary>
    public Task<RefreshResult> ReplaceRateAsync(string quote, DateOnly since, CancellationToken cancellationToken) =>
        LoadRateAsync(quote, since, cancellationToken);

    async Task<RefreshResult> LoadRateAsync(string quote, DateOnly? replaceFrom, CancellationToken cancellationToken)
    {
        var symbol = $"{BaseCurrency}{quote}=X";
        var latestStored = await db.FxRates.Where(r => r.Base == BaseCurrency && r.Quote == quote)
            .MaxAsync(r => (DateOnly?)r.Date, cancellationToken);
        // Asked to replace them, or nothing stored: the whole history, which replaces every stored rate.
        var full = replaceFrom is not null || latestStored is null;
        DateOnly? since = full ? Earlier(replaceFrom, await RatesStartAsync(cancellationToken)) : null;
        var days = await source.DailyAsync(symbol, since, adjusted: false, cancellationToken);
        // The last month doesn't reach back to the stored days (a long outage): fetch it all, over the span already stored,
        // so no weeks are left missing.
        if (!full && days.Count > 0 && latestStored < days[0].Date)
        {
            var oldest = await db.FxRates.Where(r => r.Base == BaseCurrency && r.Quote == quote).MinAsync(r => (DateOnly?)r.Date, cancellationToken);
            days = await source.DailyAsync(symbol, Earlier(oldest, await RatesStartAsync(cancellationToken)), adjusted: false, cancellationToken);
            full = true;
        }
        if (days.Count == 0) throw new MarketDataException($"Yahoo returned no rates for {symbol}.");

        var from = days[0].Date;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.FxRates.Where(r => r.Base == BaseCurrency && r.Quote == quote && (full || r.Date >= from)).ExecuteDeleteAsync(cancellationToken);
        db.FxRates.AddRange(days.Select(c => new FxRate { Date = c.Date, Base = BaseCurrency, Quote = quote, Rate = c.Close }));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        db.ChangeTracker.Clear();
        return new RefreshResult($"{BaseCurrency}{quote}", full ? RefreshOutcome.FullHistory : RefreshOutcome.Updated, days.Count);
    }

    /// <summary>Replaces the rates of every currency an asset or an account uses; one failing currency doesn't stop the others.</summary>
    public async Task<IReadOnlyList<RefreshResult>> ReplaceRatesAsync(DateOnly since, CancellationToken cancellationToken)
    {
        var currencies = await db.Assets.Select(a => a.Currency).Distinct().ToListAsync(cancellationToken);
        var results = new List<RefreshResult>();
        foreach (var quote in Quotes(currencies))
        {
            results.Add(await GuardedAsync($"{BaseCurrency}{quote}", () => ReplaceRateAsync(quote, since, cancellationToken), cancellationToken));
            await Task.Delay(Pause, cancellationToken);
        }
        return results;
    }

    /// <summary>Deletes every stored price and exchange rate: the next fetch of each starts from scratch.</summary>
    public async Task<(int Prices, int Rates)> FlushAllAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var prices = await db.Prices.ExecuteDeleteAsync(cancellationToken);
        var rates = await db.FxRates.ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (prices, rates);
    }

    /// <summary>The first day of the rates' whole history: <see cref="HistoryYears"/> before the earliest strategy or trade, which any currency may need.</summary>
    Task<DateOnly> RatesStartAsync(CancellationToken cancellationToken) => HistoryStartAsync(db.Strategies, db.UserTrades, cancellationToken);
}
