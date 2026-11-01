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
/// the daily overview right after the daily check that evaluated the session. Each goes out once: a row in <c>notifications</c> is claimed first (its unique
/// key is the dedupe contract), then the email is sent and the row says whether it worked. Only to verified addresses.
/// </summary>
public class NotificationService(AppDbContext db, IEmailSender sender, SettingsStore settings, IConfiguration configuration, AdminAlerts alerts,
    ILogger<NotificationService> logger)
{
    /// <summary>Snooze reminders and the AI round reminder go out from this hour in the user's own time zone (a morning reminder), and no overview goes out before it.</summary>
    public const int MorningHour = 7;

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
                              && strategy.Notifications && strategy.Status == StrategyStatus.Running && user.EmailVerifiedAt != null && user.AlertEmails
                              && !db.Notifications.Any(x => x.SignalId == signal.Id && x.Kind == NotificationKind.Signal && x.Channel == NotificationChannel.Email)
                          select new { signal, strategy, user.Id, user.Email, user.Locale, user.UnsubscribeToken, asset }).ToListAsync(cancellationToken);

        var sent = 0;
        foreach (var row in rows)
        {
            if (await ClaimAsync(row.Id, row.signal.Id, NotificationKind.Signal, row.signal.SignalDate, cancellationToken) is not { } claimed) continue;
            if (await SendAsync(claimed, row.Email, () => Task.FromResult(EmailTexts.SignalAlert(Line(row.signal, row.strategy, row.asset), AppUrl, row.Locale, row.UnsubscribeToken)), cancellationToken)) sent++;
        }
        return await SettledAsync(sent);
    }

    /// <summary>
    /// The overview of the last finished US session, once, to every user the daily check has evaluated that session for: sent right
    /// after the check, and by the hourly check for whoever wasn't sent it then (email not set up yet, a restart, an address just
    /// confirmed). Never in the user's night (before 07:00 their time): it waits for the morning. One per trading day, so a weekend
    /// sends Friday's once and not three times.
    /// </summary>
    public async Task<int> SendOverviewsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var lastSession = UsSession.LastComplete(now);
        var users = await db.Users.AsNoTracking()
            .Where(u => u.OverviewEmail && u.EmailVerifiedAt != null && db.Strategies.Any(s => s.UserId == u.Id && s.Status == StrategyStatus.Running)
                && !db.Notifications.Any(x => x.UserId == u.Id && x.Kind == NotificationKind.Overview && x.ForDate == lastSession)
                // Only once the daily check has evaluated that session for them: an overview of stale data would say nothing happened.
                && db.Strategies.Any(s => s.UserId == u.Id && db.DailyEvaluations.Any(e => e.StrategyId == s.Id && e.Date == lastSession)))
            .Select(u => new { u.Id, u.Email, u.TimeZone, u.Locale, u.UnsubscribeToken }).ToListAsync(cancellationToken);
        var sent = 0;
        foreach (var user in users)
        {
            var local = LocalTime(now, user.TimeZone);
            if (local.Hour < MorningHour) continue;
            if (await ClaimAsync(user.Id, null, NotificationKind.Overview, lastSession, cancellationToken) is not { } claimed) continue;

            // AI rounds due count by the user's own date, which may already be the day after the session.
            var today = DateOnly.FromDateTime(local.DateTime);
            if (await SendAsync(claimed, user.Email, () => ComposeOverviewAsync(user.Id, today, lastSession, user.Locale, user.UnsubscribeToken, cancellationToken), cancellationToken)) sent++;
        }
        return await SettledAsync(sent);
    }

    /// <summary>
    /// The reminder of each snoozed signal, once, from 07:00 of its snooze day in the user's time zone, while it still waits
    /// and its strategy has notifications on (spec: Snooze). Snoozing again sets a later day, which gets its own reminder.
    /// </summary>
    public async Task<int> SendSnoozeRemindersAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        // No time zone is more than a day ahead of UTC, so later snooze days can wait.
        var latest = DateOnly.FromDateTime(now.UtcDateTime).AddDays(1);
        // Nor more than a day behind, plus a day for time zones: older snooze days are no longer reminded (see below).
        var earliest = DateOnly.FromDateTime(now.UtcDateTime).AddDays(-2);
        var rows = await (from signal in db.Signals.AsNoTracking()
                          join strategy in db.Strategies.AsNoTracking() on signal.StrategyId equals strategy.Id
                          join user in db.Users.AsNoTracking() on strategy.UserId equals user.Id
                          join asset in db.Assets.AsNoTracking() on signal.AssetId equals asset.Id
                          where signal.Status == SignalStatus.Waiting && signal.SnoozedUntil != null && signal.SnoozedUntil <= latest && signal.SnoozedUntil >= earliest
                              && strategy.Mode == StrategyMode.Live && strategy.Notifications && strategy.Status == StrategyStatus.Running && user.EmailVerifiedAt != null && user.AlertEmails
                              && !db.Notifications.Any(x => x.SignalId == signal.Id && x.Kind == NotificationKind.Snoozed
                                  && x.Channel == NotificationChannel.Email && x.ForDate == signal.SnoozedUntil)
                          select new { signal, strategy, user.Id, user.Email, user.TimeZone, user.Locale, user.UnsubscribeToken, asset }).ToListAsync(cancellationToken);

        var sent = 0;
        foreach (var row in rows)
        {
            var local = LocalTime(now, row.TimeZone);
            var day = row.signal.SnoozedUntil!.Value;
            var localDay = DateOnly.FromDateTime(local.DateTime);
            if (localDay < day || (localDay == day && local.Hour < MorningHour)) continue;
            // A day already gone (the reminder couldn't go out then, e.g. notifications were off) isn't reminded late.
            if (localDay > day.AddDays(1)) continue;
            if (await ClaimAsync(row.Id, row.signal.Id, NotificationKind.Snoozed, day, cancellationToken) is not { } claimed) continue;
            if (await SendAsync(claimed, row.Email, () => Task.FromResult(EmailTexts.SnoozeReminder(Line(row.signal, row.strategy, row.asset), AppUrl, row.Locale, row.UnsubscribeToken)), cancellationToken)) sent++;
        }
        return await SettledAsync(sent);
    }

    /// <summary>
    /// "Time for the AI round", once per user and due day, from 07:00 of that day in the user's time zone (spec: Notifications,
    /// AI round due), listing every round of theirs due that day whose strategy has notifications on. Not for a strategy's first
    /// round, which is due the day it starts (Confirm opens it), nor for a round whose week has run out.
    /// </summary>
    public async Task<int> SendAiRoundRemindersAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        // As for snoozes: no time zone is more than a day from UTC.
        var latest = DateOnly.FromDateTime(now.UtcDateTime).AddDays(1);
        var earliest = DateOnly.FromDateTime(now.UtcDateTime).AddDays(-2);
        var rows = await (from round in db.AiRounds.AsNoTracking()
                          join strategy in db.Strategies.AsNoTracking() on round.StrategyId equals strategy.Id
                          join user in db.Users.AsNoTracking() on strategy.UserId equals user.Id
                          where round.Status == AiRoundStatus.Due && round.DueOn <= latest && round.DueOn >= earliest && round.DueOn > strategy.StartedOn
                              && strategy.Notifications && strategy.Status == StrategyStatus.Running && user.EmailVerifiedAt != null && user.AlertEmails
                              && !db.Notifications.Any(x => x.UserId == user.Id && x.Kind == NotificationKind.AiRound
                                  && x.Channel == NotificationChannel.Email && x.ForDate == round.DueOn)
                          select new { round.DueOn, strategy.Id, strategy.Name, UserId = user.Id, user.Email, user.TimeZone, user.Locale, user.UnsubscribeToken }).ToListAsync(cancellationToken);

        var sent = 0;
        foreach (var group in rows.GroupBy(r => (r.UserId, r.DueOn)))
        {
            var first = group.First();
            var local = LocalTime(now, first.TimeZone);
            var localDay = DateOnly.FromDateTime(local.DateTime);
            if (localDay < group.Key.DueOn || (localDay == group.Key.DueOn && local.Hour < MorningHour)) continue;
            if (localDay > group.Key.DueOn.AddDays(1)) continue; // A day gone isn't reminded late; the overview still lists it.
            if (await ClaimAsync(group.Key.UserId, null, NotificationKind.AiRound, group.Key.DueOn, cancellationToken) is not { } claimed) continue;
            var strategies = group.OrderBy(r => r.Name).Select(r => (r.Id, r.Name)).ToList();
            if (await SendAsync(claimed, first.Email, () => Task.FromResult(EmailTexts.AiRoundDue(strategies, AppUrl, first.Locale, first.UnsubscribeToken)), cancellationToken)) sent++;
        }
        return await SettledAsync(sent);
    }

    static DateTimeOffset LocalTime(DateTimeOffset now, string timeZone)
    {
        try { return TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(timeZone)); }
        catch (Exception error) when (error is TimeZoneNotFoundException or InvalidTimeZoneException) { return now; } // UTC, as the dashboard does.
    }

    async Task<EmailContent> ComposeOverviewAsync(Guid userId, DateOnly today, DateOnly lastSession, string locale, string unsubscribeToken, CancellationToken cancellationToken)
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
        var changed = signals.Where(x => x.signal.SignalDate == lastSession && x.signal.Status != SignalStatus.Waiting).Select(x => ToLine(x.signal, x.asset)).ToList();

        // Each stock's latest evaluation only, and only if it is still watching.
        var recent = await (from eval in db.DailyEvaluations.AsNoTracking()
                            join asset in db.Assets.AsNoTracking() on eval.AssetId equals asset.Id
                            where ids.Contains(eval.StrategyId) && eval.Date >= lastSession.AddDays(-4)
                            select new { eval, asset.Name }).ToListAsync(cancellationToken);
        var watching = recent.GroupBy(x => (x.eval.StrategyId, x.eval.AssetId)).Select(g => g.OrderByDescending(x => x.eval.Date).First())
            .Where(x => x.eval.Status == EvaluationStatus.Watching);
        var stockChanges = StockChangeLines(recent.Select(x => (x.eval, x.Name)), lastSession, byId);
        var close = watching
            .Where(x => !stockChanges.Any(c => c.StrategyId == x.eval.StrategyId && c.AssetId == x.eval.AssetId))
            .Select(x => (x.eval.StrategyId, x.Name, Distance: StockChanges.Distance(x.eval.Facts.RootElement)))
            .Where(x => x.Distance is not null)
            .OrderBy(x => x.Distance).Take(5)
            .Select(x => new CloseLine(byId[x.StrategyId].Name, byId[x.StrategyId].Mode, byId[x.StrategyId].TemplateKey, x.Name, x.Distance!.Value)).ToList();

        var roundsDue = await db.AiRounds.AsNoTracking().Where(r => ids.Contains(r.StrategyId) && r.Status == AiRoundStatus.Due && r.DueOn <= today)
            .OrderBy(r => r.DueOn).Select(r => r.StrategyId).ToListAsync(cancellationToken);
        var counts = await db.Signals.AsNoTracking().Where(s => ids.Contains(s.StrategyId)).GroupBy(s => s.StrategyId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);
        var scoreboard = strategies.OrderBy(s => s.Name).Select(s => new ScoreLine(s.Name, s.Mode, s.StartedOn, counts.GetValueOrDefault(s.Id))).ToList();
        return EmailTexts.Overview(lastSession, waiting, changed, close, scoreboard, AppUrl, locale, [.. roundsDue.Select(id => (id, byId[id].Name))], unsubscribeToken, stockChanges);
    }

    /// <summary>
    /// The stock changes of the strategies that asked for them: each stock evaluated on the last session against its evaluation
    /// the trading day before (the recent days' rows hold both). A stock without an earlier day (just added) has nothing to compare.
    /// </summary>
    static List<StockChangeLine> StockChangeLines(IEnumerable<(DailyEvaluation Eval, string Name)> recent, DateOnly lastSession, Dictionary<Guid, Strategy> strategies) =>
        recent.Where(x => strategies[x.Eval.StrategyId].StockChangeEmails)
            .GroupBy(x => (x.Eval.StrategyId, x.Eval.AssetId))
            .Select(g => g.OrderByDescending(x => x.Eval.Date).Take(2).ToList())
            .Where(days => days.Count == 2 && days[0].Eval.Date == lastSession)
            .Select(days => (days, change: StockChanges.Between((days[1].Eval.Status, days[1].Eval.Facts.RootElement), (days[0].Eval.Status, days[0].Eval.Facts.RootElement))))
            .Where(x => x.change is not null)
            .Select(x =>
            {
                var strategy = strategies[x.days[0].Eval.StrategyId];
                return new StockChangeLine(strategy.Id, x.days[0].Eval.AssetId, strategy.Name, strategy.Mode, strategy.TemplateKey, x.days[0].Name, x.change!.Value.Kind, x.change.Value.DistancePct);
            })
            .OrderBy(c => c.StrategyName).ThenBy(c => c.StockName).ToList();

    static SignalLine Line(Signal signal, Strategy strategy, Asset asset) => SignalLine.From(signal, strategy, asset);

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
    /// (nothing happened: a later run tries again); a refused send is recorded as failed; a shutdown keeps the claim (once, never twice).
    /// Any other error is recorded and does not stop the others.
    /// </summary>
    async Task<bool> SendAsync(Guid notificationId, string to, Func<Task<EmailContent>> compose, CancellationToken cancellationToken)
    {
        string? error = null;
        var shutDown = false;
        try
        {
            await sender.SendAsync(new OutgoingEmail(to, await compose()), cancellationToken);
        }
        catch (EmailNotSetUpException notSetUp)
        {
            await ReleaseAsync(notificationId);
            await EmailFailedAsync(notSetUp.Message);
            return false;
        }
        catch (EmailSuppressedException)
        {
            // Skipped, not sent and not failed: the row says why, and no alert opens for an address the mail service already told us about.
            await db.Notifications.Where(n => n.Id == notificationId)
                .ExecuteUpdateAsync(n => n.SetProperty(x => x.Error, "Not sent: the address is on the suppression list."), CancellationToken.None);
            db.ChangeTracker.Clear();
            return false;
        }
        // A shutdown keeps the claim: the mail server may have accepted the email already, and losing one is better than sending it twice.
        catch (Exception problem)
        {
            // Not the server's own words (they often quote the address) and never the body: both are the user's data.
            error = problem is EmailException ? "The mail server refused or couldn't send it." : "The email couldn't be written or sent.";
            logger.LogWarning("Couldn't send notification {Id}: {Kind}", notificationId, problem.GetType().Name);
            shutDown = problem is OperationCanceledException && cancellationToken.IsCancellationRequested;
        }
        // A shutdown isn't an email problem: the claim stays as it is, neither sent nor failed, and raises no alert.
        if (shutDown) return false;
        var now = DateTimeOffset.UtcNow;
        await db.Notifications.Where(n => n.Id == notificationId)
            .ExecuteUpdateAsync(n => n.SetProperty(x => x.SentAt, error is null ? now : (DateTimeOffset?)null)
                .SetProperty(x => x.FailedAt, error is null ? (DateTimeOffset?)null : now).SetProperty(x => x.Error, error), CancellationToken.None);
        db.ChangeTracker.Clear();
        if (error is null) anySent = true;
        else await EmailFailedAsync(error);
        return error is null;
    }

    const string EmailAlertKey = "email:notifications";

    // What this scope's sends did, so the alert clears once, after a batch where every email went out (not per email).
    bool anySent, anyFailed;

    /// <summary>
    /// How long a failure keeps the alert from clearing: a week, so an address that fails whenever it is sent to (a weekend or a
    /// clock change in between) keeps the alert open, and dismissed if it was.
    /// </summary>
    static readonly TimeSpan FailureMemory = TimeSpan.FromDays(7);

    async Task EmailFailedAsync(string problem)
    {
        anyFailed = true;
        await alerts.RaiseAsync(AlertSource.Email, new NewAlert(EmailAlertKey, "Emails to users failed",
            $"The last one: {problem} The count is the failures since every email of a run last went out."), CancellationToken.None);
    }

    /// <summary>The end of a batch: clears the email alert if emails went out and none failed. Returns how many were sent.</summary>
    async Task<int> SettledAsync(int sent)
    {
        if (!anySent || anyFailed) return sent;
        // Not while any other run failed in the last day (another time zone's overviews, the alerts): mixed results would
        // otherwise clear the alert and open it again each day, undoing a dismiss.
        var since = DateTimeOffset.UtcNow - FailureMemory;
        try
        {
            if (await db.Notifications.AnyAsync(n => n.FailedAt > since, CancellationToken.None)) return sent;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogWarning("The email alert couldn't be checked: {Reason}", error.Message);
            return sent;
        }
        await alerts.ClearAsync(EmailAlertKey, CancellationToken.None);
        return sent;
    }

    /// <summary>Gives the claim back: nothing was sent, so the next run may send it.</summary>
    async Task ReleaseAsync(Guid notificationId)
    {
        await db.Notifications.Where(n => n.Id == notificationId).ExecuteDeleteAsync(CancellationToken.None);
        db.ChangeTracker.Clear();
    }
}
