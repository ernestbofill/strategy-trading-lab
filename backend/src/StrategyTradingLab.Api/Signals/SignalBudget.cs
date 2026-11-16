using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Signals;

/// <summary>Which signals use their step of the budget: done ones and those still waiting (a skipped or missed one uses none).</summary>
public static class SignalBudget
{
    /// <summary>In a database query. Keep it the same as <see cref="UsesBudget"/>, which EF can't translate.</summary>
    public static IQueryable<Signal> UsingBudget(this IQueryable<Signal> signals) =>
        signals.Where(s => s.Status == SignalStatus.Confirmed || s.Status == SignalStatus.Waiting);

    /// <summary>For signals already loaded.</summary>
    public static bool UsesBudget(this SignalStatus status) => status is SignalStatus.Confirmed or SignalStatus.Waiting;
}
