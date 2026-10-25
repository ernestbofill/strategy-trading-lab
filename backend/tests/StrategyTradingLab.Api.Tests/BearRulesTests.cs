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
}
