using System.Text.Json;
using StrategyTradingLab.Api.Data.Entities;
using static StrategyTradingLab.Api.Templates.TemplateAssets;

namespace StrategyTradingLab.Api.Templates;

public enum InvestsIn { StockList, Sp500Fund, IndexFund }

public enum SignalSides { BuysOnly, BuysAndSells }

/// <summary>The tested fee per trade: a % of the amount plus a fixed amount in the strategy's currency, not converted: €1, $1 or £1 (rounded on purpose).</summary>
public record TestedFee(decimal Percent, decimal Fixed)
{
    /// <summary>
    /// Splits a step into what buys shares and the fee, so that the two add up to the step exactly: a step is what one
    /// round spends, fee included (decisions.md, 2026-10-24). €100 is €98.80 of shares and a €1.20 fee.
    /// </summary>
    public (decimal Amount, decimal Fee) Split(decimal step)
    {
        // A step must leave something to buy with (the budget screen's minimum step is far above the fee).
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(step, Fixed + 0.01m);
        var amount = Math.Round((step - Fixed) / (1 + Percent / 100), 2, MidpointRounding.ToZero);
        return (amount, step - amount);
    }
}

/// <summary>
/// One backtest period of <c>Years</c> years: % return of the template and the index fund over the whole period, and each one's
/// worst calendar year. The per-year figures are the yearly rate that compounds to the period's return.
/// </summary>
public record PeriodResult(string Period, int Years, decimal Template, decimal Index, decimal WorstYear, decimal IndexWorstYear)
{
    public decimal TemplatePerYear => PerYear(Template);
    public decimal IndexPerYear => PerYear(Index);

    decimal PerYear(decimal total) => Math.Round((decimal)(Math.Pow((double)(1 + total / 100), 1.0 / Years) - 1) * 100, 1);
}

/// <summary>What a template watches: a starter list of stocks, or one fund, by asset id (the owner can change them on /admin/templates).</summary>
public abstract record TemplateWatches
{
    public abstract IReadOnlyList<Guid> AssetIds { get; }
}

public sealed record StarterList(IReadOnlyList<Guid> StockIds) : TemplateWatches
{
    public StarterList(params Seed[] stocks) : this([.. stocks.Select(stock => stock.Id)]) { }

    public override IReadOnlyList<Guid> AssetIds => StockIds;
}

public sealed record DefaultFund(Guid FundId) : TemplateWatches
{
    public DefaultFund(Seed fund) : this(fund.Id) { }

    public override IReadOnlyList<Guid> AssetIds => [FundId];
}

/// <summary>
/// A template: the rules the engine runs and its tested defaults, which a new strategy copies.
/// <c>Params</c> is the template's params record (TemplateParams.cs). Its texts are in Texts/{locale}.json.
/// </summary>
public sealed record Template(
    string Key,
    string? EngineKey,
    InvestsIn InvestsIn,
    SignalSides Signals,
    Budget Budget,
    TemplateWatches Watches,
    IReadOnlyList<PeriodResult>? Backtest,
    object Params)
{
    /// <summary>Bumped when the tested defaults are retuned; strategies record the one they started from.</summary>
    public int DefaultsVersion { get; init; } = 1;

    /// <summary>The rules the user can tune on Advanced settings, in screen order. Empty until the template's are designed.</summary>
    public IReadOnlyList<Setting> Settings { get; init; } = [];

    /// <summary>
    /// "Getting close": a candidate within this many percent of its buy rule (the dashboard card, the summary's stock changes).
    /// The AI template's stocks have no buy rule, so it never applies there.
    /// </summary>
    public decimal ClosePct { get; init; } = DefaultClosePct;

    public const decimal DefaultClosePct = 3;

    /// <summary>The params as strategies.params stores them.</summary>
    public JsonDocument ParamsJson() => JsonSerializer.SerializeToDocument(Params, Params.GetType(), JsonSerializerOptions.Web);
}

/// <summary>
/// The five templates, in the order the pick screen shows them, with their tested defaults. The owner can change a template's
/// defaults, starter list or fund and "getting close" on /admin/templates: <see cref="TemplateStore"/> gives the ones in use.
/// </summary>
public static class TemplateCatalog
{
    /// <summary>The POC backtest's costs: 0.2% of the amount (spread, currency conversion) plus 1 per order.</summary>
    public static readonly TestedFee Fee = new(0.2m, 1m);

    /// <summary>Where the backtests' daily prices come from.</summary>
    public const string PriceProvider = "Yahoo Finance";

    // The POC scales in with 5 steps per stock ($110 steps, $550 per stock).
    static readonly Budget StockBudget = new(3000, 100, 5);

    // Backtests of the strategy across time periods and a broad universe of assets (from the POC research).
    // Each period is an independent test starting with fresh money at its beginning.

    // The S&P 500 fund (SPY) in each period: its return, and its worst calendar year (2008, 2018, 2022; total return).
    static readonly (string Period, int Years, decimal Index, decimal WorstYear)[] Spy =
        [("2000-09", 10, -8, -37), ("2010-19", 10, 246, -5), ("2020-24", 5, 96, -18), ("All", 25, 538, -37)];

