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
        if (!CanBuyWith(step)) throw new ArgumentOutOfRangeException(nameof(step), step, "A step must be more than the fixed fee.");
        var amount = Math.Round((step - Fixed) / (1 + Percent / 100), 2, MidpointRounding.ToZero);
        return (amount, step - amount);
    }

    /// <summary>Whether <paramref name="spend"/> leaves something to buy with once the fixed fee is paid.</summary>
    public bool CanBuyWith(decimal spend) => spend > Fixed + 0.01m;

    /// <summary>The fee on a sale of <paramref name="amount"/>, to the cent.</summary>
    public decimal On(decimal amount) => Math.Round(amount * (Percent / 100) + Fixed, 2);
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
    /// <summary>The template version (/admin/templates, <see cref="TemplateStore"/>); 1 is the catalogue's. Strategies record the one they started from.</summary>
    public int DefaultsVersion { get; init; } = 1;

    /// <summary>The rules the user can tune on Advanced settings, in screen order. Empty until the template's are designed.</summary>
    public IReadOnlyList<Setting> Settings { get; init; } = [];

    /// <summary>
    /// "Getting close": a candidate within this many percent of its buy rule (the dashboard card, the summary's stock changes).
    /// The AI template's stocks have no buy rule, so it never applies there.
    /// </summary>
    public decimal ClosePct { get; init; } = DefaultClosePct;

    public const decimal DefaultClosePct = 3;

    /// <summary>Signals are rare (years apart): the strategy page says so next to the past results, so a quiet strategy doesn't look broken.</summary>
    public bool RareSignals { get; init; }

    /// <summary>Optional multi-index backtest tabs (used by Index trend).</summary>
    public IReadOnlyList<BenchmarkBacktest>? Benchmarks { get; init; }

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

    // STL's own backtest (docs/backtest.md, ROADMAP item 56): the test draft for the stock templates, the fund for Bear markets and
    // Index trend, at the defaults in use on 2026-11-25, snapshot 2026-11. Each period is an independent test with fresh money; the
    // index column is the S&P 500, monthly (stock templates) or the fund bought with the whole budget on day one (fund templates).

    public static readonly Template Dip = new(
        TemplateKeys.Dip, "stage2_pullback", InvestsIn.StockList, SignalSides.BuysOnly, StockBudget,
        new StarterList(Coherent, Eaton, Modine, MonolithicPower, NVent, Schneider, SuperMicro, Vertiv, Vicor),
        [
            new("2011–14", 18.9m, 21.4m, -4.7m, 12.9m),
            new("2015–18", 9.6m, 7.3m, -4.3m, -0.4m),
            new("2019–22", 9.2m, 11.6m, -12.7m, -13.1m),
            new("2023–26", 11.2m, 17.4m, -3.9m, 4.3m),
            new("All", 12.2m, 14.4m, -12.7m, -13.1m),
        ],
        // STL's own low rule from a new strategy's first day (the params' default keeps the POC's).
        new DipParams { LowBeforePeakFrom = DateOnly.MinValue })
    {
        // The spec's (2b): the POC's tunable rules, plus the two averages the POC fixes at 200 and 50 days. Grouped by what they
        // decide: which stocks are candidates, when a candidate signals, and how often it may buy.
        Settings =
        [
            new("minGainPct", 5, 20, 200, Group: "candidate"),
            new("gainDays", 21, 63, 252, Group: "candidate"),
            // The floor (at most 80) and long (at least 160) averages stay apart, so the floor is always the shorter one.
            new("trendDays", 10, 160, 300, Group: "candidate"),
            new("peakDays", 5, 20, 60, Group: "signal"),
            new("zoneStartPct", 1, 1, 39, Below: "zoneEndPct", Group: "signal"),
            new("zoneEndPct", 1, 2, 40, Group: "signal"),
            new("floorDays", 5, 20, 80, Group: "signal"),
            new("cooldownDays", 5, 0, 60, Group: "pace"),
        ],
    };

    public static readonly Template Comeback = new(
        TemplateKeys.Comeback, "turnaround", InvestsIn.StockList, SignalSides.BuysOnly, StockBudget,
        new StarterList(Byd, Coherent, Eaton, Modine, Moderna, MoltenVentures, MonolithicPower, Palantir, Salesforce,
            ServiceNow, Snowflake, Tesla, Vertiv, Vicor, ZetaGlobal, Zscaler),
        [
            new("2011–14", 23.3m, 21.4m, 13.4m, 12.9m),
            new("2015–18", 3.5m, 7.3m, -30.6m, -0.4m),
            new("2019–22", 13.4m, 11.6m, 1.6m, -13.1m),
            new("2023–26", 10.4m, 17.4m, -8.6m, 4.3m),
            new("All", 12.7m, 14.4m, -30.6m, -13.1m),
        ],
        new ComebackParams())
    {
        // The POC's tunable rules, except "below its SMA 200", a yes/no the screen has no control for, and the milestones, which
        // STL replaced with a pace (Ernest, 2026-11-21).
        Settings =
        [
            new("drawdownPct", 1, 10, 60, Group: "candidate"),
            new("candidateDays", 10, 0, 120, Group: "candidate"),
            new("aboveSma50Pct", 1, 5, 30, Group: "signal"),
            new("lowDays", 5, 10, 120, Group: "signal"),
            new("volumeRatio", 0.1m, 1, 3, Group: "signal"),
            new("maxChasePct", 1, 2, 20, Group: "signal"),
            new("stepEveryDays", 5, 5, 30, Group: "steps"),
            new("cooldownDays", 1, 0, 30, Group: "pace"),
        ],
    };

    public static readonly Template Bear = new(
        TemplateKeys.Bear, "bear_accumulation", InvestsIn.Sp500Fund, SignalSides.BuysOnly, new Budget(3000, 600, 5),
        new DefaultFund(Cspx),
        [
            new("2011–14", null, 17.9m, null, 4.1m),
            new("2015–18", null, 8.5m, null, -0.4m),
            new("2019–22", 16.5m, 15.6m, -13.1m, -13.1m),
            new("2023–26", null, 18.2m, null, 4.3m),
            new("All", 16.5m, 15.6m, -13.1m, -13.1m),
        ],
        new BearParams())
    {
        RareSignals = true,
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
        new("2011–14", 16.6m, 17.9m, -5.0m, 4.1m),
        new("2015–18", 5.8m, 8.5m, -5.9m, -0.4m),
        new("2019–22", 9.2m, 15.6m, -18.6m, -13.1m),
        new("2023–26", 15.9m, 18.2m, 0.4m, 4.3m),
        new("All", 11.9m, 15.0m, -18.6m, -13.1m),
    ];

    // All in, all out: no step, the whole budget at once.
    public static readonly Template Trend = new(
        TemplateKeys.Trend, "index_trend", InvestsIn.IndexFund, SignalSides.BuysAndSells, new Budget(3000, null, null),
        new DefaultFund(Cspx),
        TrendSp500Periods,
        new TrendParams())
    {
        Settings =
        [
            new("smaDays", 10, 50, 300, Group: "signal"),
            new("bandPct", 1, 0, 10, Below: "maxChasePct", Group: "signal"),
            new("maxChasePct", 1, 4, 30, Group: "signal"),
            new("cooldownDays", 1, 0, 30, Group: "pace"),
        ],
        Benchmarks =
        [
            new("sp500", TrendSp500Periods),
            new("qqq",
            [
                new("2011–14", 18.8m, 21.3m, -5.2m, 5.5m),
                new("2015–18", 8.3m, 13.0m, -3.3m, 4.3m),
                new("2019–22", 21.6m, 18.1m, -13.4m, -28.4m),
                new("2023–26", 19.7m, 24.4m, 1.0m, 7.0m),
                new("All", 17.1m, 19.2m, -13.4m, -28.4m),
            ]),
            new("efa",
            [
                new("2011–14", 2.9m, 6.6m, -8.4m, -10.3m),
                new("2015–18", 0.4m, 3.3m, -8.8m, -10.0m),
                new("2019–22", 5.6m, 8.0m, -10.2m, -9.1m),
                new("2023–26", 6.6m, 13.2m, -3.3m, 10.1m),
                new("All", 3.9m, 7.8m, -10.2m, -10.3m),
            ]),
            new("dax",
            [
                new("2011–14", 6.1m, 8.9m, -5.1m, -15.6m),
                new("2015–18", 6.1m, 1.7m, -11.3m, -18.3m),
                new("2019–22", 3.4m, 7.3m, -7.4m, -12.3m),
                new("2023–26", 12.7m, 15.4m, -3.8m, 0.9m),
                new("All", 7.1m, 8.3m, -11.3m, -18.3m),
            ]),
            new("nikkei",
            [
                new("2011–14", -5.4m, 5.8m, -19.0m, -11.6m),
                new("2015–18", 4.9m, 7.0m, -7.0m, -6.5m),
                new("2019–22", -8.6m, 3.5m, -12.7m, -16.5m),
                new("2023–26", 10.3m, 16.4m, -7.9m, 13.1m),
                new("All", 0.3m, 8.2m, -19.0m, -16.5m),
            ]),
            new("vfinx",
            [
                new("2011–14", 16.5m, 17.7m, -4.9m, 3.7m),
                new("2015–18", 6.6m, 8.5m, -3.1m, -0.4m),
                new("2019–22", 11.5m, 15.0m, -14.0m, -13.2m),
                new("2023–26", 15.1m, 18.4m, 1.1m, 4.3m),
                new("All", 12.4m, 14.9m, -14.0m, -13.2m),
            ]),
        ],
    };

    // No backtest: results start on the day the strategy starts. Watches the stocks of Winners at a discount and The comeback.
    public static readonly Template Ai = new(
        TemplateKeys.Ai, null, InvestsIn.StockList, SignalSides.BuysOnly, StockBudget,
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
