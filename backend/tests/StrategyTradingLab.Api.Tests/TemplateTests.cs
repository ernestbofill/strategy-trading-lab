using System.ComponentModel.DataAnnotations;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Tests;

/// <summary>The five templates' definitions, copy and seeded assets. No database.</summary>
public class TemplateTests
{
    [Fact]
    public void The_catalogue_has_the_keys_the_database_allows_in_order()
    {
        Assert.Equal(TemplateKeys.All, TemplateCatalog.All.Select(template => template.Key));
    }

    [Fact]
    public void Every_template_has_english_copy_with_rules()
    {
        foreach (var template in TemplateCatalog.All)
        {
            var copy = TemplateCopy.For(template, "en");
            Assert.NotEmpty(copy.Name);
            Assert.Equal(3, copy.Rules.Count);
            Assert.Equal(template.Backtest is null, copy.Backtest is null);
        }
    }

    [Fact]
    public void A_language_without_copy_falls_back_to_english()
    {
        Assert.Equal("Dip in winners", TemplateCopy.For(TemplateCatalog.Dip, "ca").Name);
    }

    [Fact]
    public void Default_params_are_valid()
    {
        foreach (var template in TemplateCatalog.All)
        {
            var results = new List<ValidationResult>();
            var valid = Validator.TryValidateObject(template.DefaultParams, new ValidationContext(template.DefaultParams),
                results, validateAllProperties: true);
            Assert.True(valid, $"{template.Key}: {string.Join("; ", results.Select(r => r.ErrorMessage))}");
        }
    }

    [Fact]
    public void Dip_rejects_a_zone_that_ends_before_it_starts()
    {
        var bad = new DipParams { ZoneStartPct = 15, ZoneEndPct = 5 };

        Assert.False(Validator.TryValidateObject(bad, new ValidationContext(bad), [], validateAllProperties: true));
    }

    [Fact]
    public void Params_are_stored_as_camel_case_json_with_the_poc_defaults()
    {
        var dip = TemplateCatalog.Dip.DefaultParamsJson().RootElement;

        Assert.Equal(60, dip.GetProperty("peakDays").GetInt32());
        Assert.Equal(5, dip.GetProperty("zoneStartPct").GetDecimal());
        Assert.Equal(15, dip.GetProperty("zoneEndPct").GetDecimal());
        Assert.Equal(1.2m, TemplateCatalog.Comeback.DefaultParamsJson().RootElement.GetProperty("volumeRatio").GetDecimal());
    }

    [Fact]
    public void Budgets_are_steps_except_ride_the_long_trend_which_is_all_in()
    {
        Assert.Equal(new Budget(3000, 100, 5), TemplateCatalog.Dip.Budget);
        Assert.Equal(new Budget(3000, 600, 5), TemplateCatalog.Bear.Budget);
        Assert.Equal(new Budget(3000, null, null), TemplateCatalog.Trend.Budget);
    }

    [Fact]
    public void Every_template_asset_is_seeded_with_the_right_kind()
    {
        var kinds = TemplateAssets.All.ToDictionary(asset => asset.Id, asset => asset.Kind);

        Assert.Empty(TemplateCatalog.All.SelectMany(template => TemplateAssetsCheck.Problems(template, kinds)));
    }

    [Fact]
    public void The_check_reports_a_missing_asset()
    {
        var problems = TemplateAssetsCheck.Problems(TemplateCatalog.Bear, new Dictionary<Guid, AssetKind>());

        Assert.Equal(["bear: SPY is missing or inactive"], problems);
    }

    [Fact]
    public void Seeded_assets_are_unique_us_listings()
    {
        Assert.Equal(TemplateAssets.All.Length, TemplateAssets.All.Select(asset => asset.Id).Distinct().Count());
        Assert.Equal(TemplateAssets.All.Length, TemplateAssets.All.Select(asset => (asset.Symbol, asset.Exchange)).Distinct().Count());
        Assert.All(TemplateAssets.All, asset => Assert.Contains(asset.Exchange, new[] { "XNYS", "XNAS", "ARCX" }));
    }
}