    /// <summary>A template's backtest: its return and worst year in each period, next to the S&P 500 fund's.</summary>
    static IReadOnlyList<PeriodResult> Tested(params (decimal Template, decimal WorstYear)[] periods)
    {
        // One result per S&P 500 period; Zip would silently drop the rest.
        ArgumentOutOfRangeException.ThrowIfNotEqual(periods.Length, Spy.Length);
        return [.. Spy.Zip(periods, (spy, p) => new PeriodResult(spy.Period, spy.Years, p.Template, spy.Index, p.WorstYear, spy.WorstYear))];
    }

    public static readonly Template Dip = new(
        "dip", "stage2_pullback", InvestsIn.StockList, SignalSides.BuysOnly, StockBudget,
        new StarterList(Coherent, Eaton, Modine, MonolithicPower, NVent, Schneider, SuperMicro, Vertiv, Vicor),
        Tested((48, -18), (185, -12), (72, -21), (490, -21)),
        new DipParams())
    {
        // The spec's (2b): the POC's tunable rules, plus the two averages the POC fixes at 200 and 50 days. Grouped by what they
        // decide: which stocks are candidates, when a candidate signals, and how often it may buy.
        Settings =
        [
            new("minGainPct", 5, 20, 200, Group: "candidate"),
            new("gainDays", 21, 63, 252, Group: "candidate"),
            // The floor (at most 80) and long (at least 160) averages stay apart, so the middle one is always the POC's 150 days.
            new("trendDays", 10, 160, 300, Group: "candidate"),
            new("peakDays", 5, 20, 60, Group: "signal"),
            new("zoneStartPct", 1, 1, 39, Below: "zoneEndPct", Group: "signal"),
            new("zoneEndPct", 1, 2, 40, Group: "signal"),
            new("floorDays", 5, 20, 80, Group: "signal"),
            new("cooldownDays", 5, 0, 60, Group: "pace"),
        ],
    };

    public static readonly Template Comeback = new(
        "comeback", "turnaround", InvestsIn.StockList, SignalSides.BuysOnly, StockBudget,
        new StarterList(Byd, Coherent, Eaton, Modine, Moderna, MoltenVentures, MonolithicPower, Palantir, Salesforce,
            ServiceNow, Snowflake, Tesla, Vertiv, Vicor, ZetaGlobal, Zscaler),
        Tested((62, -15), (210, -14), (88, -24), (540, -24)),
        new ComebackParams())
    {
        // The POC's tunable rules, except "below its SMA 200", a yes/no the screen has no control for.
        Settings =
        [
            new("drawdownPct", 5, 10, 60, Group: "candidate"),
            new("candidateDays", 10, 0, 120, Group: "candidate"),
            new("aboveSma50Pct", 1, 5, 30, Group: "signal"),
            new("volumeRatio", 0.1m, 1, 3, Group: "signal"),
            new("maxChasePct", 1, 2, 20, Group: "signal"),
            new("nearHighPct", 1, 5, 30, Group: "milestones"),
            new("breakoutPullbackPct", 1, 3, 20, Group: "milestones"),
            new("cooldownDays", 1, 0, 30, Group: "pace"),
        ],
    };

    public static readonly Template Bear = new(
        "bear", "bear_accumulation", InvestsIn.Sp500Fund, SignalSides.BuysOnly, new Budget(3000, 600, 5),
        new DefaultFund(Cspx),
        Tested((36, -12), (18, -4), (58, -18), (165, -18)),
        new BearParams())
    {
        // The POC's tunable rules, except the second legs (rearm), a yes/no the screen has no control for.
        Settings =
        [
            new("drawdownPct", 5, 10, 40, Group: "candidate"),
            new("highDays", 21, 63, 252, Group: "candidate"),
            new("reboundPct", 1, 1, 15, Group: "signal"),
            new("stepEveryDays", 5, 5, 30, Group: "pace"),
        ],
    };

    // All in, all out: no step, the whole budget at once.
    public static readonly Template Trend = new(
        "trend", "index_trend", InvestsIn.IndexFund, SignalSides.BuysAndSells, new Budget(3000, null, null),
        new DefaultFund(Cspx),
        Tested((77, -2), (127, -11), (61, -21), (572, -21)),
        new TrendParams())
    {
        Settings =
        [
            new("smaDays", 10, 50, 300, Group: "signal"),
            new("bandPct", 1, 0, 10, Group: "signal"),
            new("cooldownDays", 1, 0, 30, Group: "pace"),
        ],
    };

    // No backtest: results start on the day the strategy starts. Watches the stocks of Winners at a discount and The comeback.
    public static readonly Template Ai = new(
        "ai", null, InvestsIn.StockList, SignalSides.BuysOnly, StockBudget,
        new StarterList([.. Dip.Watches.AssetIds.Union(Comeback.Watches.AssetIds)]),
        null,
        new AiParams())
    {
        // A round can be run until the next is due, at most a week (AiRoundRules.LastDay).
        Settings =
        [
            new("everyDays", 1, 1, 60),
            new("topN", 1, 1, 3),
        ],
    };

    public static readonly IReadOnlyList<Template> All = [Dip, Comeback, Bear, Trend, Ai];

    /// <summary>The keys the database's template_key check allows.</summary>
    public static readonly string[] Keys = [.. All.Select(template => template.Key)];

    public static Template? Find(string key) => All.FirstOrDefault(template => template.Key == key);
}
