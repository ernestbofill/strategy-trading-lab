using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Strategies;

// The strategy DTOs of frontend/src/api/types.ts (camelCase JSON, enums as camelCase strings).

/// <summary>What the Confirm screen sends. Params are not sent yet: a new strategy starts from the template's tested defaults.</summary>
public record NewStrategy(string TemplateKey, string Name, StrategyMode? Mode, Budget Budget, IReadOnlyList<Guid> AssetIds);

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

/// <summary>What a strategy has earned since it started, next to the index fund. Null until it has trades.</summary>
public record StrategyResults(decimal Strategy, decimal Index, IReadOnlyList<decimal> History);

/// <summary>How the template did in the past, shown while a strategy has no results of its own.</summary>
public record PastResult(string Years, decimal Template, decimal Index, decimal WorstYear);

public enum WaitingKind { BuySignal, SellSignal, AiRound }

/// <summary>Something that needs the user.</summary>
public record WaitingItem(Guid Id, WaitingKind Kind, Guid StrategyId, string StrategyName, string? StockName);

/// <summary><c>Today</c> is the user's local date, to tell a strategy started today from an older one.</summary>
public record Dashboard(DateOnly Today, IReadOnlyList<StrategySummary> Strategies, IReadOnlyList<WaitingItem> Waiting);
