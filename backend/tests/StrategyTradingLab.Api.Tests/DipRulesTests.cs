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

        var day = rules.At(399);

        Assert.True(day.Candidate);
    }

    [Fact]
    public void Equal_floor_and_trend_days_never_stack_instead_of_failing()
    {
        var bars = Rising(400);
        var rules = new DipRules(bars, new DipParams { TrendDays = 100, FloorDays = 100, MinGainPct = 10 });

        Assert.False(rules.At(399).Candidate);
    }
}
