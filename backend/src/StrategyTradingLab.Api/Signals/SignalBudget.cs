using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Signals;

public static class SignalBudget
{
    /// <summary>The signals that use their step of the budget: done ones and those still waiting (a skipped or missed one uses none).</summary>
    public static IQueryable<Signal> UsingBudget(this IQueryable<Signal> signals) =>
        signals.Where(s => s.Status == SignalStatus.Confirmed || s.Status == SignalStatus.Waiting);
}
