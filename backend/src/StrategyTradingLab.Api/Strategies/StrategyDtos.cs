using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Strategies;

// The strategy DTOs of frontend/src/api/types.ts (camelCase JSON, enums as camelCase strings).

/// <summary>What the Confirm screen sends. Params are not sent yet: a new strategy starts from the template's tested defaults. <c>Currency</c> is the one the user saw while setting the budget; without it the user's current one is used.</summary>
public record NewStrategy(string TemplateKey, string Name, StrategyMode? Mode, Budget Budget, IReadOnlyList<Guid> AssetIds,
    string? Currency = null);

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
public enum StockStatus { WaitingForYou, Signal, Holding, Building, Watching, NotCandidate, NotEnoughData, NotChecked }

/// <summary>
/// A stock of a strategy. <c>Steps</c> are the ones the results count (live: the user's confirmed buys), <c>RulesSteps</c>
/// the rules' own (live only). <c>Signal</c> is the waiting signal (live) or today's (simulation). <c>Result</c> is the
/// gain in percent of what was invested, null with no buys.
/// </summary>
public record StockRow(Guid AssetId, string Symbol, string Name, StockStatus Status, int Steps, int? RulesSteps, int? MaxSteps,
    decimal? DistancePct, Guid? SignalId, int? SignalStep, decimal Invested, decimal? Result);

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
    IReadOnlyList<StockRow> Stocks);
