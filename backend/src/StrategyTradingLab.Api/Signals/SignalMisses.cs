using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Signals;

public static class SignalMisses
{
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
