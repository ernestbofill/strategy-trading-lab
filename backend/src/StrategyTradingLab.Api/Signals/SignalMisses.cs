using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Signals;

public static class SignalMisses
{
    /// <summary>
    /// The signals among <paramref name="signals"/> whose stock has moved on since they were missed: a check after the one that
    /// missed them, or a newer signal of their side. A late answer is for right after the miss (Ernest, 2026-11-24): after that a
    /// skipped or missed step can be signalled again, and answering the old signal could count it twice. The check that missed
    /// one stores its own last day, which can be after the day the signal ended (a run that caught up several days), so the
    /// stock has moved on once a second day from the end on is stored.
    /// </summary>
    public static IQueryable<Signal> MovedOn(this IQueryable<Signal> signals, AppDbContext db) => signals.Where(MovedOnRule(db));

    /// <summary>The signals among <paramref name="signals"/> that haven't moved on (<see cref="MovedOn"/>), for a query that joins them.</summary>
    public static IQueryable<Signal> NotMovedOn(this IQueryable<Signal> signals, AppDbContext db)
    {
        var rule = MovedOnRule(db);
        return signals.Where(Expression.Lambda<Func<Signal, bool>>(Expression.Not(rule.Body), rule.Parameters));
    }

    static Expression<Func<Signal, bool>> MovedOnRule(AppDbContext db) => s => s.Status == SignalStatus.Missed
        && (db.DailyEvaluations.Count(e => e.StrategyId == s.StrategyId && e.AssetId == s.AssetId && e.Date >= s.EndedOn) >= 2
            || db.Signals.Any(n => n.StrategyId == s.StrategyId && n.AssetId == s.AssetId && n.Side == s.Side && n.SignalDate > s.SignalDate));

    /// <summary>
    /// The waiting signals among <paramref name="signals"/> that can't be answered any more become missed, in the database (so
    /// an answer saved since they were read stands), and lose their snooze (only a waiting signal can be snoozed: the table's
    /// check). One not ended yet ends on <paramref name="endedOn"/>; one that ended already keeps its day. Returns how many.
    /// </summary>
    public static Task<int> MissWaitingAsync(this IQueryable<Signal> signals, DateOnly endedOn, CancellationToken cancellationToken) =>
        signals.Where(s => s.Status == SignalStatus.Waiting).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.Status, SignalStatus.Missed)
            .SetProperty(x => x.EndedOn, x => x.EndedOn ?? endedOn)
            .SetProperty(x => x.SnoozedUntil, (DateOnly?)null), cancellationToken);
}
