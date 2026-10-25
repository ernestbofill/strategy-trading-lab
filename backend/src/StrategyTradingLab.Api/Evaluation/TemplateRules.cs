using System.Text.Json;
using StrategyTradingLab.Api.Engine;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Evaluation;

/// <summary>Builds a template's engine rules for one asset's history from a strategy's stored params.</summary>
public static class TemplateRules
{
    /// <summary>Whether the engine can run this template yet (the AI template comes later).</summary>
    public static bool Supports(string templateKey) => templateKey is "dip" or "comeback" or "bear" or "trend";

    /// <summary>New rules for one run (they keep state between days), from the strategy's <c>params</c> JSON.</summary>
    public static IBuyRules Create(string templateKey, IReadOnlyList<PriceBar> bars, JsonDocument parameters) => templateKey switch
    {
        "dip" => new DipRules(bars, parameters.Deserialize<DipParams>(JsonSerializerOptions.Web) ?? new DipParams()),
        "comeback" => new ComebackRules(bars, parameters.Deserialize<ComebackParams>(JsonSerializerOptions.Web) ?? new ComebackParams()),
        "bear" => new BearRules(bars, parameters.Deserialize<BearParams>(JsonSerializerOptions.Web) ?? new BearParams()),
        "trend" => new TrendRules(bars, parameters.Deserialize<TrendParams>(JsonSerializerOptions.Web) ?? new TrendParams()),
        _ => throw new ArgumentException($"The engine has no rules for '{templateKey}' yet.", nameof(templateKey)),
    };
}
