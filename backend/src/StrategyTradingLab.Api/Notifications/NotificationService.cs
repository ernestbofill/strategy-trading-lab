using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StrategyTradingLab.Api.Admin;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Email;
using StrategyTradingLab.Api.Evaluation;

namespace StrategyTradingLab.Api.Notifications;

/// <summary>
/// Sends the notification emails (spec: Notifications): the live signal alert right after the daily check that found it, and
/// the daily overview early in the user's morning. Each goes out once: a row in <c>notifications</c> is claimed first (its unique
/// key is the dedupe contract), then the email is sent and the row says whether it worked. Only to verified addresses.
/// </summary>
public class NotificationService(AppDbContext db, IEmailSender sender, SettingsStore settings, IConfiguration configuration, ILogger<NotificationService> logger)
{
    /// <summary>The overview goes out from this hour in the user's own time zone ("early morning").</summary>
    public const int OverviewHour = 7;

    string AppUrl => (configuration["PublicUrl"] ?? throw new InvalidOperationException("PublicUrl isn't set.")).TrimEnd('/');

    /// <summary>Whether the admin page lets the app send these emails (off on a development computer).</summary>
    public async Task<bool> EnabledAsync(CancellationToken cancellationToken) =>
        (await settings.ValuesAsync(cancellationToken)).GetValueOrDefault("EMAILS_ENABLED") != "false";

    /// <summary>An alert for each of these signals that is a live one still waiting, whose strategy has notifications on.</summary>
    public async Task<int> SendSignalAlertsAsync(IReadOnlyCollection<Guid> signalIds, CancellationToken cancellationToken)
    {
        if (signalIds.Count == 0) return 0;
        var rows = await (from signal in db.Signals.AsNoTracking()
                          join strategy in db.Strategies.AsNoTracking() on signal.StrategyId equals strategy.Id
                          join user in db.Users.AsNoTracking() on strategy.UserId equals user.Id
                          join asset in db.Assets.AsNoTracking() on signal.AssetId equals asset.Id
                          where signalIds.Contains(signal.Id) && signal.Status == SignalStatus.Waiting && strategy.Mode == StrategyMode.Live
                              && strategy.Notifications && strategy.Status == StrategyStatus.Running && user.EmailVerifiedAt != null
                          select new { signal, strategy, user.Id, user.Email, asset }).ToListAsync(cancellationToken);

        var sent = 0;
        foreach (var row in rows)
        {
            if (await ClaimAsync(row.Id, row.signal.Id, NotificationKind.Signal, row.signal.SignalDate, cancellationToken) is not { } claimed) continue;
            var (subject, body) = EmailTexts.SignalAlert(Line(row.signal, row.strategy, row.asset), AppUrl);
            if (await SendAsync(claimed, row.Email, subject, body, cancellationToken)) sent++;
        }
        return sent;
    }

