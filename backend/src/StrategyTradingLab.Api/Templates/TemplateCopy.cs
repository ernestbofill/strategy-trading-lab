using System.Collections.Frozen;
using System.Text.Json;

namespace StrategyTradingLab.Api.Templates;

/// <summary>
/// The words of a template in one language. Each language is one file, Copy/{locale}.json (embedded),
/// keyed by template key. In rule texts, **bold** marks the rule's plain label and `backticks` mark
/// numbers and terms. Every template has English copy (TemplateTests checks it).
/// </summary>
public record TemplateCopy(
    string Name,
    string Tagline,
    string Idea,
    string Rationale,
    IReadOnlyList<RuleDto> Rules,
    BacktestCopy? Backtest)
{
    public const string DefaultLocale = "en";

    static readonly FrozenDictionary<string, Dictionary<string, TemplateCopy>> ByLocale = Load();

    /// <summary>The copy in <paramref name="locale"/>, or in English when that language has none.</summary>
    public static TemplateCopy For(Template template, string locale) =>
        (ByLocale.TryGetValue(locale, out var copies) && copies.TryGetValue(template.Key, out var copy))
            ? copy
            : ByLocale[DefaultLocale][template.Key];

    static FrozenDictionary<string, Dictionary<string, TemplateCopy>> Load()
    {
        var assembly = typeof(TemplateCopy).Assembly;
        return assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith("templates/", StringComparison.Ordinal))
            .ToFrozenDictionary(
                name => Path.GetFileNameWithoutExtension(name),
                name =>
                {
                    using var stream = assembly.GetManifestResourceStream(name)!;
                    return JsonSerializer.Deserialize<Dictionary<string, TemplateCopy>>(stream, JsonSerializerOptions.Web)!;
                });
    }
}

/// <summary>The words of the "Tested" box and How we tested.</summary>
public record BacktestCopy(string Scope, string Years, string Universe, string TestedDefaults);
