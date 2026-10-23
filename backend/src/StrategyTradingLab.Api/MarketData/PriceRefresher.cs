using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Auth;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.MarketData;

public enum RefreshOutcome { Updated, FullHistory, Failed }

/// <summary>What the refresh did for one asset or currency pair. <c>Problem</c> says why it failed.</summary>
public record RefreshResult(string Name, RefreshOutcome Outcome, int Rows, string? Problem = null);

/// <summary>
/// Brings the stored daily prices and euro exchange rates up to date, the way the POC does: a daily run fetches the last
/// month; if that disagrees with what is stored (a split or a dividend re-adjusted the history) or nothing is stored,
/// the whole history is fetched and replaces it. One failing symbol never stops the others.
/// </summary>
public class PriceRefresher(AppDbContext db, IMarketDataSource source, ILogger<PriceRefresher> logger)
{
    public const string BaseCurrency = "EUR";
    /// <summary>A stored close further than this from the fresh one means the history was re-adjusted (0.1%, the POC's).</summary>
    public const decimal Tolerance = 0.001m;
    /// <summary>Politeness towards an unofficial endpoint.</summary>
    public TimeSpan Pause { get; set; } = TimeSpan.FromMilliseconds(300);

    public async Task<IReadOnlyList<RefreshResult>> RunAsync(CancellationToken cancellationToken)
    {
        var results = new List<RefreshResult>();
        var assets = await db.Assets.Where(a => a.Active).OrderBy(a => a.Symbol).ToListAsync(cancellationToken);
        foreach (var asset in assets)
        {
            results.Add(await GuardedAsync(asset.Symbol, () => RefreshAssetAsync(asset, cancellationToken), cancellationToken));
            await Task.Delay(Pause, cancellationToken);
        }
        var quotes = assets.Select(a => a.Currency).Concat(AccountRules.Currencies).Where(c => c != BaseCurrency).Distinct().Order();
        foreach (var quote in quotes)
        {
            results.Add(await GuardedAsync($"{BaseCurrency}{quote}", () => RefreshRateAsync(quote, cancellationToken), cancellationToken));
            await Task.Delay(Pause, cancellationToken);
        }
        return results;
    }

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

    public async Task<RefreshResult> RefreshAssetAsync(Asset asset, CancellationToken cancellationToken)
    {
        var recent = await source.DailyAsync(asset.Symbol, fullHistory: false, adjusted: true, cancellationToken);
        if (recent.Count == 0) throw new MarketDataException($"Yahoo returned no days for {asset.Symbol}.");

        var from = recent[0].Date;
        // Projected in the database, so the stored rows aren't tracked (they would clash with the rows added below).
        var stored = (await db.Prices.Where(p => p.AssetId == asset.Id && p.Date >= from)
            .Select(p => new { p.Date, p.Close }).ToListAsync(cancellationToken)).ToDictionary(p => p.Date, p => p.Close);

        var full = NeedsFullHistory(recent, stored);
        var days = recent;
        if (full)
        {
            days = await source.DailyAsync(asset.Symbol, fullHistory: true, adjusted: true, cancellationToken);
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

    public async Task<RefreshResult> RefreshRateAsync(string quote, CancellationToken cancellationToken)
    {
        var symbol = $"{BaseCurrency}{quote}=X";
        var latestStored = await db.FxRates.Where(r => r.Base == BaseCurrency && r.Quote == quote)
            .MaxAsync(r => (DateOnly?)r.Date, cancellationToken);
        var days = await source.DailyAsync(symbol, fullHistory: latestStored is null, adjusted: false, cancellationToken);
        // The last month doesn't reach back to the stored days (a long outage): fetch it all, so no weeks are left missing.
        if (latestStored is { } latest && days.Count > 0 && latest < days[0].Date)
            days = await source.DailyAsync(symbol, fullHistory: true, adjusted: false, cancellationToken);
        if (days.Count == 0) throw new MarketDataException($"Yahoo returned no rates for {symbol}.");

        var from = days[0].Date;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.FxRates.Where(r => r.Base == BaseCurrency && r.Quote == quote && r.Date >= from).ExecuteDeleteAsync(cancellationToken);
        db.FxRates.AddRange(days.Select(c => new FxRate { Date = c.Date, Base = BaseCurrency, Quote = quote, Rate = c.Close }));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        db.ChangeTracker.Clear();
        return new RefreshResult($"{BaseCurrency}{quote}", latestStored is null ? RefreshOutcome.FullHistory : RefreshOutcome.Updated, days.Count);
    }
}
