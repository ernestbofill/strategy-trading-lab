using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Signals;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Strategies;

// The strategy DTOs of frontend/src/api/types.ts (camelCase JSON, enums as camelCase strings).

/// <summary>
/// What the Confirm screen sends. <c>Currency</c> is the one the user saw while setting the budget; without it the user's
/// current one is used. <c>Settings</c> are the Advanced settings the user changed (setting key → value); the rest keep the
/// template's tested defaults.
/// </summary>
public record NewStrategy(string TemplateKey, string Name, StrategyMode? Mode, Budget Budget, IReadOnlyList<Guid> AssetIds,
    string? Currency = null, IReadOnlyDictionary<string, decimal>? Settings = null);

/// <summary>A strategy as shown on the dashboard.</summary>
public record StrategySummary(
    Guid Id,
    string Name,
    string TemplateKey,
    string TemplateName,
    StrategyMode Mode,
    DateOnly StartedOn,
    string Currency,
    decimal Budget,
    decimal Used,
    StrategyResults? Results,
    PastResult? PastResult);

/// <summary>What a strategy has earned since it started, in percent, next to the index fund (null when its prices are missing). Null until it has trades.</summary>
public record StrategyResults(decimal Strategy, decimal? Index, IReadOnlyList<decimal> History);

/// <summary>How the template did in the past, shown while a strategy has no results of its own.</summary>
public record PastResult(string Years, decimal Template, decimal Index, decimal WorstYear);

public enum WaitingKind { BuySignal, SellSignal, AiRound }

/// <summary>Something that needs the user.</summary>
public record WaitingItem(Guid Id, WaitingKind Kind, Guid StrategyId, string StrategyName, string? StockName);

/// <summary><c>Today</c> is the user's local date, to tell a strategy started today from an older one.</summary>
public record Dashboard(DateOnly Today, IReadOnlyList<StrategySummary> Strategies, IReadOnlyList<WaitingItem> Waiting);

/// <summary>The budget and what the strategy's buys use of it. <c>Steps</c> and <c>TotalSteps</c> are null for all in, all out.</summary>
public record BudgetUsed(decimal Amount, decimal Used, decimal? Step, int? MaxSteps, int? Steps, int? TotalSteps);

/// <summary>How a live strategy's signals were answered.</summary>
public record SignalCounts(int Did, int Skipped, int Missed);

/// <summary>A stock's pill on the strategy page.</summary>
public enum StockStatus
{
    WaitingForYou,
    Signal,
    Holding,
    Done,
    Building,
    OutOfTheMarket,
    NoBudget,
    Watching,
    NotCandidate,
    NotEnoughData,
    NotChecked,
}

/// <summary>
/// A stock of a strategy. <c>Steps</c> are the buys the results count (simulation: signal trades; live: the user's
/// confirmed buys), <c>RulesSteps</c> the rules' own signals (live only). <c>Signal</c> is the waiting signal (live) or
/// today's (simulation). <c>Invested</c> is the cost of its buys, fees included, minus the cost of shares sold, as budget
/// used counts it; <c>Result</c> is the gain in percent of it, null with no buys.
/// </summary>
public record StockRow(Guid AssetId, string Symbol, string Name, StockStatus Status, int Steps, int? RulesSteps, int? MaxSteps,
    decimal? DistancePct, Guid? SignalId, int? SignalStep, decimal Invested, decimal? Result, TradeSide? SignalSide = null);

/// <summary>A trade in the strategy's trades list.</summary>
public record StrategyTradeItem(
    Guid Id,
    Guid AssetId,
    string Symbol,
    string StockName,
    DateOnly TradeDate,
    TradeSide Side,
    decimal Price,
    string AssetCurrency,
    EnteredAs EnteredAs,
    decimal Amount,
    decimal Shares,
    decimal Fee,
    string StrategyCurrency,
    decimal FxRate,
    FxSource FxSource,
    TradeSource Source,
    Guid? SignalId);

/// <summary>A signal in the strategy's signal history.</summary>
public record StrategySignalItem(
    Guid Id,
    Guid AssetId,
    string Symbol,
    string StockName,
    DateOnly SignalDate,
    TradeSide Side,
    int? StepNo,
    int? MaxSteps,
    decimal? StepAmount,
    decimal ClosePrice,
    string AssetCurrency,
    string Reason,
    SignalStatus Status,
    SkipReason? SkipReason,
    DateOnly? SnoozedUntil,
    DateOnly? EndedOn,
    DateTimeOffset CreatedAt);

/// <summary>Stored reference rates for an asset's currency against the strategy's currency.</summary>
public record CurrencyRates(string Currency, IReadOnlyList<RateDay> Rates);

/// <summary>"Log a trade by hand" on a live strategy.</summary>
public record ManualTradeRequest(
    Guid AssetId,
    TradeSide Side,
    DateOnly? Date,
    decimal? Price,
    EnteredAs? EnteredAs,
    decimal? Amount,
    decimal? Shares,
    decimal? Fee,
    FxSource? FxSource,
    decimal? FxRate);

/// <summary>Edit an existing trade on a live strategy.</summary>
public record UpdateTradeRequest(
    DateOnly? Date,
    decimal? Price,
    EnteredAs? EnteredAs,
    decimal? Amount,
    decimal? Shares,
    decimal? Fee,
    FxSource? FxSource,
    decimal? FxRate);

/// <summary>The strategy page. <c>Words</c> are the template's for a stock that isn't bought; <c>Signals</c> is live only.</summary>
public record StrategyDetail(
    Guid Id,
    string Name,
    string TemplateKey,
    string TemplateName,
    StrategyMode Mode,
    DateOnly StartedOn,
    DateOnly Today,
    string Currency,
    BudgetUsed Budget,
    StrategyFigures? Results,
    PastResult? PastResult,
    SignalCounts? Signals,
    StockStatusTexts? Words,
    IReadOnlyList<StockRow> Stocks,
    IReadOnlyList<StrategyTradeItem> Trades,
    IReadOnlyList<StrategySignalItem> SignalHistory,
    IReadOnlyList<CurrencyRates> Rates);
