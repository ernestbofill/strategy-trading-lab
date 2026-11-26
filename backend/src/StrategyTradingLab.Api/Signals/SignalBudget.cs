using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Signals;

/// <summary>
/// The monthly limit (ROADMAP item 57): how much of the budget a strategy's signals may have used by a day. Unspent limit carries over
/// in full, so by the end of month m (counted from the start's month, which is month 1) it is m × the limit, never more than the
/// budget. The backtest's simulator uses the same rule.
/// </summary>
public static class MonthlyLimit
{
    public static decimal Room(decimal amount, decimal? limit, DateOnly start, DateOnly day) =>
        limit is not { } perMonth ? amount : Math.Min(amount, Math.Max(1, (day.Year - start.Year) * 12 + day.Month - start.Month + 1) * perMonth);

    /// <summary>A strategy's budget: what its signals may have used by <paramref name="day"/>.</summary>
    public static decimal Room(Budget budget, DateOnly start, DateOnly day) => Room(budget.Amount, budget.MonthlyLimit, start, day);

    /// <summary>
    /// The month's room, not the budget, holds the next step back on <paramref name="day"/>: with <paramref name="used"/> of the budget
    /// used by the signals, one more step fits the budget but not the room. An AI round then says so.
    /// </summary>
    public static bool HoldsNextStep(Budget budget, DateOnly start, DateOnly day, decimal used) =>
        budget is { MonthlyLimit: not null, Step: { } step } && used + step <= budget.Amount && used + step > Room(budget, start, day);
}

/// <summary>Which signals use their step of the budget: done ones and those still waiting (a skipped or missed one uses none).</summary>
public static class SignalBudget
{
    /// <summary>In a database query. Keep it the same as <see cref="UsesBudget"/>, which EF can't translate.</summary>
    public static IQueryable<Signal> UsingBudget(this IQueryable<Signal> signals) =>
        signals.Where(s => s.Status == SignalStatus.Confirmed || s.Status == SignalStatus.Waiting);

    /// <summary>For signals already loaded.</summary>
    public static bool UsesBudget(this SignalStatus status) => status is SignalStatus.Confirmed or SignalStatus.Waiting;

    /// <summary>What loaded signals use of the budget: their steps (as <c>StrategyEvaluator.BudgetHistoryAsync</c>'s <c>Used</c>).</summary>
    public static decimal BudgetUsed(this IEnumerable<Signal> signals) => signals.Where(s => s.Status.UsesBudget()).Sum(s => s.StepAmount ?? 0);
}
