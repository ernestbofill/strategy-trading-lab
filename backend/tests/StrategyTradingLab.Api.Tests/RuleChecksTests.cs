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

    static IEnumerable<RuleCheck> Group(DayEvaluation day, string group) => day.Checks.Where(c => c.Group == group && c.Preview != true);

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
        // Held: the turn, step 1, comes first, bought on its day; a milestone that bought a step says so, with its day.
        foreach (var day in days.Where(d => Group(d, CheckGroup.Next).Any()))
        {
            var turn = Group(day, CheckGroup.Next).First();
            Holds(turn.Key == "turn" && turn.Bought == true && turn.Date is not null, day, "the turn is the first next-step row, bought");
            Holds(Group(day, CheckGroup.Next).All(c => c.Bought != true || (c.Used == true && c.Date is not null)), day, "a bought row is used, with its day");
            // Used without buying says so (false): null is how checks stored before the field read.
            Holds(Group(day, CheckGroup.Next).All(c => c.Used != true || c.Bought is not null), day, "a used row says whether it bought");
        }
        Assert.Contains(days, d => Group(d, CheckGroup.Next).Any(c => c.Key != "turn" && c.Bought == true));
        Assert.Contains(days, d => d.Status == RuleStatus.BuySignal);
    }

    [Fact]
    public void Once_the_first_step_is_bought_the_checks_show_the_next_steps_and_the_cooldown()
    {
        var all = Bars("comeback-NOW-2400");
        var turn = RulesRunner.Run(new ComebackRules(all, new ComebackParams()), all, new StepPlan(5))
            .First(d => d.NewBuy && !Group(d, CheckGroup.Next).Any(c => c.Preview != true));
        var bars = all.TakeWhile(b => b.Date <= turn.Date).ToList();
        var buyDay = turn.Date.AddDays(1);

        var after = RulesRunner.Run(new ComebackRules(bars, new ComebackParams()), bars, new StepPlan(5), 0, boughtBeforeLast: buyDay)[^1].Checks;

        // The milestones count from the buy, no longer in preview, and the cooldown holds the next step back, all of it to wait.
        Assert.DoesNotContain(after, c => c.Group is CheckGroup.Candidate or CheckGroup.Buy);
        Assert.All(after.Where(c => c.Group == CheckGroup.Next), c => Assert.Null(c.Preview));
        Assert.All(after.Where(c => c.Group == CheckGroup.Next && c.Met == true && c.Key != "cooldown"), c => Assert.True(c.Used));
        var cooldown = Assert.Single(after, c => c.Key == "cooldown");
        Assert.Equal(CheckGroup.Next, cooldown.Group);
        Assert.Equal((decimal)new ComebackParams().CooldownDays, cooldown.Value);
        Assert.Equal(buyDay, Assert.Single(after, c => c.Key == "turn").Date);

        // A later step bought on its signal's day: its milestone reads bought on that day, not just used.
        var later = RulesRunner.Run(new ComebackRules(all, new ComebackParams()), all, new StepPlan(5))
            .First(d => d.NewBuy && Group(d, CheckGroup.Next).Any());
        var upTo = all.TakeWhile(b => b.Date <= later.Date).ToList();
        var laterBuy = later.Date.AddDays(1);
        var afterLater = RulesRunner.Run(new ComebackRules(upTo, new ComebackParams()), upTo, new StepPlan(5), 0, boughtBeforeLast: laterBuy)[^1].Checks;
        Assert.Contains(afterLater, c => c.Key != "turn" && c.Bought == true && c.Date == laterBuy);
    }

    [Fact]
    public void Before_the_start_the_chart_gets_the_candidate_checks_of_a_stock_never_bought()
    {
        var bars = Bars("comeback-SMCI-800");
        var from = bars[300].Date;
        var start = bars[^20].Date;
        var watched = StrategyTradingLab.Api.Strategies.StockChart.WatchedChecks(new ComebackRules(bars, new ComebackParams()), bars, from, start, 5);

        Assert.Equal(bars.Count(b => b.Date >= from && b.Date < start), watched.Count);
        // A run that buys swaps the candidate checks for milestones; a stock never bought keeps them every day.
        Assert.All(watched.Values, checks => Assert.Contains(checks, c => c.Key == "beaten"));
        // With the next-step previews, whose averages the chart can draw back in time.
        Assert.All(watched.Values.SelectMany(c => c), c => Assert.True(c.Group == CheckGroup.Candidate || c.Preview == true));
        Assert.All(watched.Values, checks => Assert.Contains(checks, c => c.Key == "golden" && c.Level is not null && c.MidLevel is not null));
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
            if (day.Status == RuleStatus.BuySignal)
            {
                var check = day.Checks.Single();
                Holds(check.Key == "buyLine" && check.Met == true, day, "a buy is above the buy line, with no cooldown");
                Holds(check.Progress > 0, day, "a buy has progress toward the buy line");
            }
            if (day.Status == RuleStatus.Watching)
            {
                // After a sale the cooldown can show too, until it ends.
                var check = day.Checks.Single(c => c.Key != "cooldown");
                Holds(check.Key == "buyLine" && check.Met == false, day, "watching is below the buy line");
                Holds(check.Progress is > 0 and <= 1, day, "watching has progress toward the buy line");
            }
            if (day.Status == RuleStatus.SellSignal) Holds(day.Checks.Single().Key == "sellLine" && day.Checks.Single().Met == true, day, "a sale is below the sell line");
            if (day.Status == RuleStatus.Holding) Holds(day.Checks.Single().Met == false, day, "holding is above the sell line");
        }
        Assert.Contains(days, d => d.Checks.Any(c => c.Key == "cooldown" && c.Value is > 0 && c.Group == CheckGroup.Buy));
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

    [Theory]
    [InlineData("dip", "dip-NOW")]
    [InlineData("comeback", "comeback-NOW-2400")]
    [InlineData("bear", "bear-SPY-5600")]
    [InlineData("trend", "trend-SPY-5600")]
    public void Every_check_has_a_boolean_verdict_and_candidates_report_candidate_since(string templateKey, string name)
    {
        var bars = Bars(name);
        IBuyRules rules = templateKey switch
        {
            "dip" => new DipRules(bars, new DipParams()),
            "comeback" => new ComebackRules(bars, new ComebackParams()),
            "bear" => new BearRules(bars, new BearParams()),
            _ => new TrendRules(bars, new TrendParams()),
        };
        var days = RulesRunner.Run(rules, bars, new StepPlan(templateKey == "trend" ? 1 : 5)).Where(d => d.Checks.Count > 0).ToList();

        Assert.All(days.SelectMany(d => d.Checks), c => Assert.NotNull(c.Met));
        if (templateKey != "trend")
        {
            var candidateDays = days.Where(d => Group(d, CheckGroup.Candidate).Any() && Group(d, CheckGroup.Candidate).All(c => c.Met == true)).ToList();
            Assert.NotEmpty(candidateDays);
            Assert.All(candidateDays, d => Assert.True(d.Facts?.ContainsKey("candidateSince") == true));
        }
        if (templateKey is "comeback" or "bear")
        {
            var prePosition = days.First(d => !Group(d, CheckGroup.Next).Any());
            Assert.Contains(prePosition.Checks, c => c.Group == CheckGroup.Next && c.Preview == true);
        }
    }
}
