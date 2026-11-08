using System.Text.Json;
using System.Text.Json.Serialization;
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
/// One backtest period: yearly-average % return of the template and the index fund (on money actually invested vs same-day
/// index baseline for step strategies, or full account vs buy-and-hold for Index trend), each one's worst calendar year,
/// direct alpha, and an optional story. Null figures mean the strategy never triggered in that period (e.g. Bear 2013-19).
/// </summary>
public record PeriodResult(
    string Period,
    decimal? TemplatePerYear = null,
    decimal? IndexPerYear = null,
    decimal? WorstYear = null,
    decimal? IndexWorstYear = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Story = null)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? Alpha => TemplatePerYear - IndexPerYear;
}

/// <summary>Backtest results for one benchmark index tab in a multi-index template (Index trend).</summary>
public record BenchmarkBacktest(string Key, IReadOnlyList<PeriodResult> Periods);

/// <summary>What a template watches: a starter list of stocks, or one fund.</summary>
public abstract record TemplateWatches
{
    public abstract IEnumerable<Seed> Assets { get; }
}

public sealed record StarterList(IReadOnlyList<Seed> Stocks) : TemplateWatches
{
    public override IEnumerable<Seed> Assets => Stocks;
}

public sealed record DefaultFund(Seed Fund) : TemplateWatches
{
    public override IEnumerable<Seed> Assets => [Fund];
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

    /// <summary>Optional multi-index backtest tabs (used by Index trend).</summary>
    public IReadOnlyList<BenchmarkBacktest>? Benchmarks { get; init; }

    /// <summary>The params as strategies.params stores them.</summary>
    public JsonDocument ParamsJson() => JsonSerializer.SerializeToDocument(Params, Params.GetType(), JsonSerializerOptions.Web);
}

/// <summary>The five templates, in the order the pick screen shows them.</summary>
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

    public static readonly Template Dip = new(
        "dip", "stage2_pullback", InvestsIn.StockList, SignalSides.BuysOnly, StockBudget,
        new StarterList([Coherent, Eaton, Modine, MonolithicPower, NVent, Schneider, SuperMicro, Vertiv, Vicor]),
        [
            new("2000-12", 11.5m, 5.5m, -18m, -37m),
            new("2013-19", 14.9m, 13.4m, -12m, -4m),
            new("2020-26", 16.3m, 14.3m, -21m, -18m),
            new("All", 14.3m, 9.8m, -21m, -37m),
        ],
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
        new StarterList([Byd, Coherent, Eaton, Modine, Moderna, MoltenVentures, MonolithicPower, Palantir, Salesforce,
            ServiceNow, Snowflake, Tesla, Vertiv, Vicor, ZetaGlobal, Zscaler]),
        [
            new("2000-12", 15.1m, 5.5m, -15m, -37m),
            new("2013-19", 16.3m, 13.4m, -14m, -4m),
            new("2020-26", 19.2m, 14.3m, -24m, -18m),
            new("All", 16.5m, 9.8m, -24m, -37m),
        ],
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
        [
            new("2000-12", 9.4m, 1.7m, -12m, -37m),
            new("2013-19"),
            new("2020-26", 16.8m, 13.3m, -14m, -18m),
            new("All", 11.2m, 7.7m, -14m, -37m),
        ],
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

    static readonly IReadOnlyList<PeriodResult> TrendSp500Periods =
    [
        new("2000-12", 6.4m, 1.7m, -4m, -37m),
        new("2013-19", 10.4m, 13.8m, -11m, -4m),
        new("2020-26", 11.2m, 13.3m, -16m, -18m),
        new("All", 10.2m, 10.9m, -16m, -37m),
    ];

    // All in, all out: no step, the whole budget at once.
    public static readonly Template Trend = new(
        "trend", "index_trend", InvestsIn.IndexFund, SignalSides.BuysAndSells, new Budget(3000, null, null),
        new DefaultFund(Cspx),
        TrendSp500Periods,
        new TrendParams())
    {
        Settings =
        [
            new("smaDays", 10, 50, 300, Group: "signal"),
            new("bandPct", 1, 0, 10, Group: "signal"),
            new("cooldownDays", 1, 0, 30, Group: "pace"),
        ],
        Benchmarks =
        [
            new("sp500", TrendSp500Periods),
            new("qqq",
            [
                new("2000-12", 4.8m, -1.2m, -15m, -42m),
                new("2013-19", 15.4m, 18.9m, -9m, -1m),
                new("2020-26", 16.2m, 18.5m, -19m, -33m),
                new("All", 9.4m, 8.7m, -19m, -42m),
            ]),
            new("efa",
            [
                new("2002-12", 7.8m, 5.6m, -8m, -41m),
                new("2013-19", 4.9m, 6.8m, -10m, -14m),
                new("2020-26", 8.6m, 9.1m, -11m, -14m),
                new("All", 7.2m, 7.0m, -11m, -41m),
            ]),
            new("dax",
            [
                new("2000-12", 6.1m, 0.8m, -12m, -44m),
                new("2013-19", 6.8m, 8.5m, -11m, -18m),
                new("2020-26", 10.4m, 11.2m, -9m, -12m),
                new("All", 8.9m, 7.5m, -12m, -44m),
            ]),
            new("nikkei",
            [
                new("2000-12", 1.8m, -4.5m, -11m, -42m),
                new("2013-19", 10.9m, 12.8m, -9m, -12m),
                new("2020-26", 11.8m, 13.1m, -8m, -9m),
                new("All", 6.5m, 5.8m, -11m, -42m),
            ]),
            new("vfinx",
            [
                new("2000-12", 6.3m, 1.7m, -4m, -37m),
                new("2013-19", 10.3m, 13.7m, -11m, -4m),
                new("2020-26", 11.1m, 13.3m, -16m, -18m),
                new("All", 10.5m, 11.1m, -16m, -37m),
            ]),
        ],
    };

    // No backtest: results start on the day the strategy starts. Watches the stocks of Winners at a discount and The comeback.
    public static readonly Template Ai = new(
        "ai", null, InvestsIn.StockList, SignalSides.BuysOnly, StockBudget,
        new StarterList([.. Dip.Watches.Assets.Union(Comeback.Watches.Assets)]),
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
