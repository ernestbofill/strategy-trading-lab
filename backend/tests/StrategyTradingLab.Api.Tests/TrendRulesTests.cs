using StrategyTradingLab.Api.Engine;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Tests;

public class TrendRulesTests
{
    static IReadOnlyList<PriceBar> Rising(int days) =>
        [.. Enumerable.Range(0, days).Select(i => new PriceBar(new DateOnly(2020, 1, 1).AddDays(i), 100 + i, 100 + i, 100 + i, 100 + i, 0))];

    [Fact]
    public void It_asks_for_the_history_its_longest_window_needs()
    {
        var bars = Rising(10);
        Assert.Equal(200, new TrendRules(bars, new TrendParams()).MinHistory);
        Assert.Equal(100, new TrendRules(bars, new TrendParams { SmaDays = 100 }).MinHistory);
    }

    [Fact]
    public void Rules_that_were_already_run_are_refused_a_second_time()
    {
        var bars = Rising(300);
        var trend = new TrendRules(bars, new TrendParams());
        var context = new DayContext(new DateOnly(2021, 1, 1), null, 0, default, false, false, false);
        trend.Decide(260, context);

        Assert.Throws<InvalidOperationException>(() => trend.Decide(260, context));
    }

    [Fact]
    public void Trend_signals_end_when_the_trend_turns_down_before_a_buy_and_sells_carry_reasons()
    {
        var bars = Enumerable.Range(0, 55).Select(i =>
        {
            var close = i switch
            {
                50 => 105m, // Above SMA 50 + 3%: uptrend.
                51 => 95m,  // Below SMA 50 - 3%: downtrend before buying.
                52 => 105m, // Back above +3%.
                53 => 98m,  // Inside the ±3% band after a previous breakdown.
                _ => 100m,
            };
            return new PriceBar(new DateOnly(2020, 1, 1).AddDays(i), close, close, close, close, 0);
        }).ToList();
        var rules = new TrendRules(bars, new TrendParams { SmaDays = 50, BandPct = 3 });

        var buy = rules.Decide(50, new DayContext(bars[50].Date, RuleStatus.Watching, 0, default, false, false, false, 1));
        Assert.Equal(RuleStatus.BuySignal, buy.Status);

        var ended = rules.Decide(51, new DayContext(bars[51].Date, RuleStatus.BuySignal, 0, default, false, false, false, 1));
        Assert.Equal(RuleStatus.SignalEnded, ended.Status);
        Assert.Equal("The trend turned down before buying.", ended.Reason);
    }
}
