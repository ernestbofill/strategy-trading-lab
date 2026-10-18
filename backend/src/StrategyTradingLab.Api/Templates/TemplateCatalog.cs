using System.Text.Json;
using StrategyTradingLab.Api.Data.Entities;
using static StrategyTradingLab.Api.Templates.TemplateAssets;

namespace StrategyTradingLab.Api.Templates;

public enum InvestsIn { StockList, Sp500Fund, IndexFund }

public enum SignalSides { BuysOnly, BuysAndSells }

/// <summary>The tested fee per trade: a % of the amount plus a fixed amount in the strategy's currency.</summary>
public record TestedFee(decimal Percent, decimal Fixed);

/// <summary>One backtest period: % return of the template, the index fund, and the worst calendar year.</summary>
public record PeriodResult(string Period, decimal Template, decimal Index, decimal WorstYear);

/// <summary>
/// A template: the rules the engine runs and its tested defaults, which a new strategy copies.
/// Its copy (name, rules…) is in Copy/{key}.{locale}.json.
/// </summary>
public abstract record Template(
    string Key,
    string? EngineKey,
    InvestsIn InvestsIn,
    SignalSides Signals,
    Budget Budget,
    IReadOnlyList<Seed> Assets,
    IReadOnlyList<PeriodResult>? Backtest)
{
    /// <summary>Bumped when the tested defaults are retuned; strategies record the one they started from.</summary>
    public int DefaultsVersion { get; init; } = 1;

    public TestedFee Fee { get; init; } = TemplateCatalog.PocFee;

    public bool WatchesFund => InvestsIn != InvestsIn.StockList;

    public abstract object DefaultParams { get; }

    public JsonDocument DefaultParamsJson() =>
        JsonSerializer.SerializeToDocument(DefaultParams, DefaultParams.GetType(), JsonSerializerOptions.Web);
}

public sealed record Template<TParams>(
    string Key,
    string? EngineKey,
    InvestsIn InvestsIn,
    SignalSides Signals,
    Budget Budget,
    IReadOnlyList<Seed> Assets,
    IReadOnlyList<PeriodResult>? Backtest,
    TParams Params)
    : Template(Key, EngineKey, InvestsIn, Signals, Budget, Assets, Backtest)
    where TParams : notnull
{
    public override object DefaultParams => Params;
}

/// <summary>The five templates, in the order the pick screen shows them.</summary>
public static class TemplateCatalog
{
    /// <summary>The POC backtest's costs: 0.2% of the amount (spread, currency conversion) plus 1 per order.</summary>
    public static readonly TestedFee PocFee = new(0.2m, 1m);

    // The POC scales in with 5 steps per stock ($110 steps, $550 per stock).
    static readonly Budget StockBudget = new(3000, 100, 5);

    // MOCK figures, the same as the frontend's mock, until the backtest engine computes them.
    static PeriodResult[] Mock(params (string, decimal, decimal, decimal)[] periods) =>
        [.. periods.Select(p => new PeriodResult(p.Item1, p.Item2, p.Item3, p.Item4))];

    public static readonly Template<DipParams> Dip = new(
        "dip", "stage2_pullback", InvestsIn.StockList, SignalSides.BuysOnly, StockBudget,
        [Eaton, Modine, MonolithicPower, NVent, SuperMicro, Vertiv, Vicor],
        Mock(("2010-14", 38, 61, -9), ("2015-19", 71, 58, -12), ("2020-24", 64, 79, -21), ("All", 212, 248, -21)),
        new DipParams());

    public static readonly Template<ComebackParams> Comeback = new(
        "comeback", "turnaround", InvestsIn.StockList, SignalSides.BuysOnly, StockBudget,
        [Moderna, Palantir, Salesforce, ServiceNow, Snowflake, Tesla, ZetaGlobal, Zscaler],
        Mock(("2010-14", 22, 61, -14), ("2015-19", 31, 58, -17), ("2020-24", 47, 79, -26), ("All", 118, 248, -26)),
        new ComebackParams());

    public static readonly Template<BearParams> Bear = new(
        "bear", "bear_accumulation", InvestsIn.Sp500Fund, SignalSides.BuysOnly, new Budget(3000, 600, 5),
        [Spy],
        Mock(("2000-09", 41, -9, -18), ("2010-19", 12, 189, -4), ("2020-24", 58, 79, -12), ("All", 136, 283, -18)),
        new BearParams());

    // All in, all out: no step, the whole budget at once.
    public static readonly Template<TrendParams> Trend = new(
        "trend", "index_trend", InvestsIn.IndexFund, SignalSides.BuysAndSells, new Budget(3000, null, null),
        [Spy],
        Mock(("2000-09", 24, -4, -8), ("2010-19", 121, 186, -11), ("2020-24", 52, 81, -9), ("All", 249, 272, -11)),
        new TrendParams());

    // No backtest: results start on the day the strategy starts. Watches every stock of the POC's lists.
    public static readonly Template<AiParams> Ai = new(
        "ai", null, InvestsIn.StockList, SignalSides.BuysOnly, StockBudget,
        [.. Dip.Assets, .. Comeback.Assets],
        null,
        new AiParams());

    public static readonly IReadOnlyList<Template> All = [Dip, Comeback, Bear, Trend, Ai];

    public static Template? Find(string key) => All.FirstOrDefault(template => template.Key == key);
}
