using StrategyTradingLab.Api.Engine;

namespace StrategyTradingLab.Api.Strategies;

/// <summary>
/// The stock page's chart (spec: Stock page): each day's close with the rules' checks of that day, so the chart draws the
/// levels as they moved. From the strategy's first check on, the checks the daily check stored; before it, only what makes
/// a candidate, since buys don't apply before the start.
/// </summary>
public static class StockChart
{
    /// <summary>How far back the chart goes, in calendar days (its longest zoom).</summary>
    public const int Days = 90;

    /// <summary>
    /// Each day's candidate checks from <paramref name="from"/> until the day before <paramref name="before"/>, the rules run
    /// over the whole history with no position: as a stock the rules only watch.
    /// </summary>
    public static Dictionary<DateOnly, IReadOnlyList<RuleCheck>> WatchedChecks(IBuyRules rules, IReadOnlyList<PriceBar> bars, DateOnly from, DateOnly? before)
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
            var decision = rules.Decide(i, new DayContext(bars[i].Date, previous, 0, default, ActedNow: false, Cooling: false, Full: false));
            previous = decision.Status;
            if (bars[i].Date >= from) result[bars[i].Date] = [.. decision.Checks.Where(c => c.Group == CheckGroup.Candidate)];
        }
        return result;
    }
}
