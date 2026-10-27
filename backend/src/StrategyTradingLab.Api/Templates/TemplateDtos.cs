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
public record AiRoundDto(int EveryDays, int TopN);

/// <summary>
/// Everything the review screen (2 / 5) and How we tested (2c) show, plus the tested defaults.
/// Flat in JSON like the frontend's <c>TemplateDetail extends StrategyTemplate</c>; built from the summary.
/// </summary>
public record TemplateDetail : TemplateSummary
{
    public TemplateDetail(TemplateSummary summary) : base(summary) { }

    public required string Rationale { get; init; }
    public required IReadOnlyList<RuleDto> Rules { get; init; }
    public required Watches Watches { get; init; }
    public required BacktestDto? Backtest { get; init; }
    public required TemplateDefaults Defaults { get; init; }
    /// <summary>The rules Advanced settings can tune, in screen order; empty when the template has none yet.</summary>
    public required IReadOnlyList<SettingDto> Settings { get; init; }
    /// <summary>The groups the settings are shown in, in screen order; empty when the settings are one plain list.</summary>
    public required IReadOnlyList<SettingGroupDto> SettingGroups { get; init; }
}

/// <summary>
/// A setting on Advanced settings (2b). <c>Key</c> is its name in <c>Defaults.Params</c>, which holds the tested value.
/// <c>Sentence</c> has <c>{value}</c> where the value goes; <c>Value</c> reads a number, with <c>{n}</c> for it.
/// <c>Below</c> names the setting this one stays at least a step under. <c>Help</c> says what changing it does, and
/// <c>Group</c> is the key of its <see cref="SettingGroupDto"/>.
/// </summary>
public record SettingDto(
    string Key,
    string Sentence,
    string Value,
    decimal Step,
    decimal Min,
    decimal Max,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Below,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Help = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Group = null);

/// <summary>A titled group of settings: the part of the rules they tune ("Which stocks it watches").</summary>
public record SettingGroupDto(string Key, string Title, string Lead);

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

/// <summary>What a new strategy from this template starts with. <c>Params</c> is the template's params record.</summary>
public record TemplateDefaults(int Version, Budget Budget, object Params);
