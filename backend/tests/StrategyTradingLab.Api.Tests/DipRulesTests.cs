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
        Assert.Equal(252, new ComebackRules(bars, new ComebackParams()).MinHistory);
        Assert.Equal(300, new ComebackRules(bars, new ComebackParams { LowDays = 300 }).MinHistory);
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
    public void A_fall_after_the_peak_is_no_proven_growth()
    {
        static IReadOnlyList<PriceBar> Bars(decimal first, decimal then) =>
            [.. Enumerable.Range(0, 240).Select(i => (Day: new DateOnly(2020, 1, 1).AddDays(i), Close: i < 230 ? first : then))
                .Select(x => new PriceBar(x.Day, x.Close, x.Close, x.Close, x.Close, 0))];
        static RuleCheck Gain(IReadOnlyList<PriceBar> bars, DipParams p) =>
            Assert.Single(new DipRules(bars, p).Decide(239, new DayContext(bars[239].Date, null, 0, default, false, false, false)).Checks, c => c.Key == "gain");
        var halved = Bars(200, 100);

        // The peak (200) came before the low (100): the low up to the peak's day is the peak itself, no rise.
        var rule = new DipParams { LowBeforePeakFrom = DateOnly.MinValue };
        var gain = Gain(halved, rule);
        Assert.False(gain.Met);
        Assert.Equal(0m, gain.Value);
        Assert.Equal(200m, gain.Level);
        // A real rise, the low first, still counts in full.
        Assert.Equal(100m, Gain(Bars(100, 200), rule).Value);
        // A strategy older than the rule replays its earlier days as the POC did: the window's low, whatever the order.
        Assert.True(Gain(halved, new DipParams { LowBeforePeakFrom = halved[^1].Date.AddDays(1) }).Met);
        // So do params stored without the date, while a new strategy takes the template's, with the rule on.
        Assert.True(Gain(halved, new DipParams()).Met);
        Assert.False(Gain(halved, (DipParams)TemplateCatalog.Dip.Params).Met);
    }

    [Fact]
    public void A_dip_under_the_SMA_150_still_stacks_with_two_averages()
    {
        // A long climb, then 45 days down: the SMA 50 (336) is under the SMA 150 (342) but still over the SMA 200 (324).
        IReadOnlyList<PriceBar> bars = [.. Enumerable.Range(0, 345).Select(i => (Day: new DateOnly(2020, 1, 1).AddDays(i), Close: i < 300 ? 100m + i : 399m - 3 * (i - 299)))
            .Select(x => new PriceBar(x.Day, x.Close, x.Close, x.Close, x.Close, 0))];
        RuleCheck Stack(bool pocStack) =>
            Assert.Single(new DipRules(bars, new DipParams(), pocStack).Decide(344, new DayContext(bars[344].Date, null, 0, default, false, false, false)).Checks, c => c.Key == "stacked");

        // STL's stack: two averages, met, with no middle level to show.
        var two = Stack(pocStack: false);
        Assert.True(two.Met);
        Assert.Null(two.MidLevel);
        // The POC's three: not met.
        Assert.False(Stack(pocStack: true).Met);
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

    [Fact]
    public void The_comeback_measures_the_turn_from_the_recent_low_window()
    {
        // A fast fall from 200 to 100 leaves the SMA 50 above 170, while the 50-day low is 100 (buy line 110).
        PriceBar Bar(int i, decimal close, long volume = 100) =>
            new(new DateOnly(2020, 1, 1).AddDays(i), close, close, close, close, volume);
        // Then it rises from 105 to 112 on heavy volume; `falling` reaches 112 from 116 (inside the window) instead, on the same volume.
        List<PriceBar> Bars(decimal before) =>
        [
            .. Enumerable.Range(0, 250).Select(i => Bar(i, 200)),
            .. Enumerable.Range(250, 5).Select(i => Bar(i, 100)),
            .. Enumerable.Range(255, 10).Select(i => Bar(i, before)),
            Bar(265, 112, volume: 300),
        ];
        var bars = Bars(105);
        var falling = Bars(116);

        var fiftyDay = RulesRunner.Run(new ComebackRules(bars, new ComebackParams()), bars, new StepPlan(5))[^1];
        var tenDay = RulesRunner.Run(new ComebackRules(bars, new ComebackParams { LowDays = 10 }), bars, new StepPlan(5))[^1];

        Assert.Equal(RuleStatus.BuySignal, fiftyDay.Status);
        var buyLine = Assert.Single(fiftyDay.Checks, c => c.Key == "buyLine");
        Assert.Equal(12m, buyLine.Value);
        Assert.Equal(10m, buyLine.Min);
        Assert.Equal(18m, buyLine.Max);
        Assert.Equal(110m, buyLine.Level);
        Assert.Equal(100m, buyLine.MidLevel);
        Assert.Equal(118m, buyLine.EndLevel);
        Assert.Equal(50m, buyLine.SubValue);
        Assert.Equal(8m, buyLine.EndValue);
        Assert.Equal(bars[254].Date, buyLine.Date); // The recent low's day: the last of its closes at 100.
        Assert.Equal(RuleStatus.Watching, tenDay.Status);

        // A close between the new limit (+18% above the low) and the POC's (+18.8%, 8% above the buy line) is too late now.
        List<PriceBar> jumped = [.. bars[..^1], Bar(265, 118.5m, volume: 300)];
        Assert.NotEqual(RuleStatus.BuySignal, RulesRunner.Run(new ComebackRules(jumped, new ComebackParams()), jumped, new StepPlan(5))[^1].Status);
        Assert.Equal(RuleStatus.BuySignal, RulesRunner.Run(new ComebackRules(jumped, new ComebackParams(), pocTurn: true), jumped, new StepPlan(5))[^1].Status);

        // Heavy volume on a falling price is no turn: the signal needs a close above the last one. The POC's rule has none.
        var fell = RulesRunner.Run(new ComebackRules(falling, new ComebackParams()), falling, new StepPlan(5))[^1];
        Assert.NotEqual(RuleStatus.BuySignal, fell.Status);
        var volume = Assert.Single(fell.Checks, c => c.Key == "volume");
        Assert.False(volume.Met);
        Assert.True(volume.Value >= volume.Min);
        Assert.Equal(-3.4m, volume.SubValue);
        Assert.Equal(0.99m, volume.Progress);
        Assert.Equal(RuleStatus.BuySignal, RulesRunner.Run(new ComebackRules(falling, new ComebackParams(), pocTurn: true), falling, new StepPlan(5))[^1].Status);
    }

    [Fact]
    public void The_comeback_needs_no_volume_back_from_above_the_chase_limit()
    {
        // From 200 to a low of 100 (buy line 110, chase limit 118), then 105 for 10 days at a volume of 100.
        PriceBar Bar(int i, decimal close, long volume = 100) =>
            new(new DateOnly(2020, 1, 1).AddDays(i), close, close, close, close, volume);
        List<PriceBar> Bars(params (decimal Close, long Volume)[] then) =>
        [
            .. Enumerable.Range(0, 250).Select(i => Bar(i, 200)),
            .. Enumerable.Range(250, 5).Select(i => Bar(i, 100)),
            .. Enumerable.Range(255, 10).Select(i => Bar(i, 105)),
            .. then.Select((t, k) => Bar(265 + k, t.Close, t.Volume)),
        ];
        static DayEvaluation Last(List<PriceBar> bars) => RulesRunner.Run(new ComebackRules(bars, new ComebackParams()), bars, new StepPlan(5))[^1];

        // Past the chase limit on normal volume, back into the window: waits for an up day, then signals without volume.
        var pulledBack = Bars((125, 100), (115, 100), (114, 100));
        var waiting = Last(pulledBack);
        Assert.Equal(RuleStatus.Watching, waiting.Status);
        Assert.DoesNotContain(waiting.Checks, c => c.Key == "volume");
        Assert.False(Assert.Single(waiting.Checks, c => c.Key == "pullback").Met);
        List<PriceBar> upDay = [.. pulledBack, Bar(268, 116)];
        var signal = Last(upDay);
        Assert.Equal(RuleStatus.BuySignal, signal.Status);
        var pullback = Assert.Single(signal.Checks, c => c.Key == "pullback");
        Assert.True(pullback.Met);
        Assert.Equal(118m, pullback.Level);
        // Coming up from below, the same day still needs the volume.
        List<PriceBar> fromBelow = [.. Bars((112, 100), (115, 100), (114, 100)), Bar(268, 116)];
        Assert.Equal(RuleStatus.Watching, Last(fromBelow).Status);
    }
}
