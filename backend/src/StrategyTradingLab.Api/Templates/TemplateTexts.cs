using System.Collections.Frozen;
using System.Text.Json;

namespace StrategyTradingLab.Api.Templates;

/// <summary>
/// The words of a template in one language. Each language is one file, Texts/{locale}.json (embedded),
/// keyed by template key. In rule texts, **bold** marks the rule's plain label and `backticks` mark
/// numbers and terms. Every template has English texts (TemplateTests checks it).
/// </summary>
public record TemplateTexts(
    string Name,
    string Tagline,
    string Idea,
    string Rationale,
    IReadOnlyList<RuleDto> Rules,
    BacktestTexts? Backtest,
    StockStatusTexts? StockStatus = null,
    IReadOnlyDictionary<string, SettingTexts>? Settings = null,
    IReadOnlyDictionary<string, SettingGroupTexts>? SettingGroups = null,
    IReadOnlyDictionary<string, CheckTexts>? Checks = null)
{
    public const string DefaultLocale = "en";

    static readonly FrozenDictionary<string, Dictionary<string, TemplateTexts>> ByLocale = Load();

    /// <summary>The texts in <paramref name="locale"/>, or in English when that language has none.</summary>
    public static TemplateTexts For(Template template, string locale) =>
        (ByLocale.TryGetValue(locale, out var texts) && texts.TryGetValue(template.Key, out var found))
            ? found
            : ByLocale[DefaultLocale][template.Key];

    static FrozenDictionary<string, Dictionary<string, TemplateTexts>> Load()
    {
        var assembly = typeof(TemplateTexts).Assembly;
        return assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith("templates/", StringComparison.Ordinal))
            .ToFrozenDictionary(
                name => Path.GetFileNameWithoutExtension(name),
                name =>
                {
                    using var stream = assembly.GetManifestResourceStream(name)!;
                    return JsonSerializer.Deserialize<Dictionary<string, TemplateTexts>>(stream, JsonSerializerOptions.Web)!;
                });
    }
}

/// <summary>
/// How the strategy page words a stock that is not bought: <c>Zone</c> is what <c>distancePct</c> measures to ("the discount
/// zone"), <c>NotCandidate</c> the detail line of a stock the rules don't watch yet ("Below its long trend" or "{n}% below its
/// 6-month high"), <c>Watching</c> a custom watching line ("In a drop: {n}% above its low"), and <c>Holding</c> a custom done line.
/// </summary>
public record StockStatusTexts(string Zone, string NotCandidate, string? Watching = null, string? Holding = null);

/// <summary>
/// The words of a rule check on the stock page (Engine <c>RuleCheck</c>). <c>Label</c> names the rule, <c>Detail</c> says where
/// the stock stands and <c>Short</c> says it in a few words for the strategy page's list; both hold <c>{value}</c> (or
/// <c>{+value}</c>, signed), <c>{min}</c>, <c>{max}</c> and <c>{level}</c> (a price). <c>None</c> replaces both when the check
/// has no value (never beaten down, no pullback yet). <c>Info</c> explains the rule.
/// </summary>
public record CheckTexts(string Label, string Detail, string Short, string? None = null, string? Info = null);

/// <summary>The words of the "Tested" box and How we tested.</summary>
public record BacktestTexts(string Scope, string Years, string Universe, string TestedDefaults);

/// <summary>
/// The words of a setting on Advanced settings. <c>Sentence</c> holds <c>{value}</c> where the value goes, and <c>Value</c>
/// how a value reads, with <c>{n}</c> for the number ("{n}%", "{n} days"). <c>Help</c> says what changing it does.
/// </summary>
public record SettingTexts(string Sentence, string Value, string? Help = null);

/// <summary>A titled group of settings on Advanced settings: <c>Lead</c> says what that part of the rules decides.</summary>
public record SettingGroupTexts(string Title, string Lead);
