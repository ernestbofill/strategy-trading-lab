using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Evaluation;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Strategies;

/// <summary>
/// The buys and prices behind some strategies' results, loaded once for all of them. A simulation's results come from
/// its signal trades; a live strategy's from the trades the user confirmed, with its signal trades as "the rules alone".
/// </summary>
public sealed class StrategyBook
{
    /// <summary>The broad index fund the results are compared with (spec: Versus the index).</summary>
    public static readonly Guid IndexFundId = TemplateAssets.Spy.Id;

    readonly ILookup<Guid, Buy> signalBuys, userBuys;

    public PriceBook Prices { get; }

    StrategyBook(ILookup<Guid, Buy> signalBuys, ILookup<Guid, Buy> userBuys, PriceBook prices) =>
        (this.signalBuys, this.userBuys, Prices) = (signalBuys, userBuys, prices);

    public static async Task<StrategyBook> LoadAsync(AppDbContext db, IReadOnlyCollection<Strategy> strategies, DateOnly today,
        CancellationToken cancellationToken)
    {
        var ids = strategies.Select(s => s.Id).ToList();
        var signalTrades = await db.SignalTrades.AsNoTracking()
            .Where(t => ids.Contains(t.StrategyId) && t.Side == TradeSide.Buy)
            .Select(t => new { t.StrategyId, Buy = new Buy(t.AssetId, t.TradeDate, t.Shares, t.Amount, t.Fee) })
            .ToListAsync(cancellationToken);
        var userTrades = await db.UserTrades.AsNoTracking()
            .Where(t => ids.Contains(t.StrategyId) && t.Side == TradeSide.Buy && t.DeletedAt == null)
            .Select(t => new { t.StrategyId, Buy = new Buy(t.AssetId, t.TradeDate, t.Shares, t.Amount, t.Fee) })
            .ToListAsync(cancellationToken);

        var buys = signalTrades.Select(t => t.Buy).Concat(userTrades.Select(t => t.Buy)).ToList();
        var prices = buys.Count == 0
            ? new PriceBook(new Dictionary<Guid, (string, IReadOnlyList<DayPrice>)>(), new ExchangeRates([]))
            : await LoadPricesAsync(db, [.. buys.Select(b => b.AssetId).Append(IndexFundId).Distinct()], buys.Min(b => b.Date), today, cancellationToken);
        return new StrategyBook(signalTrades.ToLookup(t => t.StrategyId, t => t.Buy), userTrades.ToLookup(t => t.StrategyId, t => t.Buy), prices);
    }

    static async Task<PriceBook> LoadPricesAsync(AppDbContext db, List<Guid> assetIds, DateOnly from, DateOnly today, CancellationToken cancellationToken)
    {
        var currencies = await db.Assets.AsNoTracking().Where(a => assetIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.Currency, cancellationToken);
        var since = from.AddDays(-ExchangeRates.LookBackDays);
        var rows = await db.Prices.AsNoTracking()
            .Where(p => assetIds.Contains(p.AssetId) && p.Date >= since && p.Date <= today)
            .OrderBy(p => p.Date)
            .Select(p => new { p.AssetId, Day = new DayPrice(p.Date, p.Open, p.Close) })
            .ToListAsync(cancellationToken);
        var days = rows.ToLookup(r => r.AssetId, r => r.Day);
        var assets = currencies.ToDictionary(a => a.Key, a => (a.Value, (IReadOnlyList<DayPrice>)[.. days[a.Key]]));
        return new PriceBook(assets, await ExchangeRates.LoadAsync(db, from, cancellationToken));
    }

    /// <summary>The buys the strategy's own results come from.</summary>
    public IReadOnlyList<Buy> You(Strategy strategy) =>
        [.. (strategy.Mode == StrategyMode.Live ? userBuys : signalBuys)[strategy.Id].OrderBy(b => b.Date)];

    /// <summary>A live strategy's signal trades, "the rules alone"; null for a simulation, whose results are the rules'.</summary>
    public IReadOnlyList<Buy>? Rules(Strategy strategy) =>
        strategy.Mode == StrategyMode.Live ? [.. signalBuys[strategy.Id].OrderBy(b => b.Date)] : null;

    /// <summary>
    /// The budget used (spec: Metrics): a simulation's signal-trade buys, a live strategy's confirmed buys with their fees.
    /// </summary>
    public decimal Used(Strategy strategy) => Spent(strategy, You(strategy));

    /// <summary>What <paramref name="buys"/> use of the budget, on the same basis as <see cref="Used"/>.</summary>
    public static decimal Spent(Strategy strategy, IEnumerable<Buy> buys) =>
        strategy.Mode == StrategyMode.Live ? buys.Sum(b => b.Cost) : buys.Sum(b => b.Amount);

    /// <summary>The strategy's results today: null until it has a buy that can be valued.</summary>
    public StrategyFigures? Figures(Strategy strategy, DateOnly today)
    {
        var you = You(strategy);
        if (Performance.GainPct(you, Prices, today, strategy.Currency) is not { } gain) return null;
        var rules = Rules(strategy);
        var index = Performance.IndexBuys(you, Prices, IndexFundId, strategy.Currency);
        return new StrategyFigures(
            gain,
            rules is null ? null : Performance.GainPct(rules, Prices, today, strategy.Currency),
            index is null ? null : Performance.GainPct(index, Prices, today, strategy.Currency),
            Performance.History(you, rules, index, Prices, today, strategy.Currency));
    }
}

/// <summary>A strategy's gain since it started, in percent: the user's own, the rules' (live only) and the index fund's for the same money.</summary>
public record StrategyFigures(decimal You, decimal? Rules, decimal? Index, IReadOnlyList<ResultPoint> History);
