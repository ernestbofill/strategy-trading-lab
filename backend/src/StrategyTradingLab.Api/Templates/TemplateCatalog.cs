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

    // The backtest periods are MOCK figures, the same as the frontend's mock, until the backtest engine computes them.

    public static readonly Template Dip = new(
        "dip", "stage2_pullback", InvestsIn.StockList, SignalSides.BuysOnly, StockBudget,
        new StarterList([Coherent, Eaton, Modine, MonolithicPower, NVent, Schneider, SuperMicro, Vertiv, Vicor]),
        [new("2010-14", 38, 61, -9), new("2015-19", 71, 58, -12), new("2020-24", 64, 79, -21), new("All", 212, 248, -21)],
        new DipParams());

    public static readonly Template Comeback = new(
        "comeback", "turnaround", InvestsIn.StockList, SignalSides.BuysOnly, StockBudget,
        new StarterList([Byd, Coherent, Eaton, Modine, Moderna, MoltenVentures, MonolithicPower, Palantir, Salesforce,
            ServiceNow, Snowflake, Tesla, Vertiv, Vicor, ZetaGlobal, Zscaler]),
        [new("2010-14", 22, 61, -14), new("2015-19", 31, 58, -17), new("2020-24", 47, 79, -26), new("All", 118, 248, -26)],
        new ComebackParams());

    public static readonly Template Bear = new(
        "bear", "bear_accumulation", InvestsIn.Sp500Fund, SignalSides.BuysOnly, new Budget(3000, 600, 5),
        new DefaultFund(Cspx),
        [new("2000-09", 41, -9, -18), new("2010-19", 12, 189, -4), new("2020-24", 58, 79, -12), new("All", 136, 283, -18)],
        new BearParams());

    // All in, all out: no step, the whole budget at once.
    public static readonly Template Trend = new(
        "trend", "index_trend", InvestsIn.IndexFund, SignalSides.BuysAndSells, new Budget(3000, null, null),
        new DefaultFund(Cspx),
        [new("2000-09", 24, -4, -8), new("2010-19", 121, 186, -11), new("2020-24", 52, 81, -9), new("All", 249, 272, -11)],
        new TrendParams());

    // No backtest: results start on the day the strategy starts. Watches the stocks of Dip in winners and The comeback.
    public static readonly Template Ai = new(
        "ai", null, InvestsIn.StockList, SignalSides.BuysOnly, StockBudget,
        new StarterList([.. Dip.Watches.Assets.Union(Comeback.Watches.Assets)]),
        null,
        new AiParams());

    public static readonly IReadOnlyList<Template> All = [Dip, Comeback, Bear, Trend, Ai];

    /// <summary>The keys the database's template_key check allows.</summary>
    public static readonly string[] Keys = [.. All.Select(template => template.Key)];

    public static Template? Find(string key) => All.FirstOrDefault(template => template.Key == key);
}
