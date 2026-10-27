using StrategyTradingLab.Api.Auth;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace StrategyTradingLab.Api.Templates;

/// <summary>
/// A rule the user can tune on Advanced settings (2b): a number of the template's params, by its camelCase JSON name.
/// The screen offers <c>Min</c> to <c>Max</c> in steps of <c>Step</c>. <c>Below</c> names another setting this one must stay
/// at least a step under (the discount zone starts before it ends). <c>Group</c> names the part of the rules it tunes
/// (a key of the texts' <c>SettingGroups</c>); the screen shows the groups in the order their first setting comes. Its words
/// are in the template's texts.
/// </summary>
public record Setting(string Key, decimal Step, decimal Min, decimal Max, string? Below = null, string? Group = null);

/// <summary>How the user's settings become a strategy's params.</summary>
public static class TemplateSettings
{
    /// <summary>
    /// The template's params with <paramref name="settings"/> (setting key → value) over the tested defaults, as
    /// strategies.params stores them. Params that aren't settings keep their <paramref name="current"/> values (a running
    /// strategy's: the AI template's prompt choices). Null, with the reasons in <paramref name="fail"/>, when a value is refused.
    /// </summary>
    public static JsonDocument? Apply(Template template, IReadOnlyDictionary<string, decimal>? settings, Action<string, string> fail, string locale = "en",
        JsonDocument? current = null)
    {
        var json = (JsonObject)JsonSerializer.SerializeToNode(template.Params, template.Params.GetType(), JsonSerializerOptions.Web)!;
        if (current?.RootElement.ValueKind == JsonValueKind.Object)
            foreach (var property in current.RootElement.EnumerateObject())
                if (json.ContainsKey(property.Name) && template.Settings.All(s => s.Key != property.Name))
                    json[property.Name] = JsonNode.Parse(property.Value.GetRawText());
        if (settings is null || settings.Count == 0) return JsonSerializer.SerializeToDocument(json, JsonSerializerOptions.Web);

        var ok = true;
        foreach (var (key, value) in settings)
        {
            var field = $"settings.{key}";
            if (template.Settings.FirstOrDefault(s => s.Key == key) is not { } setting)
            {
                fail(field, Messages.NoSuchSetting.In(locale));
                ok = false;
            }
            else if (value < setting.Min || value > setting.Max || (value - setting.Min) % setting.Step != 0)
            {
                fail(field, Messages.SettingRange.In(locale, setting.Min, setting.Max, setting.Step));
                ok = false;
            }
            else
            {
                // Without trailing zeros, so 40.0 reads as 40 into a whole-day param.
                json[key] = value / 1.0000000000000000000000000000m;
            }
        }
        if (!ok) return null;

        // The screen keeps a setting at least a step under the one it must stay below; so does the API.
        foreach (var setting in template.Settings.Where(s => s.Below is not null))
        {
            if (json[setting.Key]!.GetValue<decimal>() + setting.Step > json[setting.Below!]!.GetValue<decimal>())
            {
                fail($"settings.{setting.Key}", Messages.SettingBelow.In(locale, setting.Step));
                return null;
            }
        }

        var merged = json.Deserialize(template.Params.GetType(), JsonSerializerOptions.Web)!;
        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(merged, new ValidationContext(merged), results, validateAllProperties: true))
        {
            // The params' own checks, such as a discount zone that ends before it starts.
            fail("settings", string.Join(" ", results.Select(r => r.ErrorMessage)));
            return null;
        }
        return JsonSerializer.SerializeToDocument(merged, merged.GetType(), JsonSerializerOptions.Web);
    }
}
