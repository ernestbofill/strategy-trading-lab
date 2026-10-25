using Microsoft.AspNetCore.Identity;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Strategies;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Tests;

/// <summary>The rules for starting a strategy, and the seeded user. No database.</summary>
public class NewStrategyValidationTests
{
    static Asset Stock(bool active = true) => new()
        { Id = Guid.NewGuid(), Symbol = "X", Exchange = "XNAS", Name = "X", Kind = AssetKind.Stock, Currency = "USD", Active = active };

    static Asset Fund() => new()
        { Id = Guid.NewGuid(), Symbol = "F", Exchange = "XLON", Name = "F", Kind = AssetKind.Fund, Currency = "USD" };

    static Dictionary<string, string[]> Check(Template template, Budget budget, string name = "My strategy", params Asset[] assets) =>
        NewStrategyValidation.Validate(
            new NewStrategy(template.Key, name, StrategyMode.Simulation, budget, [.. assets.Select(a => a.Id)]), template, assets);

    [Fact]
    public void The_tested_defaults_with_a_stock_are_valid()
    {
        Assert.Empty(Check(TemplateCatalog.Dip, TemplateCatalog.Dip.Budget, assets: Stock()));
        Assert.Empty(Check(TemplateCatalog.Trend, TemplateCatalog.Trend.Budget, assets: Fund()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_name_is_rejected(string name) =>
        Assert.Contains("name", Check(TemplateCatalog.Dip, TemplateCatalog.Dip.Budget, name, Stock()).Keys);

    [Fact]
    public void A_name_over_the_limit_is_rejected() =>
        Assert.Contains("name", Check(TemplateCatalog.Dip, TemplateCatalog.Dip.Budget, new string('a', 61), Stock()).Keys);

    [Theory]
    [InlineData(null)]
    [InlineData(99)]
    public void A_missing_or_unknown_mode_is_rejected(int? mode)
    {
        var request = new NewStrategy("dip", "A", (StrategyMode?)mode, TemplateCatalog.Dip.Budget, []);

        Assert.Contains("mode", NewStrategyValidation.Validate(request, TemplateCatalog.Dip, []).Keys);
    }

    [Fact]
    public void Errors_can_be_returned_in_catalan()
    {
        var request = new NewStrategy("dip", "", StrategyMode.Simulation, TemplateCatalog.Dip.Budget, []);
        var errors = NewStrategyValidation.Validate(request, TemplateCatalog.Dip, [], locale: "ca");
        Assert.Equal(["Fes servir d'1 a 60 caràcters."], errors["name"]);
        Assert.Equal(["Tria almenys una acció."], errors["assetIds"]);
    }

    [Theory]
    [InlineData("USD", false)]
    [InlineData(null, false)]
    [InlineData("JPY", true)]
    [InlineData("eur", true)]
    public void A_currency_off_the_list_is_rejected(string? currency, bool rejected)
    {
        var request = new NewStrategy("dip", "A", StrategyMode.Live, TemplateCatalog.Dip.Budget, [], currency);

        Assert.Equal(rejected, NewStrategyValidation.Validate(request, TemplateCatalog.Dip, []).ContainsKey("currency"));
    }

    [Theory]
    [InlineData(499, 100, 5)]
    [InlineData(1_000_001, 100, 5)]
    [InlineData(3000.5, 100, 5)]
    public void A_budget_outside_the_limits_is_rejected(double amount, double step, int maxSteps) =>
        Assert.Contains("budget.amount",
            Check(TemplateCatalog.Dip, new Budget((decimal)amount, (decimal)step, maxSteps), assets: Stock()).Keys);

    [Theory]
    [InlineData(9, 5, "budget.step")]
    [InlineData(3001, 5, "budget.step")]
    [InlineData(100, 0, "budget.maxSteps")]
    [InlineData(100, 21, "budget.maxSteps")]
    public void A_step_template_needs_a_valid_step_and_count(double step, int maxSteps, string field) =>
        Assert.Contains(field, Check(TemplateCatalog.Dip, new Budget(3000, (decimal)step, maxSteps), assets: Stock()).Keys);

    [Fact]
    public void A_step_template_without_steps_is_rejected() =>
        Assert.Equal(["budget.step", "budget.maxSteps"],
            Check(TemplateCatalog.Dip, new Budget(3000, null, null), assets: Stock()).Keys);

    [Fact]
    public void An_all_in_template_takes_no_steps() =>
        Assert.Contains("budget", Check(TemplateCatalog.Trend, new Budget(3000, 100, 5), assets: Fund()).Keys);

    [Fact]
    public void Stocks_are_needed_and_each_listed_once()
    {
        var stock = Stock();
        Assert.Contains("assetIds", Check(TemplateCatalog.Dip, TemplateCatalog.Dip.Budget).Keys);
        var twice = NewStrategyValidation.Validate(
            new NewStrategy("dip", "A", StrategyMode.Live, TemplateCatalog.Dip.Budget, [stock.Id, stock.Id]), TemplateCatalog.Dip, [stock]);
        Assert.Contains("assetIds", twice.Keys);
    }

    [Fact]
    public void An_unknown_or_delisted_stock_is_rejected()
    {
        var unknown = new NewStrategy("dip", "A", StrategyMode.Live, TemplateCatalog.Dip.Budget, [Guid.NewGuid()]);
        Assert.Contains("assetIds", NewStrategyValidation.Validate(unknown, TemplateCatalog.Dip, []).Keys);
        Assert.Contains("assetIds", Check(TemplateCatalog.Dip, TemplateCatalog.Dip.Budget, assets: Stock(active: false)).Keys);
    }

    [Fact]
    public void A_fund_template_takes_exactly_one_fund_and_a_stock_template_no_fund()
    {
        Assert.Contains("assetIds", Check(TemplateCatalog.Trend, TemplateCatalog.Trend.Budget, assets: [Fund(), Fund()]).Keys);
        Assert.Contains("assetIds", Check(TemplateCatalog.Trend, TemplateCatalog.Trend.Budget, assets: Stock()).Keys);
        Assert.Contains("assetIds", Check(TemplateCatalog.Dip, TemplateCatalog.Dip.Budget, assets: Fund()).Keys);
    }

    [Fact]
    public void Every_template_s_tested_defaults_are_valid_with_its_own_assets()
    {
        foreach (var template in TemplateCatalog.All)
        {
            var assets = template.Watches.Assets.Select(seed => new Asset
            {
                Id = seed.Id, Symbol = seed.Symbol, Exchange = "X", Name = seed.Name, Currency = "USD",
                Kind = template.Watches is DefaultFund ? AssetKind.Fund : AssetKind.Stock,
            }).ToList();
            Assert.Empty(Check(template, template.Budget, assets: [.. assets]));
        }
    }
}
