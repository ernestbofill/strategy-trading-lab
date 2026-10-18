using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Data;

/// <summary>Sets updated_at on every added or changed row of a mutable table.</summary>
public class UpdatedAtInterceptor(TimeProvider clock) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null) Stamp(eventData.Context.ChangeTracker, clock.GetUtcNow());
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null) Stamp(eventData.Context.ChangeTracker, clock.GetUtcNow());
        return ValueTask.FromResult(result);
    }

    public static void Stamp(ChangeTracker changeTracker, DateTimeOffset now)
    {
        foreach (var entry in changeTracker.Entries<IHasUpdatedAt>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified) entry.Entity.UpdatedAt = now;
        }
    }
}