    /// <summary>The overview for every user whose morning has begun and who hasn't had today's.</summary>
    public async Task<int> SendOverviewsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var users = await db.Users.AsNoTracking()
            .Where(u => u.OverviewEmail && u.EmailVerifiedAt != null && db.Strategies.Any(s => s.UserId == u.Id && s.Status == StrategyStatus.Running))
            .Select(u => new { u.Id, u.Email, u.TimeZone }).ToListAsync(cancellationToken);
        var lastSession = UsSession.LastComplete(now);
        var sent = 0;
        foreach (var user in users)
        {
            var local = LocalTime(now, user.TimeZone);
            if (local.Hour < OverviewHour) continue;
            var day = DateOnly.FromDateTime(local.DateTime);
            if (await ClaimAsync(user.Id, null, NotificationKind.Overview, day, cancellationToken) is not { } claimed) continue;

            var (subject, body) = await ComposeOverviewAsync(user.Id, day, lastSession, cancellationToken);
            if (await SendAsync(claimed, user.Email, subject, body, cancellationToken)) sent++;
        }
        return sent;
    }

    static DateTimeOffset LocalTime(DateTimeOffset now, string timeZone)
    {
        try { return TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(timeZone)); }
        catch (Exception error) when (error is TimeZoneNotFoundException or InvalidTimeZoneException) { return now; } // UTC, as the dashboard does.
    }

    async Task<(string Subject, string Body)> ComposeOverviewAsync(Guid userId, DateOnly day, DateOnly lastSession, CancellationToken cancellationToken)
    {
        var strategies = await db.Strategies.AsNoTracking().Where(s => s.UserId == userId && s.Status == StrategyStatus.Running).ToListAsync(cancellationToken);
        var ids = strategies.Select(s => s.Id).ToList();
        var signals = await (from signal in db.Signals.AsNoTracking()
                             join asset in db.Assets.AsNoTracking() on signal.AssetId equals asset.Id
                             where ids.Contains(signal.StrategyId) && (signal.Status == SignalStatus.Waiting || signal.SignalDate == lastSession)
                             select new { signal, asset }).ToListAsync(cancellationToken);
        var byId = strategies.ToDictionary(s => s.Id);
        SignalLine ToLine(Signal s, Asset a) => Line(s, byId[s.StrategyId], a);
        var waiting = signals.Where(x => x.signal.Status == SignalStatus.Waiting).Select(x => ToLine(x.signal, x.asset)).OrderBy(l => l.Date).ToList();
        var changed = signals.Where(x => x.signal.SignalDate == lastSession).Select(x => ToLine(x.signal, x.asset)).ToList();

        var watching = await (from eval in db.DailyEvaluations.AsNoTracking()
                              join asset in db.Assets.AsNoTracking() on eval.AssetId equals asset.Id
                              where ids.Contains(eval.StrategyId) && eval.Status == EvaluationStatus.Watching
                              select new { eval, asset.Name }).ToListAsync(cancellationToken);
        var close = watching
            .Where(x => x.eval.Date >= lastSession.AddDays(-4))
            .Select(x => (x.eval.StrategyId, x.Name, Distance: x.eval.Facts.RootElement.TryGetProperty("distancePct", out var d) && d.ValueKind == JsonValueKind.Number ? d.GetDecimal() : (decimal?)null))
            .Where(x => x.Distance is not null)
            .OrderBy(x => x.Distance).Take(5)
            .Select(x => new CloseLine(byId[x.StrategyId].Name, byId[x.StrategyId].TemplateKey, x.Name, x.Distance!.Value)).ToList();

        var counts = await db.Signals.AsNoTracking().Where(s => ids.Contains(s.StrategyId)).GroupBy(s => s.StrategyId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);
        var scoreboard = strategies.OrderBy(s => s.Name).Select(s => new ScoreLine(s.Name, s.Mode, s.StartedOn, counts.GetValueOrDefault(s.Id))).ToList();
        return EmailTexts.Overview(day, waiting, changed, close, scoreboard, AppUrl);
    }

    static SignalLine Line(Signal signal, Strategy strategy, Asset asset) =>
        new(strategy.Id, strategy.Name, strategy.TemplateKey, asset.Name, asset.Currency, strategy.Currency, signal.SignalDate, signal.ClosePrice,
            signal.StepNo, signal.MaxSteps, signal.StepAmount, signal.Facts.RootElement.Clone());

    /// <summary>Takes the right to send this notification, once: its id, or null when it was already claimed (a retry, a second run).</summary>
    async Task<Guid?> ClaimAsync(Guid userId, Guid? signalId, NotificationKind kind, DateOnly forDate, CancellationToken cancellationToken)
    {
        var notification = new Notification { UserId = userId, SignalId = signalId, Kind = kind, Channel = NotificationChannel.Email, ForDate = forDate };
        db.Notifications.Add(notification);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return notification.Id;
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            return null;
        }
    }

    /// <summary>Sends the email of a claimed notification and records whether it worked.</summary>
    async Task<bool> SendAsync(Guid notificationId, string to, string subject, string body, CancellationToken cancellationToken)
    {
        string? error = null;
        try
        {
            await sender.SendAsync(to, subject, body, cancellationToken);
        }
        catch (EmailException problem)
        {
            error = problem.Message; // Never the body: it is the user's data.
            logger.LogWarning("Couldn't send notification {Id}: {Reason}", notificationId, problem.Message);
        }
        var now = DateTimeOffset.UtcNow;
        await db.Notifications.Where(n => n.Id == notificationId)
            .ExecuteUpdateAsync(n => n.SetProperty(x => x.SentAt, error is null ? now : (DateTimeOffset?)null)
                .SetProperty(x => x.FailedAt, error is null ? (DateTimeOffset?)null : now).SetProperty(x => x.Error, error), cancellationToken);
        db.ChangeTracker.Clear();
        return error is null;
    }
}
