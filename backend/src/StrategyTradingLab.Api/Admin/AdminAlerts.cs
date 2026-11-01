using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Admin;

/// <summary>A problem to raise: its key (one row per problem), what it says, and the strategy it is about, if any.</summary>
public record NewAlert(string Key, string Title, string? Detail = null, Guid? StrategyId = null);

/// <summary>An alert on the admin Alerts page.</summary>
public record AdminAlertDto(Guid Id, AlertSource Source, AlertStatus Status, string Title, string? Detail, int Count,
    DateTimeOffset FirstSeenAt, DateTimeOffset LastSeenAt, DateTimeOffset? ClosedAt);

/// <summary>
/// The owner's alerts (spec: Admin alerts): what the background jobs raise instead of emailing the owner. A job raises a problem
/// each time it sees it and clears it once a run finds it gone. Writing an alert never fails the job: a database error is logged.
/// </summary>
public class AdminAlerts(AppDbContext db, TimeProvider time, ILogger<AdminAlerts> logger)
{
    public const int KeyMax = 200;
    public const int TitleMax = 200;
    public const int DetailMax = 2000;

    /// <summary>How long a cleared alert stays on the page.</summary>
    public static readonly TimeSpan ShownAfterCleared = TimeSpan.FromDays(7);

    /// <summary>How long a cleared alert is kept, after which the daily run deletes it.</summary>
    public static readonly TimeSpan KeptAfterCleared = TimeSpan.FromDays(30);

