using System.Text.Json;
using StrategyTradingLab.Api.Engine;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Tests;

/// <summary>
/// The stock page's rule checks agree with the engine's verdict on every day of the golden price runs: a candidate's checks
/// are all met, a buy signal's checks are all met, and a stock that isn't a candidate fails one of the candidate checks.
/// </summary>
public class RuleChecksTests
{
    record Golden(string[] Dates, decimal[] Close, long[]? Volume);

    static IReadOnlyList<PriceBar> Bars(string name)
    {
        var g = JsonSerializer.Deserialize<Golden>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Golden", $"{name}.json")), JsonSerializerOptions.Web)!;
        return [.. g.Dates.Select((d, i) => new PriceBar(DateOnly.Parse(d), g.Close[i], g.Close[i], g.Close[i], g.Close[i], g.Volume?[i] ?? 0))];
    }

    static IEnumerable<RuleCheck> Group(DayEvaluation day, string group) => day.Checks.Where(c => c.Group == group);

    static bool AllMet(DayEvaluation day, string group) => Group(day, group).All(c => c.Met != false);

    static void Holds(bool condition, DayEvaluation day, string what) =>
        Assert.True(condition, $"{day.Date}: {day.Status}, {what}: {string.Join(", ", day.Checks.Select(c => $"{c.Key}={c.Met}"))}");

    [Theory]
    [InlineData("dip-ETN-a")]
    [InlineData("dip-MPWR")]
    [InlineData("dip-NOW")]
    [InlineData("dip-CRM")]
    public void Winners_at_a_discount(string name)
    {
        var bars = Bars(name);
        var days = RulesRunner.Run(new DipRules(bars, new DipParams()), bars, new StepPlan(5)).Where(d => d.Checks.Count > 0).ToList();

        Assert.NotEmpty(days);
        foreach (var day in days)
        {
            if (day.Status == RuleStatus.BuySignal) Holds(AllMet(day, CheckGroup.Candidate) && AllMet(day, CheckGroup.Buy), day, "a signal meets every check");
            if (day.Status == RuleStatus.NotCandidate) Holds(!AllMet(day, CheckGroup.Candidate), day, "not a candidate fails a candidate check");
            if (day.Status == RuleStatus.Watching) Holds(AllMet(day, CheckGroup.Candidate) && !AllMet(day, CheckGroup.Buy), day, "a candidate waits for a buy check");
        }
        Assert.Contains(days, d => d.Status == RuleStatus.BuySignal);
    }

    [Theory]
    [InlineData("comeback-MOD-800")]
    [InlineData("comeback-NOW-2400")]
    [InlineData("comeback-VRT-800")]
    public void The_comeback(string name)
    {
        var bars = Bars(name);
        var days = RulesRunner.Run(new ComebackRules(bars, new ComebackParams()), bars, new StepPlan(5)).Where(d => d.Checks.Count > 0).ToList();

        foreach (var day in days.Where(d => !Group(d, CheckGroup.Next).Any()))
        {
            var beaten = day.Checks.Single(c => c.Key == "beaten");
            if (day.Status == RuleStatus.NotCandidate) Holds(beaten.Met == false, day, "not a candidate is not beaten down lately");
            if (beaten.Met == false) Holds(day.Status is RuleStatus.NotCandidate or RuleStatus.SignalEnded, day, "not beaten down lately is no candidate");
            if (day.Status == RuleStatus.BuySignal) Holds(AllMet(day, CheckGroup.Candidate) && AllMet(day, CheckGroup.Buy), day, "a signal meets every check");
        }
        // Holding a position: a step's signal comes from a milestone in its window.
        foreach (var day in days.Where(d => d.Status == RuleStatus.BuySignal && Group(d, CheckGroup.Next).Any()))
            Holds(Group(day, CheckGroup.Next).Any(c => c.Met == true && c.Used != true), day, "a step's signal has an open milestone");
        Assert.Contains(days, d => d.Status == RuleStatus.BuySignal);
    }

