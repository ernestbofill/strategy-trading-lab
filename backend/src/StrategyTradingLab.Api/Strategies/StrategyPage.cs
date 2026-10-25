using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Strategies;

/// <summary>The strategy page's rules for each stock's pill (spec: Strategy page).</summary>
public static class StrategyPage
{
    /// <summary>
    /// A live signal waiting for an answer comes first; then a simulation's signal of the latest evaluated day. A stock
    /// with every step bought, or that the rules hold, is <i>Holding</i> (<i>Done</i> for <i>The big market drop</i>),
    /// one with some is <i>Building</i>, and an all-in strategy outside the market is <i>OutOfTheMarket</i>. The others
    /// say where the rules stand: met with no budget left for the step, watching it, not a candidate, not enough history,
    /// or not checked yet.
    /// </summary>
    /// <param name="rulesSteps">The steps the rules currently hold: every buy signal, answered or not, since the rules carry on as if it was bought.</param>
    /// <param name="todaysSignal">A signal on the day of <paramref name="latest"/>.</param>
    public static StockStatus Status(StrategyMode mode, EvaluationStatus? latest, bool waiting, bool todaysSignal, int rulesSteps, int? maxSteps,
        string? templateKey = null)
    {
        if (mode == StrategyMode.Live && waiting) return StockStatus.WaitingForYou;
        if (mode == StrategyMode.Simulation && latest is EvaluationStatus.BuySignal or EvaluationStatus.SellSignal && todaysSignal) return StockStatus.Signal;
        if (templateKey == "bear")
        {
            if (rulesSteps > 0 && (latest == EvaluationStatus.Holding || (maxSteps is { } max && rulesSteps >= max))) return StockStatus.Done;
            if (latest == EvaluationStatus.NotCandidate) return StockStatus.NotCandidate;
            if (latest == EvaluationStatus.Watching) return StockStatus.Watching;
        }
        if (maxSteps is null || templateKey == "trend")
        {
            if (rulesSteps > 0) return StockStatus.Holding;
            if (latest is EvaluationStatus.NotCandidate or EvaluationStatus.Watching or EvaluationStatus.SignalEnded or EvaluationStatus.SellSignal)
                return StockStatus.OutOfTheMarket;
        }
        if (rulesSteps > 0)
            return latest == EvaluationStatus.Holding || (maxSteps is { } m && rulesSteps >= m) ? StockStatus.Holding : StockStatus.Building;
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

    /// <summary>The pill's order on the page: what waits for the user first, then what the rules are doing.</summary>
    public static int Order(StockStatus status) => status switch
    {
        StockStatus.WaitingForYou => 0,
        StockStatus.Signal => 1,
        StockStatus.Building => 2,
        StockStatus.Holding or StockStatus.Done => 3,
        StockStatus.NoBudget or StockStatus.Watching or StockStatus.OutOfTheMarket => 4,
        _ => 5,
    };
}
