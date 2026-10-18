using System.Collections.Frozen;
using System.Text.Json;

namespace StrategyTradingLab.Api.Templates;

/// <summary>
/// The words of a template in one language, from Copy/{key}.{locale}.json (embedded).
/// In rule texts, **bold** marks the rule's plain label and `backticks` mark numbers and terms.
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

    static readonly FrozenDictionary<(string Key, string Locale), TemplateCopy> All = Load();

    /// <summary>The copy in <paramref name="locale"/>, or in English when that language has none.</summary>
    public static TemplateCopy For(Template template, string locale) =>
        All.GetValueOrDefault((template.Key, locale)) ?? All[(template.Key, DefaultLocale)];

    static FrozenDictionary<(string, string), TemplateCopy> Load()
    {
        var assembly = typeof(TemplateCopy).Assembly;
        var copies = new Dictionary<(string, string), TemplateCopy>();
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith("templates/")))
        {
            // templates/{key}.{locale}.json
            var parts = name["templates/".Length..].Split('.');
            using var stream = assembly.GetManifestResourceStream(name)!;
            copies[(parts[0], parts[1])] = JsonSerializer.Deserialize<TemplateCopy>(stream, JsonSerializerOptions.Web)
                ?? throw new InvalidOperationException($"Empty template copy: {name}");
        }

        var missing = TemplateCatalog.All.Where(t => !copies.ContainsKey((t.Key, DefaultLocale))).Select(t => t.Key).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException($"No English copy for template(s): {string.Join(", ", missing)}");
        return copies.ToFrozenDictionary();
    }
}

/// <summary>The words of the "Tested" box and How we tested.</summary>
public record BacktestCopy(string Scope, string Provider, string Years, string Universe, string TestedDefaults);