    [Theory]
    [InlineData("bear-SPY-5600")]
    [InlineData("bear-SPY-3800")]
    public void Bear_markets(string name)
    {
        var bars = Bars(name);
        var days = RulesRunner.Run(new BearRules(bars, new BearParams()), bars, new StepPlan(5)).Where(d => d.Checks.Count > 0).ToList();

        foreach (var day in days)
        {
            var drop = day.Checks.Single(c => c.Key == "drop");
            Holds((day.Status == RuleStatus.NotCandidate) == (drop.Met == false), day, "not a candidate exactly when not in a drop");
            if (day.Status == RuleStatus.BuySignal) Holds(AllMet(day, CheckGroup.Buy) && AllMet(day, CheckGroup.Next), day, "a signal meets every check");
        }
        Assert.Contains(days, d => d.Status == RuleStatus.BuySignal);
    }

    [Theory]
    [InlineData("trend-SPY-5600")]
    [InlineData("trend-SPY-0")]
    public void Index_trend(string name)
    {
        var bars = Bars(name);
        var days = RulesRunner.Run(new TrendRules(bars, new TrendParams()), bars, new StepPlan(1)).Where(d => d.Checks.Count > 0).ToList();

        foreach (var day in days)
        {
            if (day.Status == RuleStatus.BuySignal) Holds(day.Checks.Single().Key == "buyLine" && day.Checks.Single().Met == true, day, "a buy is above the buy line");
            if (day.Status == RuleStatus.SellSignal) Holds(day.Checks.Single().Key == "sellLine" && day.Checks.Single().Met == true, day, "a sale is below the sell line");
            if (day.Status == RuleStatus.Holding) Holds(day.Checks.Single().Met == false, day, "holding is above the sell line");
        }
    }

    [Theory]
    [InlineData("dip", "dip-NOW")]
    [InlineData("comeback", "comeback-NOW-2400")]
    [InlineData("bear", "bear-SPY-5600")]
    [InlineData("trend", "trend-SPY-5600")]
    public void Every_check_has_words_in_each_language(string templateKey, string name)
    {
        var bars = Bars(name);
        var template = TemplateCatalog.Find(templateKey)!;
        IBuyRules rules = templateKey switch
        {
            "dip" => new DipRules(bars, new DipParams()),
            "comeback" => new ComebackRules(bars, new ComebackParams()),
            "bear" => new BearRules(bars, new BearParams()),
            _ => new TrendRules(bars, new TrendParams()),
        };
        var keys = RulesRunner.Run(rules, bars, new StepPlan(templateKey == "trend" ? 1 : 5)).SelectMany(d => d.Checks).Select(c => c.Key).ToHashSet();

        Assert.NotEmpty(keys);
        foreach (var locale in new[] { "en", "ca" })
        {
            var checks = TemplateTexts.For(template, locale).Checks;
            Assert.NotNull(checks);
            foreach (var key in keys) Assert.True(checks.ContainsKey(key), $"{templateKey} has no {locale} words for the check '{key}'.");
        }
    }

    [Fact]
    public void A_watched_stock_that_fails_a_candidate_check_is_not_a_candidate()
    {
        RuleCheck[] lost = [new("gain", CheckGroup.Candidate, false), new("zone", CheckGroup.Buy, true)];
        RuleCheck[] waiting = [new("gain", CheckGroup.Candidate, true), new("drop", CheckGroup.Candidate, null), new("zone", CheckGroup.Buy, false)];

        Assert.Equal(Strategies.StockStatus.NotCandidate, Strategies.StrategyPage.WithCandidacy(Strategies.StockStatus.Watching, lost));
        Assert.Equal(Strategies.StockStatus.Watching, Strategies.StrategyPage.WithCandidacy(Strategies.StockStatus.Watching, waiting));
        Assert.Equal(Strategies.StockStatus.Watching, Strategies.StrategyPage.WithCandidacy(Strategies.StockStatus.Watching, []));
        Assert.Equal(Strategies.StockStatus.Building, Strategies.StrategyPage.WithCandidacy(Strategies.StockStatus.Building, lost));
    }
}