    /// <summary>
    /// Raises a problem: a new alert, one seen once more (counted), or a cleared one that came back (open again, counting from 1).
    /// A dismissed alert stays dismissed while its problem lasts.
    /// </summary>
    public Task RaiseAsync(AlertSource source, NewAlert alert, CancellationToken cancellationToken = default) =>
        Guarded("raise", alert.Key, async () =>
        {
            var now = time.GetUtcNow();
            var key = Cut(alert.Key, KeyMax);
            var title = Cut(alert.Title, TitleMax);
            var detail = alert.Detail is null ? null : Cut(alert.Detail, DetailMax);
            var stored = Stored(source);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO admin_alerts (id, key, source, status, title, detail, strategy_id, count, first_seen_at, last_seen_at)
                VALUES ({Guid.CreateVersion7()}, {key}, {stored}, 'open', {title}, {detail}, {alert.StrategyId}, 1, {now}, {now})
                ON CONFLICT (key) DO UPDATE SET
                    source = excluded.source, title = excluded.title, detail = excluded.detail, strategy_id = excluded.strategy_id,
                    last_seen_at = excluded.last_seen_at,
                    count = CASE WHEN admin_alerts.status = 'cleared' THEN 1 ELSE admin_alerts.count + 1 END,
                    first_seen_at = CASE WHEN admin_alerts.status = 'cleared' THEN excluded.first_seen_at ELSE admin_alerts.first_seen_at END,
                    closed_at = CASE WHEN admin_alerts.status = 'cleared' THEN NULL ELSE admin_alerts.closed_at END,
                    status = CASE WHEN admin_alerts.status = 'cleared' THEN 'open' ELSE admin_alerts.status END
                """, cancellationToken);
        });

    /// <summary>The problem is gone: its alert, open or dismissed, is cleared. False when the database couldn't be written.</summary>
    public Task<bool> ClearAsync(string key, CancellationToken cancellationToken = default) =>
        Guarded("clear", key, () => Close(db.AdminAlerts.Where(a => a.Key == key), cancellationToken));

    /// <summary>
    /// A run of one source found these problems and no others: raises each, and clears that source's other alerts (a stock that
    /// fetched again, a strategy that ran, or was archived or deleted).
    /// </summary>
    public async Task SyncAsync(AlertSource source, IReadOnlyCollection<NewAlert> found, CancellationToken cancellationToken = default)
    {
        foreach (var alert in found) await RaiseAsync(source, alert, cancellationToken);
        var keys = found.Select(a => Cut(a.Key, KeyMax)).ToList();
        await Guarded("clear the others of", source.ToString(),
            () => Close(db.AdminAlerts.Where(a => a.Source == source && !keys.Contains(a.Key)), cancellationToken));
    }

    /// <summary>
    /// <see cref="SyncAsync"/> for a strategy check: one alert per strategy (<c>{keyPrefix}:{id}</c>), saying whose it is, its
    /// template and mode, then the problem. Each source has its own prefix, so one never clears the other's.
    /// </summary>
    public async Task SyncStrategiesAsync(AlertSource source, string keyPrefix,
        IReadOnlyList<(Strategy Strategy, string Title, string Detail)> problems, CancellationToken cancellationToken = default)
    {
        Dictionary<Guid, string> emails = [];
        if (problems.Count > 0)
        {
            var userIds = problems.Select(p => p.Strategy.UserId).Distinct().ToList();
            // Without the emails an alert still names the strategy: never a reason to fail the check.
            await Guarded("find the owners of", source.ToString(), async () => emails = await db.Users.AsNoTracking()
                .Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Email, cancellationToken));
        }
        await SyncAsync(source, [.. problems.Select(p => new NewAlert($"{keyPrefix}:{p.Strategy.Id}", p.Title,
            $"{emails.GetValueOrDefault(p.Strategy.UserId, "?")} · {p.Strategy.TemplateKey} · {p.Strategy.Mode.ToString().ToLowerInvariant()}\n{p.Detail}",
            p.Strategy.Id))], cancellationToken);
    }

    /// <summary>Deletes the alerts cleared long ago.</summary>
    public Task ForgetOldAsync(CancellationToken cancellationToken = default) =>
        Guarded("forget", "old alerts", async () =>
        {
            var before = time.GetUtcNow() - KeptAfterCleared;
            await db.AdminAlerts.Where(a => a.Status == AlertStatus.Cleared && a.ClosedAt < before).ExecuteDeleteAsync(cancellationToken);
        });

    async Task Close(IQueryable<AdminAlert> alerts, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        await alerts.Where(a => a.Status != AlertStatus.Cleared)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, AlertStatus.Cleared).SetProperty(a => a.ClosedAt, now), cancellationToken);
    }

    async Task<bool> Guarded(string action, string what, Func<Task> write)
    {
        try
        {
            await write();
            return true;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogWarning("Couldn't {Action} the admin alert {Key}: {Reason}", action, what, error.Message);
            return false;
        }
    }

    /// <summary>As EF stores it (<see cref="SnakeCaseEnumConverter{TEnum}"/>). The upsert's <c>'open'</c> and <c>'cleared'</c> are checked by a test.</summary>
    internal static string Stored<TEnum>(TEnum value) where TEnum : struct, Enum => JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());

    /// <summary>At most <paramref name="max"/> characters, never splitting an emoji's surrogate pair (Npgsql refuses half of one).</summary>
    static string Cut(string text, int max)
    {
        if (text.Length <= max) return text;
        var keep = max - 1;
        if (char.IsHighSurrogate(text[keep - 1])) keep--;
        return text[..keep] + "…";
    }

    /// <summary><c>GET /admin/alerts</c> and dismissing one, for the owner (inside the admin group).</summary>
    public static void MapAlerts(RouteGroupBuilder group)
    {
        var alerts = group.MapGroup("/alerts");

        alerts.MapGet("/", async (AppDbContext db, TimeProvider time, CancellationToken cancellationToken) =>
        {
            var since = time.GetUtcNow() - ShownAfterCleared;
            return await db.AdminAlerts.AsNoTracking()
                .Where(a => a.Status != AlertStatus.Cleared || a.ClosedAt >= since)
                .OrderByDescending(a => a.LastSeenAt)
                .Select(a => new AdminAlertDto(a.Id, a.Source, a.Status, a.Title, a.Detail, a.Count, a.FirstSeenAt, a.LastSeenAt, a.ClosedAt))
                .ToListAsync(cancellationToken);
        })
        .WithSummary("The open and dismissed alerts, and those cleared in the last 7 days, last seen first.");

        alerts.MapPost("/{id:guid}/dismiss", async Task<Results<NoContent, NotFound>> (Guid id, AppDbContext db, TimeProvider time,
            CancellationToken cancellationToken) =>
        {
            var now = time.GetUtcNow();
            var updated = await db.AdminAlerts.Where(a => a.Id == id && a.Status == AlertStatus.Open)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, AlertStatus.Dismissed).SetProperty(a => a.ClosedAt, now), cancellationToken);
            return updated == 0 ? TypedResults.NotFound() : TypedResults.NoContent();
        })
        .WithSummary("Dismisses an open alert: it stays quiet while its problem lasts, and opens again only if the problem clears and comes back.");
    }
}
