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
    StockStatusTexts? StockStatus = null)
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
/// zone"), <c>NotCandidate</c> the detail line of a stock the rules don't watch yet ("Below its long trend"). Only for
/// templates the engine runs.
/// </summary>
public record StockStatusTexts(string Zone, string NotCandidate);

/// <summary>The words of the "Tested" box and How we tested.</summary>
public record BacktestTexts(string Scope, string Years, string Universe, string TestedDefaults);
