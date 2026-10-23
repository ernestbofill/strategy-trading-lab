using StrategyTradingLab.Api.Engine;

namespace StrategyTradingLab.Api.Tests;

public class EngineTests
{
    [Fact]
    public void An_average_is_null_until_its_window_is_full_and_then_trails()
    {
        var sma = Indicators.Sma([1, 2, 3, 4, 5], 3);

        Assert.Equal([null, null, 2m, 3m, 4m], sma);
    }

    [Fact]
    public void The_rolling_high_and_low_include_today_and_leave_out_older_days()
    {
        Assert.Equal([null, 5m, 4m, 4m], Indicators.RollingHigh([5, 1, 4, 3], 2));
        Assert.Equal([null, 1m, 1m, 3m], Indicators.RollingLow([5, 1, 4, 3], 2));
    }

    [Fact]
    public void Rising_compares_with_days_ago_and_is_false_without_both()
    {
        var rising = Indicators.Rising([null, 1m, 2m, 2m, 3m], 2);

        Assert.Equal([false, false, false, true, true], rising); // Day 2: 2 against a missing value is false.
    }

    /// <summary>Rules whose window is open on the listed days; always a candidate.</summary>
    sealed class OpenOn(params int[] days) : IBuyRules
    {
        public int MinHistory => 1;
        public int Length { get; init; }
        public int CooldownDays { get; init; } = 3;
        public RuleDay At(int i) => new(true, days.Contains(i), false, "lost", "ran", new Dictionary<string, decimal>());
    }

    static IReadOnlyList<PriceBar> Bars(int count) =>
        [.. Enumerable.Range(0, count).Select(i => new PriceBar(new DateOnly(2026, 1, 1).AddDays(i), 10, 10, 10, 10, 0))];

    static IEnumerable<int> NewBuyDays(IReadOnlyList<DayEvaluation> days) => days.Select((d, i) => (d, i)).Where(x => x.d.NewBuy).Select(x => x.i);

    [Fact]
    public void A_signal_is_acted_on_the_next_day_and_the_cooldown_holds_the_next_one_back()
    {
        // Open on days 1-2 and 4-9, cooldown 3 days from the fill: the fill is day 2 (cooldown to day 5), so day 4 is still cooling.
        var days = RulesRunner.Run(new OpenOn(1, 2, 4, 5, 6, 7, 8, 9) { Length = 12 }, Bars(12), new StepPlan(3));

        Assert.Equal(RuleStatus.Watching, days[0].Status);
        Assert.True(days[1].NewBuy);
        Assert.Equal(1, days[1].StepNo);
        Assert.Equal(RuleStatus.Building, days[2].Status); // Bought at day 2's open; the same window gives no second step.
        Assert.Equal(RuleStatus.Building, days[4].Status); // Day 4: still in the cooldown (until day 5).
        Assert.Equal(RuleStatus.BuySignal, days[5].Status); // The window is still open and the cooldown is over.
        Assert.Equal(2, days[5].StepNo);
    }

    [Fact]
    public void The_last_step_makes_it_holding_and_no_more_signals_follow()
    {
        // Two windows (days 1-2 and 5-6), no cooldown, two steps: one step per window, then holding.
        var days = RulesRunner.Run(new OpenOn(1, 2, 5, 6, 9, 10) { CooldownDays = 0, Length = 14 }, Bars(14), new StepPlan(2));

        Assert.Equal([1, 5], NewBuyDays(days));
        Assert.Equal(RuleStatus.Holding, days[^1].Status);
    }

    [Fact]
    public void An_open_window_gives_one_step_however_long_it_stays_open()
    {
        var days = RulesRunner.Run(new OpenOn([.. Enumerable.Range(1, 20)]) { CooldownDays = 0, Length = 25 }, Bars(25), new StepPlan(5));

        Assert.Equal(1, days.Count(d => d.NewBuy));
    }

    [Fact]
    public void Nothing_is_said_before_enough_history()
    {
        var days = RulesRunner.Run(new Late(new OpenOn(0, 1, 2, 3) { Length = 6 }), Bars(6), new StepPlan(3));

        Assert.All(days.Take(3), d => Assert.Equal(RuleStatus.NotEnoughData, d.Status));
    }

    sealed class Late(IBuyRules inner) : IBuyRules
    {
        public int MinHistory => 4;
        public int Length => inner.Length;
        public int CooldownDays => inner.CooldownDays;
        public RuleDay At(int i) => inner.At(i);
    }

    [Fact]
    public void Rules_prepared_over_another_history_are_refused()
    {
        Assert.Throws<ArgumentException>(() => RulesRunner.Run(new OpenOn(1) { Length = 5 }, Bars(6), new StepPlan(1)));
    }

    [Fact]
    public void The_rolling_extremes_match_a_plain_scan_on_noisy_data()
    {
        var random = new Random(7);
        decimal[] values = [.. Enumerable.Range(0, 400).Select(_ => (decimal)random.Next(1, 60))];

        foreach (var days in new[] { 1, 2, 5, 60, 168, 400 })
        {
            var high = Indicators.RollingHigh(values, days);
            var low = Indicators.RollingLow(values, days);
            for (var i = 0; i < values.Length; i++)
            {
                if (i < days - 1) { Assert.Null(high[i]); continue; }
                var window = values.Skip(i - days + 1).Take(days).ToList();
                Assert.Equal(window.Max(), high[i]);
                Assert.Equal(window.Min(), low[i]);
            }
        }
    }
}
