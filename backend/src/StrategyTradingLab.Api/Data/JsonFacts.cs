using System.Globalization;
using System.Text.Json;

namespace StrategyTradingLab.Api.Data;

/// <summary>
/// Reads one value from a JSON object kept in a column (an evaluation's or a signal's facts, a strategy's params): null when
/// the document isn't an object, the name is missing or the value is of another kind.
/// </summary>
public static class JsonFacts
{
    static JsonElement? Get(this JsonElement facts, string name) =>
        facts.ValueKind == JsonValueKind.Object && facts.TryGetProperty(name, out var value) ? value : null;

    public static decimal? Number(this JsonElement facts, string name) =>
        facts.Get(name) is { ValueKind: JsonValueKind.Number } value ? value.GetDecimal() : null;

    public static int? Int(this JsonElement facts, string name) =>
        facts.Get(name) is { ValueKind: JsonValueKind.Number } value && value.TryGetInt32(out var number) ? number : null;

    public static string? Text(this JsonElement facts, string name) =>
        facts.Get(name) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;

    /// <summary>A day as stored ("2026-11-15").</summary>
    public static DateOnly? Date(this JsonElement facts, string name) =>
        facts.Text(name) is { } text && DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;

    /// <summary>The numbers stored under <paramref name="names"/>, leaving out the missing ones (a strategy's settings).</summary>
    public static Dictionary<string, decimal> Numbers(this JsonElement facts, IEnumerable<string> names) =>
        names.Select(name => (name, value: facts.Number(name))).Where(x => x.value is not null).ToDictionary(x => x.name, x => x.value!.Value);
}
