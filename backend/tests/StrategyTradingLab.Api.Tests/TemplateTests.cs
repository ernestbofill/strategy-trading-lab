using System.ComponentModel.DataAnnotations;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Tests;

/// <summary>The five templates' definitions, texts and seeded assets. No database.</summary>
public class TemplateTests
{
    [Fact]
    public void The_catalogue_has_the_five_templates_in_pick_screen_order()
    {
        Assert.Equal(["dip", "comeback", "bear", "trend", "ai"], TemplateCatalog.Keys);
    }

    [Fact]
    public void Every_template_has_english_texts_with_rules()
    {
        foreach (var template in TemplateCatalog.All)
        {
            var texts = TemplateTexts.For(template, "en");
            Assert.NotEmpty(texts.Name);
            Assert.Equal(3, texts.Rules.Count);
            Assert.Equal(template.Backtest is null, texts.Backtest is null);
        }
    }

    [Fact]
    public void Every_template_has_catalan_texts_with_rules()
    {
        foreach (var template in TemplateCatalog.All)
        {
            var texts = TemplateTexts.For(template, "ca");
            Assert.NotEmpty(texts.Name);
            Assert.Equal(3, texts.Rules.Count);
            Assert.Equal(template.Backtest is null, texts.Backtest is null);
        }
    }

    [Fact]
    public void A_language_without_texts_falls_back_to_english()
    {
        Assert.Equal("Dip in winners", TemplateTexts.For(TemplateCatalog.Dip, "fr").Name);
    }

    [Fact]
    public void Default_params_are_valid()
    {
        foreach (var template in TemplateCatalog.All)
        {
            var results = new List<ValidationResult>();
            var valid = Validator.TryValidateObject(template.Params, new ValidationContext(template.Params),
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
        var dip = TemplateCatalog.Dip.ParamsJson().RootElement;

        Assert.Equal(60, dip.GetProperty("peakDays").GetInt32());
        Assert.Equal(5, dip.GetProperty("zoneStartPct").GetDecimal());
        Assert.Equal(15, dip.GetProperty("zoneEndPct").GetDecimal());
        Assert.Equal(1.2m, TemplateCatalog.Comeback.ParamsJson().RootElement.GetProperty("volumeRatio").GetDecimal());
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
        foreach (var template in TemplateCatalog.All)
        {
            var expected = template.Watches is DefaultFund ? AssetKind.Fund : AssetKind.Stock;
            Assert.All(template.Watches.Assets, asset =>
            {
                Assert.Contains(asset, TemplateAssets.All);
                Assert.Equal(expected, asset.Kind);
            });
        }
    }

    [Fact]
    public void Seeded_assets_are_unique()
    {
        Assert.Equal(TemplateAssets.All.Length, TemplateAssets.All.Select(asset => asset.Id).Distinct().Count());
        Assert.Equal(TemplateAssets.All.Length, TemplateAssets.All.Select(asset => (asset.Symbol, asset.Exchange)).Distinct().Count());
    }

    [Fact]
    public void The_ai_template_watches_the_stocks_of_both_stock_templates_once()
    {
        Assert.Equal(19, TemplateCatalog.Ai.Watches.Assets.Count());
    }

    [Theory]
    [InlineData(100, 98.80, 1.20)]
    [InlineData(10, 8.98, 1.02)]
    [InlineData(1000, 997.00, 3.00)]
    [InlineData(250, 248.50, 1.50)]
    public void A_step_includes_its_fee_and_the_two_add_up_to_it_exactly(decimal step, decimal amount, decimal fee)
    {
        var split = TemplateCatalog.Fee.Split(step);

        Assert.Equal((amount, fee), split);
        Assert.Equal(step, split.Amount + split.Fee);
        // Within a cent of the tested fee on what buys shares, never under it.
        var tested = split.Amount * TemplateCatalog.Fee.Percent / 100 + TemplateCatalog.Fee.Fixed;
        Assert.InRange(split.Fee - tested, 0m, 0.01m);
    }

    [Theory]
    [InlineData(0.50)]
    [InlineData(1)]
    [InlineData(1.01)]
    public void A_step_that_leaves_nothing_to_buy_with_is_refused(decimal step) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => TemplateCatalog.Fee.Split(step));
}
