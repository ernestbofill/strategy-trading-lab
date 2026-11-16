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
    public void Without_the_middle_average_a_dip_under_the_SMA_150_still_stacks()
    {
        // A long climb, then 45 days down: the SMA 50 (336) is under the SMA 150 (342) but still over the SMA 200 (324).
        IReadOnlyList<PriceBar> bars = [.. Enumerable.Range(0, 345).Select(i => (Day: new DateOnly(2020, 1, 1).AddDays(i), Close: i < 300 ? 100m + i : 399m - 3 * (i - 299)))
            .Select(x => new PriceBar(x.Day, x.Close, x.Close, x.Close, x.Close, 0))];
        RuleCheck Stack(DipParams p, string key) =>
            Assert.Single(new DipRules(bars, p).Decide(344, new DayContext(bars[344].Date, null, 0, default, false, false, false)).Checks, c => c.Key == key);

        // The POC's stack, and what params stored without the choice mean: three averages, not met.
        Assert.False(Stack(new DipParams(), "stacked").Met);
        // The template's: two averages, met, with no middle level to show.
        var two = Stack((DipParams)TemplateCatalog.Dip.Params, "stackedTwo");
        Assert.True(two.Met);
        Assert.Null(two.MidLevel);
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
        // Then it rises from 105 to 112 on heavy volume; `falling` reaches 112 from 120 instead, on the same volume.
        List<PriceBar> Bars(decimal before) =>
        [
            .. Enumerable.Range(0, 250).Select(i => Bar(i, 200)),
            .. Enumerable.Range(250, 5).Select(i => Bar(i, 100)),
            .. Enumerable.Range(255, 10).Select(i => Bar(i, before)),
            Bar(265, 112, volume: 300),
        ];
        var bars = Bars(105);
        var falling = Bars(120);

        var fiftyDay = RulesRunner.Run(new ComebackRules(bars, new ComebackParams()), bars, new StepPlan(5))[^1];
        var tenDay = RulesRunner.Run(new ComebackRules(bars, new ComebackParams { LowDays = 10 }), bars, new StepPlan(5))[^1];

        Assert.Equal(RuleStatus.BuySignal, fiftyDay.Status);
        var buyLine = Assert.Single(fiftyDay.Checks, c => c.Key == "buyLine");
        Assert.Equal(12m, buyLine.Value);
        Assert.Equal(10m, buyLine.Min);
        Assert.Equal(18.8m, buyLine.Max);
        Assert.Equal(110m, buyLine.Level);
        Assert.Equal(100m, buyLine.MidLevel);
        Assert.Equal(118.8m, buyLine.EndLevel);
        Assert.Equal(50m, buyLine.SubValue);
        Assert.Equal(8m, buyLine.EndValue);
        Assert.Equal(RuleStatus.Watching, tenDay.Status);

        // Heavy volume on a falling price is no turn: the volume only counts when the close is above the one 5 days before.
        var fell = RulesRunner.Run(new ComebackRules(falling, new ComebackParams()), falling, new StepPlan(5))[^1];
        Assert.NotEqual(RuleStatus.BuySignal, fell.Status);
        var volume = Assert.Single(fell.Checks, c => c.Key == "volume");
        Assert.False(volume.Met);
        Assert.True(volume.Value >= volume.Min);
        Assert.Equal(-6.7m, volume.SubValue);
        Assert.Equal(0.99m, volume.Progress);

        // A strategy older than the rule replays its earlier days as it did: the same day still signals.
        var before = new ComebackParams { VolumeOnRiseFrom = falling[^1].Date.AddDays(1) };
        Assert.Equal(RuleStatus.BuySignal, RulesRunner.Run(new ComebackRules(falling, before), falling, new StepPlan(5))[^1].Status);
    }
}
