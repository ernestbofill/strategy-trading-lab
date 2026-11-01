using StrategyTradingLab.Api.Engine;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Tests;

public class BearRulesTests
{
    static IReadOnlyList<PriceBar> Rising(int days) =>
        [.. Enumerable.Range(0, days).Select(i => new PriceBar(new DateOnly(2020, 1, 1).AddDays(i), 100 + i, 100 + i, 100 + i, 100 + i, 0))];

    [Fact]
    public void It_asks_for_the_history_its_longest_window_needs()
    {
        var bars = Rising(10);
        Assert.Equal(126, new BearRules(bars, new BearParams()).MinHistory);
        Assert.Equal(63, new BearRules(bars, new BearParams { HighDays = 63 }).MinHistory);
    }

    [Fact]
    public void Rules_that_were_already_run_are_refused_a_second_time()
    {
        var bars = Rising(300);
        var bear = new BearRules(bars, new BearParams());
        var context = new DayContext(new DateOnly(2021, 1, 1), null, 0, default, false, false, false);
        bear.Decide(260, context);

        Assert.Throws<InvalidOperationException>(() => bear.Decide(260, context));
    }

    [Fact]
    public void Each_cycle_records_when_its_first_step_signalled_and_clears_it_between_drops()
    {
        var closes = new List<decimal>();
        for (var i = 0; i < 126; i++) closes.Add(100m); // 126-day high at 100.
        closes.Add(75m); // Drop 1: 25% below 100 (Watching).
        for (var i = 0; i < 25; i++) closes.Add(80m); // +6.67% bounce: 3 steps (days 0, 10, 20), then Holding.
        closes.Add(100m); // Recovers old peak (100): back to NotCandidate.
        closes.Add(75m); // Drop 2: 25% below 100 (Watching).
        for (var i = 0; i < 15; i++) closes.Add(80m); // +6.67% bounce: new cycle starts on this day.

        var first = new DateOnly(2024, 1, 1);
        var bars = closes.Select((c, i) => new PriceBar(first.AddDays(i), c, c, c, c, 0)).ToList();
        var days = RulesRunner.Run(new BearRules(bars, new BearParams()), bars, new StepPlan(3));

        var drop1Watch = days[126];
        Assert.Equal(RuleStatus.Watching, drop1Watch.Status);
        Assert.Equal(0, drop1Watch.Facts["doneSteps"]);
        Assert.False(drop1Watch.Facts.ContainsKey("cycleStart"));

        var cycle1Start = days[127].Date.ToString("yyyy-MM-dd");
        for (var i = 127; i < 152; i++)
        {
            Assert.True((int)days[i].Facts["doneSteps"] >= 1);
            Assert.Equal(cycle1Start, days[i].Facts["cycleStart"]);
        }

        var recovered = days[152];
        Assert.Equal(RuleStatus.NotCandidate, recovered.Status);
        Assert.Equal(0, recovered.Facts["doneSteps"]);
        Assert.False(recovered.Facts.ContainsKey("cycleStart"));

        var drop2Watch = days[153];
        Assert.Equal(RuleStatus.Watching, drop2Watch.Status);
        Assert.Equal(0, drop2Watch.Facts["doneSteps"]);
        Assert.False(drop2Watch.Facts.ContainsKey("cycleStart"));

        var cycle2Start = days[154].Date.ToString("yyyy-MM-dd");
        Assert.NotEqual(cycle1Start, cycle2Start);
        for (var i = 154; i < days.Count; i++)
        {
            Assert.True((int)days[i].Facts["doneSteps"] >= 1);
            Assert.Equal(cycle2Start, days[i].Facts["cycleStart"]);
        }
    }
}
