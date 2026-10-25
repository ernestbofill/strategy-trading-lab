using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Tests;

/// <summary>Advanced settings: each template's tunable rules, and the user's values over the tested defaults. No database.</summary>
public class TemplateSettingsTests
{
    static (JsonDocument? Params, Dictionary<string, string> Errors) Apply(Template template, Dictionary<string, decimal>? settings)
    {
        var errors = new Dictionary<string, string>();
        return (TemplateSettings.Apply(template, settings, (field, message) => errors[field] = message), errors);
    }

    [Fact]
    public void Every_setting_is_a_param_with_words_a_reachable_default_and_a_range_the_engine_accepts()
    {
        foreach (var template in TemplateCatalog.All)
        {
            var defaults = template.ParamsJson().RootElement;
            var texts = TemplateTexts.For(template, "en");
            foreach (var setting in template.Settings)
            {
                var label = $"{template.Key}.{setting.Key}";
                Assert.True(defaults.TryGetProperty(setting.Key, out var tested), label);
                var value = tested.GetDecimal();
                Assert.InRange(value, setting.Min, setting.Max);
                Assert.Equal(0, (value - setting.Min) % setting.Step);

                Assert.True(texts.Settings?.ContainsKey(setting.Key), label);
                Assert.Contains("{value}", texts.Settings![setting.Key].Sentence);
                Assert.Contains("{n}", texts.Settings[setting.Key].Value);

                // The screen's range stays inside the params' own [Range].
                var property = template.Params.GetType().GetProperties()
                    .Single(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name) == setting.Key);
                var range = property.GetCustomAttribute<RangeAttribute>()!;
                Assert.True(setting.Min >= Convert.ToDecimal(range.Minimum) && setting.Max <= Convert.ToDecimal(range.Maximum), label);

                if (setting.Below is { } above) Assert.Contains(template.Settings, s => s.Key == above);
            }
        }
    }

    [Fact]
    public void Dip_in_winners_exposes_the_specs_five_settings_but_not_the_cooldown()
    {
        Assert.Equal(["zoneStartPct", "zoneEndPct", "peakDays", "trendDays", "floorDays"],
            TemplateCatalog.Dip.Settings.Select(s => s.Key));
    }

    [Fact]
    public void Ride_the_long_trend_exposes_its_average_and_signal_band_but_not_the_cooldown()
    {
        Assert.Equal(["smaDays", "bandPct"], TemplateCatalog.Trend.Settings.Select(s => s.Key));

        var (stored, errors) = Apply(TemplateCatalog.Trend, new() { ["smaDays"] = 150, ["bandPct"] = 4 });

        Assert.Empty(errors);
        Assert.Equal(150, stored!.RootElement.GetProperty("smaDays").GetInt32());
        Assert.Equal(4, stored.RootElement.GetProperty("bandPct").GetDecimal());
        Assert.Equal(5, stored.RootElement.GetProperty("cooldownDays").GetInt32());
    }

    [Fact]
    public void No_settings_keep_the_tested_defaults()
    {
        var (stored, errors) = Apply(TemplateCatalog.Dip, null);

        Assert.Empty(errors);
        Assert.Equal(TemplateCatalog.Dip.ParamsJson().RootElement.GetRawText(), stored!.RootElement.GetRawText());
    }

    [Fact]
    public void Changed_settings_replace_only_their_defaults()
    {
        var (stored, errors) = Apply(TemplateCatalog.Dip, new() { ["zoneStartPct"] = 3, ["peakDays"] = 40 });

        Assert.Empty(errors);
        var json = stored!.RootElement;
        Assert.Equal(3, json.GetProperty("zoneStartPct").GetDecimal());
        Assert.Equal(40, json.GetProperty("peakDays").GetInt32());
        Assert.Equal(15, json.GetProperty("zoneEndPct").GetDecimal());
        Assert.Equal(10, json.GetProperty("cooldownDays").GetInt32());
    }

    [Theory]
    [InlineData("cooldownDays", 5)]  // a param, but not exposed
    [InlineData("nope", 5)]
    [InlineData("peakDays", 15)]     // under the minimum
    [InlineData("peakDays", 65)]     // over the maximum
    [InlineData("peakDays", 42)]     // off the 5-day steps
    [InlineData("zoneStartPct", 2.5)]
    public void A_setting_the_screen_does_not_offer_is_refused(string key, double value)
    {
        var (stored, errors) = Apply(TemplateCatalog.Dip, new() { [key] = (decimal)value });

        Assert.Null(stored);
        Assert.Contains($"settings.{key}", errors.Keys);
    }

    [Theory]
    [InlineData(20, 10)]
    [InlineData(10, 10)]
    public void A_zone_that_does_not_end_a_step_after_it_starts_is_refused(int start, int end)
    {
        var (stored, errors) = Apply(TemplateCatalog.Dip, new() { ["zoneStartPct"] = start, ["zoneEndPct"] = end });

        Assert.Null(stored);
        Assert.Contains("settings.zoneStartPct", errors.Keys);
    }

    [Fact]
    public void A_whole_number_written_with_a_decimal_point_is_accepted_for_a_day_count()
    {
        var (stored, errors) = Apply(TemplateCatalog.Dip, new() { ["peakDays"] = 40.0m, ["zoneEndPct"] = 12.0m });

        Assert.Empty(errors);
        Assert.Equal(40, stored!.RootElement.GetProperty("peakDays").GetInt32());
        Assert.Equal(12, stored.RootElement.GetProperty("zoneEndPct").GetDecimal());
    }

    [Fact]
    public void A_template_without_settings_refuses_any()
    {
        var (stored, errors) = Apply(TemplateCatalog.Comeback, new() { ["drawdownPct"] = 40 });

        Assert.Null(stored);
        Assert.Contains("settings.drawdownPct", errors.Keys);
    }

    [Theory]
    [InlineData("smaDays", 55)]
    [InlineData("bandPct", 10.5)]
    public void Ride_refuses_values_outside_its_settings_steps(string key, double value)
    {
        var (stored, errors) = Apply(TemplateCatalog.Trend, new() { [key] = (decimal)value });

        Assert.Null(stored);
        Assert.Contains($"settings.{key}", errors.Keys);
    }
}
