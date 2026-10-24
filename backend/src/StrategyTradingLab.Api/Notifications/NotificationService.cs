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

    /// <summary>How far back an alert that wasn't sent is still tried.</summary>
    public const int AlertDays = 3;

    string AppUrl => (configuration["PublicUrl"] ?? throw new InvalidOperationException("PublicUrl isn't set.")).TrimEnd('/');

    /// <summary>Whether the admin page lets the app send these emails (off on a development computer).</summary>
    public async Task<bool> EnabledAsync(CancellationToken cancellationToken) =>
        (await settings.ValuesAsync(cancellationToken)).GetValueOrDefault("EMAILS_ENABLED") != "false";

    /// <summary>
    /// An alert for each live signal of the last <see cref="AlertDays"/> days that is still waiting, whose strategy has notifications
    /// on, and that has none yet. Looking back (instead of at the signals a run just made) is what retries an alert a run
    /// couldn't send (email not set up yet, a shutdown) without ever sending one twice.
    /// </summary>
    public async Task<int> SendSignalAlertsAsync(DateOnly today, CancellationToken cancellationToken)
    {
        var since = today.AddDays(-AlertDays);
        var rows = await (from signal in db.Signals.AsNoTracking()
                          join strategy in db.Strategies.AsNoTracking() on signal.StrategyId equals strategy.Id
                          join user in db.Users.AsNoTracking() on strategy.UserId equals user.Id
                          join asset in db.Assets.AsNoTracking() on signal.AssetId equals asset.Id
                          where signal.SignalDate >= since && signal.Status == SignalStatus.Waiting && strategy.Mode == StrategyMode.Live
                              && strategy.Notifications && strategy.Status == StrategyStatus.Running && user.EmailVerifiedAt != null
                              && !db.Notifications.Any(x => x.SignalId == signal.Id && x.Kind == NotificationKind.Signal && x.Channel == NotificationChannel.Email)
                          select new { signal, strategy, user.Id, user.Email, asset }).ToListAsync(cancellationToken);

        var sent = 0;
        foreach (var row in rows)
        {
            if (await ClaimAsync(row.Id, row.signal.Id, NotificationKind.Signal, row.signal.SignalDate, cancellationToken) is not { } claimed) continue;
            if (await SendAsync(claimed, row.Email, () => EmailTexts.SignalAlert(Line(row.signal, row.strategy, row.asset), AppUrl), cancellationToken)) sent++;
        }
        return sent;
    }

    /// <summary>
    /// The overview of the last finished US session, once, to every user whose next morning has begun (07:00 in their time zone,
    /// on a later calendar day than the session): one per trading day, so a weekend sends Friday's once and not three times.
    /// </summary>
    public async Task<int> SendOverviewsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var lastSession = UsSession.LastComplete(now);
        var users = await db.Users.AsNoTracking()
            .Where(u => u.OverviewEmail && u.EmailVerifiedAt != null && db.Strategies.Any(s => s.UserId == u.Id && s.Status == StrategyStatus.Running)
                && !db.Notifications.Any(x => x.UserId == u.Id && x.Kind == NotificationKind.Overview && x.ForDate == lastSession))
            .Select(u => new { u.Id, u.Email, u.TimeZone }).ToListAsync(cancellationToken);
        var sent = 0;
        foreach (var user in users)
        {
            var local = LocalTime(now, user.TimeZone);
            if (local.Hour < OverviewHour || DateOnly.FromDateTime(local.DateTime) <= lastSession) continue;
            if (await ClaimAsync(user.Id, null, NotificationKind.Overview, lastSession, cancellationToken) is not { } claimed) continue;

            var today = DateOnly.FromDateTime(local.DateTime);
            if (await SendAsync(claimed, user.Email, () => ComposeOverviewAsync(user.Id, today, lastSession, cancellationToken).GetAwaiter().GetResult(), cancellationToken)) sent++;
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

        // Each stock's latest evaluation only, and only if it is still watching.
        var recent = await (from eval in db.DailyEvaluations.AsNoTracking()
                            join asset in db.Assets.AsNoTracking() on eval.AssetId equals asset.Id
                            where ids.Contains(eval.StrategyId) && eval.Date >= lastSession.AddDays(-4)
                            select new { eval, asset.Name }).ToListAsync(cancellationToken);
        var watching = recent.GroupBy(x => (x.eval.StrategyId, x.eval.AssetId)).Select(g => g.OrderByDescending(x => x.eval.Date).First())
            .Where(x => x.eval.Status == EvaluationStatus.Watching);
        var close = watching
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

    /// <summary>
    /// Writes and sends the email of a claimed notification and records whether it worked. Email not set up yet releases the claim
    /// (nothing happened: a later run tries again); a refused send is recorded as failed; a shutdown releases the claim too, so
    /// "once" never turns into "never". Any other error is recorded and does not stop the others.
    /// </summary>
    async Task<bool> SendAsync(Guid notificationId, string to, Func<(string Subject, string Body)> compose, CancellationToken cancellationToken)
    {
        string? error = null;
        try
        {
            var (subject, body) = compose();
            await sender.SendAsync(to, subject, body, cancellationToken);
        }
        catch (EmailNotSetUpException)
        {
            await ReleaseAsync(notificationId);
            return false;
        }
        catch (OperationCanceledException)
        {
            await ReleaseAsync(notificationId);
            throw;
        }
        catch (Exception problem)
        {
            error = problem is EmailException ? problem.Message : "The email couldn't be written or sent."; // Never the body: it is the user's data.
            logger.LogWarning("Couldn't send notification {Id}: {Reason}", notificationId, problem.Message);
        }
        var now = DateTimeOffset.UtcNow;
        await db.Notifications.Where(n => n.Id == notificationId)
            .ExecuteUpdateAsync(n => n.SetProperty(x => x.SentAt, error is null ? now : (DateTimeOffset?)null)
                .SetProperty(x => x.FailedAt, error is null ? (DateTimeOffset?)null : now).SetProperty(x => x.Error, error), CancellationToken.None);
        db.ChangeTracker.Clear();
        return error is null;
    }

    /// <summary>Gives the claim back: nothing was sent, so the next run may send it.</summary>
    async Task ReleaseAsync(Guid notificationId)
    {
        await db.Notifications.Where(n => n.Id == notificationId).ExecuteDeleteAsync(CancellationToken.None);
        db.ChangeTracker.Clear();
    }
}
