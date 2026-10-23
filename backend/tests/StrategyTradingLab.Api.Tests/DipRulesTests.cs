using StrategyTradingLab.Api.Engine;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Tests;

/// <summary>How <see cref="DipRules"/> behaves when its params move away from the POC's fixed averages.</summary>
public class DipRulesTests
{
    static IReadOnlyList<PriceBar> Rising(int days) =>
        [.. Enumerable.Range(0, days).Select(i => new PriceBar(new DateOnly(2020, 1, 1).AddDays(i), 100 + i, 100 + i, 100 + i, 100 + i, 0))];

    [Fact]
    public void It_asks_for_the_history_its_longest_window_needs()
    {
        var bars = Rising(10);

        Assert.Equal(220, new DipRules(bars, new DipParams()).MinHistory);
        Assert.Equal(320, new DipRules(bars, new DipParams { TrendDays = 300 }).MinHistory); // 300 days, then 20 to see it rise.
        Assert.Equal(252, new DipRules(bars, new DipParams { GainDays = 252 }).MinHistory);
    }

    [Fact]
    public void A_shorter_trend_average_still_stacks_in_a_steady_climb()
    {
        // A steady climb has SMA 50 > middle > SMA 100 > ... whatever the trend length: the stack must be able to hold.
        var bars = Rising(400);
        var rules = new DipRules(bars, new DipParams { TrendDays = 100, MinGainPct = 10 });

        var day = rules.Decide(399, new DayContext(new DateOnly(2021, 2, 4), null, 0, default, false, false, false));

        Assert.Equal(RuleStatus.Watching, day.Status); // A candidate (not-candidate otherwise), waiting for a dip into the zone.
    }

    [Fact]
    public void Equal_floor_and_trend_days_never_stack_instead_of_failing()
    {
        var bars = Rising(400);
        var rules = new DipRules(bars, new DipParams { TrendDays = 100, FloorDays = 100, MinGainPct = 10 });

        Assert.Equal(RuleStatus.NotCandidate, rules.Decide(399, new DayContext(new DateOnly(2021, 2, 4), null, 0, default, false, false, false)).Status);
    }

    [Fact]
    public void Rules_that_were_already_run_are_refused_a_second_time()
    {
        var bars = Rising(300);
        var dip = new DipRules(bars, new DipParams());
        var comeback = new ComebackRules(bars, new ComebackParams());
        var context = new DayContext(new DateOnly(2021, 1, 1), null, 0, default, false, false, false);
        dip.Decide(260, context);
        comeback.Decide(260, context);

        Assert.Throws<InvalidOperationException>(() => dip.Decide(260, context));
        Assert.Throws<InvalidOperationException>(() => comeback.Decide(100, context));
    }

    [Fact]
    public void The_comeback_says_not_a_candidate_before_its_indicators_have_data()
    {
        var rules = new ComebackRules(Rising(300), new ComebackParams());

        var day = rules.Decide(10, new DayContext(new DateOnly(2020, 1, 11), null, 0, default, false, false, false));

        Assert.Equal(RuleStatus.NotCandidate, day.Status);
    }
}
