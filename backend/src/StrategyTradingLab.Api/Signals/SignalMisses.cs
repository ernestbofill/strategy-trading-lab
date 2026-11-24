using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Signals;

public static class SignalMisses
{
    /// <summary>
    /// The signals among <paramref name="signals"/> that can still take the user's trade: a waiting one, or a missed one whose trade
    /// wasn't recorded in time, until a newer signal of its side for the stock stands for that trade instead (Ernest, 2026-11-24): a
    /// skipped or missed trade can be signalled again, and recording it on both would count it twice.
    /// </summary>
    public static IQueryable<Signal> TakesItsTrade(this IQueryable<Signal> signals, AppDbContext db) =>
        signals.Where(s => s.Status == SignalStatus.Waiting || (s.Status == SignalStatus.Missed
            && !db.Signals.Any(n => n.StrategyId == s.StrategyId && n.AssetId == s.AssetId && n.Side == s.Side && n.SignalDate > s.SignalDate)));

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
