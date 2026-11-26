using System.Text.Json;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Engine;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Strategies;

/// <summary>The strategy page's rules for each stock's pill (spec: Strategy page).</summary>
public static class StrategyPage
{
    /// <summary>
    /// For Bear markets, the current drop's step count and when its first step signalled (none between drops). Each drop has its
    /// own steps, so signals and buys from an earlier drop don't count towards the next one.
    /// </summary>
    public static (int? DoneSteps, DateOnly? CycleStart) BearCycle(JsonElement? facts)
    {
        if (facts is not { } root || root.Int("doneSteps") is not { } done) return default;
        return (done, root.Date("cycleStart"));
    }
    /// <summary>
    /// A live signal waiting for an answer comes first; then a simulation's signal of the latest evaluated day. A stock
    /// with every step bought, or that the rules hold, is <i>Holding</i> (<i>Done</i> for <i>Bear markets</i>),
    /// one with some is <i>Building</i>, and an all-in strategy outside the market is <i>OutOfTheMarket</i>. The others
    /// say where the rules stand: met with no budget left for the step, watching it, not a candidate, not enough history,
    /// or not checked yet.
    /// </summary>
    /// <param name="steps">The steps held: a simulation's every buy signal; a Manual strategy's the user's buys (a step skipped or missed isn't held).</param>
    /// <param name="signalPending">A simulation's signal on the day of <paramref name="latest"/> whose trade isn't recorded yet.</param>
    /// <param name="unboughtSignal">Manual: a buy signal skipped or missed whose window is still open. The day reads as a buy signal,
    /// but the signal was made, so it isn't the budget that held the step back.</param>
    public static StockStatus Status(StrategyMode mode, EvaluationStatus? latest, bool waiting, bool signalPending, int steps, int? maxSteps,
        string? templateKey = null, bool unboughtSignal = false)
    {
        if (mode == StrategyMode.Live && waiting) return StockStatus.WaitingForYou;
        // Manual, the rules hold steps the user's trades don't (Bear markets counts its own steps, or a trade was deleted), or a
        // signal the user skipped still stands: the pill says where the user is.
        if (mode == StrategyMode.Live && steps == 0
            && (latest is EvaluationStatus.Building or EvaluationStatus.Holding || (latest == EvaluationStatus.BuySignal && unboughtSignal)))
            return maxSteps is null || templateKey == TemplateKeys.Trend ? StockStatus.OutOfTheMarket : StockStatus.Watching;
        if (mode == StrategyMode.Simulation && latest is EvaluationStatus.BuySignal or EvaluationStatus.SellSignal && signalPending) return StockStatus.Signal;
        if (templateKey == TemplateKeys.Bear)
        {
            if (steps > 0 && (latest == EvaluationStatus.Holding || (maxSteps is { } max && steps >= max))) return StockStatus.Done;
            if (latest == EvaluationStatus.NotCandidate) return StockStatus.NotCandidate;
            if (latest == EvaluationStatus.Watching) return StockStatus.Watching;
        }
        if (maxSteps is null || templateKey == TemplateKeys.Trend)
        {
            if (steps > 0) return StockStatus.Holding;
            if (latest is EvaluationStatus.NotCandidate or EvaluationStatus.Watching or EvaluationStatus.SignalEnded or EvaluationStatus.SellSignal)
                return StockStatus.OutOfTheMarket;
        }
        if (steps > 0)
            return latest == EvaluationStatus.Holding || (maxSteps is { } m && steps >= m) ? StockStatus.Holding : StockStatus.Building;
        return latest switch
        {
            // The rules call for a step, or hold steps, that the budget had no room for: no signal was made.
            EvaluationStatus.BuySignal or EvaluationStatus.Building or EvaluationStatus.Holding => StockStatus.NoBudget,
            null => StockStatus.NotChecked,
            EvaluationStatus.NotEnoughData => StockStatus.NotEnoughData,
            EvaluationStatus.NotCandidate => StockStatus.NotCandidate,
            _ => StockStatus.Watching,
        };
    }

    /// <summary>
    /// A stock the rules only watch is a candidate unless a candidate check failed: a signal that ended because the stock stopped
    /// being a candidate (the engine's <c>SignalEnded</c>) reads as not a candidate, as the stock page shows it.
    /// </summary>
    public static StockStatus WithCandidacy(StockStatus status, IReadOnlyList<RuleCheck> checks) =>
        status == StockStatus.Watching && checks.Any(c => c.Group == CheckGroup.Candidate && c.Met == false) ? StockStatus.NotCandidate : status;

    /// <summary>
    /// The candidates' order within their pill (feedback, 2026-11-17): in the buy range first (no distance left, not past the
    /// rule), then the closest to it, then those with no distance, and those past the buy rule (too late to chase) last.
    /// </summary>
    public static (int, decimal) CandidateOrder(StockStatus status, decimal? distance, IReadOnlyList<RuleCheck> checks) =>
        Order(status) != Order(StockStatus.Watching) ? (0, 0)
        : StockStates.PastBuyRule(checks) ? (3, 0)
        : distance is { } d ? (d == 0 ? 0 : 1, d)
        : (2, 0);

    /// <summary>The pill's order on the page: what waits for the user first, then what the rules are doing.</summary>
    public static int Order(StockStatus status) => status switch
    {
        StockStatus.WaitingForYou => 0,
        StockStatus.Signal => 1,
        StockStatus.Building => 2,
        StockStatus.Holding or StockStatus.Done => 3,
        StockStatus.NoBudget or StockStatus.MonthlyLimit or StockStatus.Watching or StockStatus.OutOfTheMarket => 4,
        StockStatus.Removed => 6,
        _ => 5,
    };
}
