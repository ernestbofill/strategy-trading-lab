using System.Text.Json;
using System.Text.Json.Serialization;
using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Templates;

// The template DTOs of frontend/src/api/types.ts (camelCase JSON, enums as camelCase strings).

/// <summary>A template as listed on the pick screen.</summary>
public record TemplateSummary(
    string Key,
    string? EngineKey,
    string Name,
    string Tagline,
    string Idea,
    InvestsIn InvestsIn,
    SignalSides Signals,
    AiRoundDto? AiRound);

/// <summary>Set when signals come from the user's AI rounds instead of price rules.</summary>
public record AiRoundDto(int EveryWeeks, int TopN);

/// <summary>Everything the review screen (2 / 5) and How we tested (2c) show, plus the tested defaults.</summary>
public record TemplateDetail(
    string Key,
    string? EngineKey,
    string Name,
    string Tagline,
    string Idea,
    InvestsIn InvestsIn,
    SignalSides Signals,
    AiRoundDto? AiRound,
    string Rationale,
    IReadOnlyList<RuleDto> Rules,
    Watches Watches,
    BacktestDto? Backtest,
    TemplateDefaults Defaults)
    : TemplateSummary(Key, EngineKey, Name, Tagline, Idea, InvestsIn, Signals, AiRound);

/// <summary>One line of "It signals when". <c>Marker</c> is "01", "BUY", "SELL"…</summary>
public record RuleDto(
    string Marker,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Tone,
    string Text);

/// <summary>A US-listed stock or fund. <c>What</c> is one line on what the company does, or what the fund tracks.</summary>
public record SecurityDto(Guid Id, string Symbol, string Name, string What);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(StockWatches), "stocks")]
[JsonDerivedType(typeof(FundWatches), "fund")]
public abstract record Watches;

public record StockWatches(IReadOnlyList<SecurityDto> StarterList) : Watches;

public record FundWatches(SecurityDto Fund) : Watches;

/// <summary>The "Tested" box and How we tested. <c>Periods</c>: sub-periods, then the whole period last.</summary>
public record BacktestDto(
    string Scope,
    IReadOnlyList<PeriodResult> Periods,
    string Provider,
    string Years,
    string Universe,
    string TestedDefaults,
    decimal FeePerTrade,
    decimal FeePercent);

/// <summary>What a new strategy from this template starts with.</summary>
public record TemplateDefaults(int Version, Budget Budget, JsonDocument Params);
