using StrategyTradingLab.Api.Engine;

namespace StrategyTradingLab.Api.Strategies;

/// <summary>
/// The stock page's chart (spec: Stock page): each day's close with the rules' checks of that day, so the chart draws the
/// levels as they moved. From the strategy's first check on, the rules replayed from that day as the daily check does, on
/// today's closes (a split re-adjusts the history, so stored levels would no longer match them); before it, only what makes
/// a candidate, since buys don't apply before the start.
/// </summary>
public static class StockChart
{
    /// <summary>How far back the chart goes, in calendar days (its longest zoom).</summary>
    public const int Days = 90;

    /// <summary>Each day's checks from <paramref name="from"/> on. <paramref name="newRules"/> builds fresh rules: they keep state, so each run needs its own.</summary>
    public static Dictionary<DateOnly, IReadOnlyList<RuleCheck>> Checks(Func<IBuyRules> newRules, IReadOnlyList<PriceBar> bars, DateOnly from,
        DateOnly? startedOn, int maxSteps)
    {
        var checks = startedOn is null || startedOn > from ? WatchedChecks(newRules(), bars, from, startedOn, maxSteps) : [];
        var start = startedOn is { } day ? bars.ToList().FindIndex(b => b.Date >= day) : -1;
        if (start >= 0)
            foreach (var evaluation in RulesRunner.Run(newRules(), bars, new StepPlan(maxSteps), start).Where(e => e.Date >= from))
                checks[evaluation.Date] = evaluation.Checks;
        return checks;
    }

    /// <summary>
    /// Each day's candidate checks from <paramref name="from"/> until the day before <paramref name="before"/>, the rules run
    /// over the whole history with no position: as a stock the rules only watch.
    /// </summary>
    public static Dictionary<DateOnly, IReadOnlyList<RuleCheck>> WatchedChecks(IBuyRules rules, IReadOnlyList<PriceBar> bars, DateOnly from, DateOnly? before,
        int maxSteps)
    {
        var result = new Dictionary<DateOnly, IReadOnlyList<RuleCheck>>();
        RuleStatus? previous = null;
        for (var i = 0; i < bars.Count && (before is not { } end || bars[i].Date < end); i++)
        {
            if (i + 1 < rules.MinHistory)
            {
                previous = RuleStatus.NotEnoughData;
                continue;
            }
            var decision = rules.Decide(i, new DayContext(bars[i].Date, previous, 0, default, ActedNow: false, Cooling: false, Full: false, maxSteps));
            previous = decision.Status;
            if (bars[i].Date >= from) result[bars[i].Date] = [.. decision.Checks.Where(c => c.Group == CheckGroup.Candidate)];
        }
        return result;
    }
}
