using Microsoft.EntityFrameworkCore;
using Npgsql;
using StrategyTradingLab.Api.Admin;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Email;
using StrategyTradingLab.Api.Evaluation;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Notifications;

/// <summary>
/// Sends the notification emails (spec: Notifications): the live signal alert right after the daily check that found it, and
/// the summary right after the daily check that evaluated the session. Each goes out once: a row in <c>notifications</c> is claimed first (its unique
/// key is the dedupe contract), then the email is sent and the row says whether it worked. Only to verified addresses.
/// </summary>
public class NotificationService(AppDbContext db, IEmailSender sender, IPushSender push, SettingsStore settings, IConfiguration configuration, AdminAlerts alerts,
    TemplateStore templates,
    ILogger<NotificationService> logger)
{
    /// <summary>Snooze reminders and the AI round reminder go out from this hour in the user's own time zone (a morning reminder), and no overview goes out before it.</summary>
    public const int MorningHour = 7;

    /// <summary>The summary's window of earlier daily checks: stock changes compare against them.</summary>
    const int RecentDays = 4;

    /// <summary>
    /// A stock's signal alert goes out once in this window per strategy and side: an extra check the same day (a re-run, a price
    /// that came in late) that signals the same stock again records its alert as not sent instead.
    /// </summary>
    static readonly TimeSpan RepeatWindow = TimeSpan.FromHours(20);

    /// <summary>How far back an alert that wasn't sent is still tried.</summary>
    public const int AlertDays = 3;

    string AppUrl => AppLink.AppUrl(configuration);

    /// <summary>Whether the admin page lets the app send these emails (off on a development computer).</summary>
    public async Task<bool> EnabledAsync(CancellationToken cancellationToken) =>
        (await settings.ValuesAsync(cancellationToken)).GetValueOrDefault("EMAILS_ENABLED") != "false";

    /// <summary>
    /// An alert for each live signal of the last <see cref="AlertDays"/> days that is still waiting, of a user with alert emails
    /// on, and that has none yet. Looking back (instead of at the signals a run just made) is what retries an alert a run
    /// couldn't send (email not set up yet, a shutdown) without ever sending one twice.
    /// </summary>
    public async Task<int> SendSignalAlertsAsync(DateOnly today, CancellationToken cancellationToken)
    {
        var recent = DateTimeOffset.UtcNow - RepeatWindow;
        var since = today.AddDays(-AlertDays);
        var rows = await (from signal in db.Signals.AsNoTracking()
                          join strategy in db.Strategies.AsNoTracking() on signal.StrategyId equals strategy.Id
                          join user in db.Users.AsNoTracking() on strategy.UserId equals user.Id
                          join asset in db.Assets.AsNoTracking() on signal.AssetId equals asset.Id
                          where signal.SignalDate >= since && signal.Status == SignalStatus.Waiting && strategy.Mode == StrategyMode.Live
                              && strategy.Status == StrategyStatus.Running && user.EmailVerifiedAt != null && user.AlertEmails
                              && strategy.TemplateKey != "ai"
                              && !db.Notifications.Any(x => x.SignalId == signal.Id && x.Kind == NotificationKind.Signal && x.Channel == NotificationChannel.Email)
                          // A second check the same day (a re-run, a price that came in late) can signal the same stock again: it was alerted already.
                          let repeat = db.Notifications.Any(x => x.Kind == NotificationKind.Signal && x.Channel == NotificationChannel.Email && x.SentAt > recent
                              && db.Signals.Any(o => o.Id == x.SignalId && o.StrategyId == signal.StrategyId && o.AssetId == signal.AssetId && o.Side == signal.Side))
                          select new { signal, strategy, user.Id, user.Email, user.Locale, user.UnsubscribeToken, asset, repeat }).ToListAsync(cancellationToken);

        var sent = 0;
        var seen = new HashSet<(Guid, Guid, TradeSide)>();
        foreach (var row in rows.OrderBy(r => r.signal.CreatedAt))
        {
            if (await ClaimAsync(row.Id, row.signal.Id, NotificationKind.Signal, row.signal.SignalDate, cancellationToken) is not { } claimed) continue;
            // Only a send that worked holds back the batch's other signal for the stock: after a failed one, the next is tried.
            if (row.repeat || seen.Contains(SameThing(row.signal)))
            {
                await NotSentAsync(claimed, "Not sent: this stock's alert went out today.");
                continue;
            }
            if (await SendAsync(claimed, row.Email, () => Task.FromResult<EmailContent?>(EmailTexts.SignalAlert(Line(row.signal, row.strategy, row.asset), AppUrl, row.Locale, row.UnsubscribeToken)), cancellationToken))
            {
                sent++;
                seen.Add(SameThing(row.signal));
            }
        }
        return await SettledAsync(sent);
    }

    /// <summary>
    /// The summary of the last finished US session, once, to every user the daily check has evaluated that session for, on days
    /// something changed (a signal or a stock change; a quiet day sends nothing, and its claim says so): sent right
    /// after the check, and by the hourly check for whoever wasn't sent it then (email not set up yet, a restart, an address just
    /// confirmed). Never in the user's night (before 07:00 their time): it waits for the morning. One per trading day, so a weekend
    /// sends Friday's once and not three times.
    /// </summary>
    public async Task<int> SendOverviewsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var lastSession = UsSession.LastComplete(now);
        var users = await db.Users.AsNoTracking()
            .Where(u => u.OverviewEmail && u.EmailVerifiedAt != null && db.Strategies.Any(s => s.UserId == u.Id && s.Status == StrategyStatus.Running)
                && !db.Notifications.Any(x => x.UserId == u.Id && x.Kind == NotificationKind.Overview && x.Channel == NotificationChannel.Email && x.ForDate == lastSession)
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
            // A quiet day is only final once every strategy has been checked for the session: until then (the daily check still
            // running, a strategy that couldn't be checked yet), the claim is given back and a later run looks again.
            var incomplete = false;
            if (await SendAsync(claimed, user.Email, async () =>
                {
                    var (content, complete) = await ComposeSummaryAsync(user.Id, today, lastSession, user.Locale, user.UnsubscribeToken, cancellationToken);
                    incomplete = content is null && !complete;
                    return content;
                }, cancellationToken)) sent++;
            else if (incomplete) await ReleaseAsync(claimed);
        }
        return await SettledAsync(sent);
    }

    /// <summary>
    /// The push twin of <see cref="SendOverviewsAsync"/>: pushes the summary of the last finished US session, once, to every user
    /// with summary push on and registered devices, on days something changed (a signal on the session or a stock change).
    /// </summary>
    public async Task<int> PushOverviewsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (push.PublicKey is null) return 0;
        var lastSession = UsSession.LastComplete(now);
        var users = await db.Users.AsNoTracking()
            .Where(u => u.OverviewPush && db.Strategies.Any(s => s.UserId == u.Id && s.Status == StrategyStatus.Running)
                && db.PushSubscriptions.Any(p => p.UserId == u.Id)
                && !db.Notifications.Any(x => x.UserId == u.Id && x.Kind == NotificationKind.Overview && x.Channel == NotificationChannel.Push && x.ForDate == lastSession)
                && db.Strategies.Any(s => s.UserId == u.Id && db.DailyEvaluations.Any(e => e.StrategyId == s.Id && e.Date == lastSession)))
            .Select(u => new { u.Id, u.TimeZone, u.Locale }).ToListAsync(cancellationToken);
        var sent = 0;
        foreach (var user in users)
        {
            var local = LocalTime(now, user.TimeZone);
            if (local.Hour < MorningHour) continue;
            if (await ClaimAsync(user.Id, null, NotificationKind.Overview, lastSession, cancellationToken, NotificationChannel.Push) is not { } claimed) continue;

            var today = DateOnly.FromDateTime(local.DateTime);
            var data = await LoadSummaryDataAsync(user.Id, today, lastSession, cancellationToken);
            if (!data.HasChanges)
            {
                if (data.Complete)
                {
                    await db.Notifications.Where(n => n.Id == claimed)
                        .ExecuteUpdateAsync(n => n.SetProperty(x => x.Error, "Not sent: nothing changed."), CancellationToken.None);
                    db.ChangeTracker.Clear();
                }
                else
                {
                    await ReleaseAsync(claimed);
                }
                continue;
            }

            if (await PushAsync(claimed, user.Id, PushTexts.Summary(lastSession, user.Locale), cancellationToken))
                sent++;
        }
        return sent;
    }

    /// <summary>
    /// The reminder of each snoozed signal, once, from 07:00 of its snooze day in the user's time zone, while it still waits
    /// and its strategy runs (spec: Snooze). Snoozing again sets a later day, which gets its own reminder.
    /// </summary>
    public async Task<int> SendSnoozeRemindersAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var (earliest, latest) = ReminderDays(now);
        var rows = await (from signal in db.Signals.AsNoTracking()
                          join strategy in db.Strategies.AsNoTracking() on signal.StrategyId equals strategy.Id
                          join user in db.Users.AsNoTracking() on strategy.UserId equals user.Id
                          join asset in db.Assets.AsNoTracking() on signal.AssetId equals asset.Id
                          where signal.Status == SignalStatus.Waiting && signal.SnoozedUntil != null && signal.SnoozedUntil <= latest && signal.SnoozedUntil >= earliest
                              && strategy.Mode == StrategyMode.Live && strategy.Status == StrategyStatus.Running && user.EmailVerifiedAt != null && user.AlertEmails
                              && strategy.TemplateKey != "ai"
                              && !db.Notifications.Any(x => x.SignalId == signal.Id && x.Kind == NotificationKind.Snoozed
                                  && x.Channel == NotificationChannel.Email && x.ForDate == signal.SnoozedUntil)
                          select new { signal, strategy, user.Id, user.Email, user.TimeZone, user.Locale, user.UnsubscribeToken, asset }).ToListAsync(cancellationToken);

        var sent = 0;
        foreach (var row in rows)
        {
            var day = row.signal.SnoozedUntil!.Value;
            if (!IsMorningOf(day, LocalTime(now, row.TimeZone))) continue;
            if (await ClaimAsync(row.Id, row.signal.Id, NotificationKind.Snoozed, day, cancellationToken) is not { } claimed) continue;
            if (await SendAsync(claimed, row.Email, () => Task.FromResult<EmailContent?>(EmailTexts.SnoozeReminder(Line(row.signal, row.strategy, row.asset), AppUrl, row.Locale, row.UnsubscribeToken)), cancellationToken)) sent++;
        }
        return await SettledAsync(sent);
    }

    /// <summary>
    /// "Time for the AI round", once per user and due day, from 07:00 of that day in the user's time zone (spec: Notifications,
    /// AI round due), listing every round of theirs due that day. Not for a strategy's first
    /// round, which is due the day it starts (Confirm opens it), nor for a round whose week has run out.
    /// </summary>
    public async Task<int> SendAiRoundRemindersAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var (earliest, latest) = ReminderDays(now);
        var rows = await (from round in db.AiRounds.AsNoTracking()
                          join strategy in db.Strategies.AsNoTracking() on round.StrategyId equals strategy.Id
                          join user in db.Users.AsNoTracking() on strategy.UserId equals user.Id
                          where round.Status == AiRoundStatus.Due && round.DueOn <= latest && round.DueOn >= earliest && round.DueOn > strategy.StartedOn
                              && strategy.Status == StrategyStatus.Running && user.EmailVerifiedAt != null && user.AlertEmails
                              && !db.Notifications.Any(x => x.UserId == user.Id && x.Kind == NotificationKind.AiRound
                                  && x.Channel == NotificationChannel.Email && x.ForDate == round.DueOn)
                          select new { round.DueOn, strategy.Id, strategy.Name, UserId = user.Id, user.Email, user.TimeZone, user.Locale, user.UnsubscribeToken }).ToListAsync(cancellationToken);

        var sent = 0;
        foreach (var group in rows.GroupBy(r => (r.UserId, r.DueOn)))
        {
            var first = group.First();
            if (!IsMorningOf(group.Key.DueOn, LocalTime(now, first.TimeZone))) continue;
            if (await ClaimAsync(group.Key.UserId, null, NotificationKind.AiRound, group.Key.DueOn, cancellationToken) is not { } claimed) continue;
            var strategies = group.OrderBy(r => r.Name).Select(r => (r.Id, r.Name)).ToList();
            if (await SendAsync(claimed, first.Email, () => Task.FromResult<EmailContent?>(EmailTexts.AiRoundDue(strategies, AppUrl, first.Locale, first.UnsubscribeToken)), cancellationToken)) sent++;
        }
        return await SettledAsync(sent);
    }

    /// <summary>
    /// The push twin of <see cref="SendSignalAlertsAsync"/>: each waiting live signal, once, to all the devices of a user with
    /// alert push on. It needs no confirmed email and ignores the email switches: push has its own.
    /// </summary>
    public async Task<int> PushSignalAlertsAsync(DateOnly today, CancellationToken cancellationToken)
    {
        if (push.PublicKey is null) return 0;
        var recent = DateTimeOffset.UtcNow - RepeatWindow;
        var since = today.AddDays(-AlertDays);
        var rows = await (from signal in db.Signals.AsNoTracking()
                          join strategy in db.Strategies.AsNoTracking() on signal.StrategyId equals strategy.Id
                          join user in db.Users.AsNoTracking() on strategy.UserId equals user.Id
                          join asset in db.Assets.AsNoTracking() on signal.AssetId equals asset.Id
                          where signal.SignalDate >= since && signal.Status == SignalStatus.Waiting && strategy.Mode == StrategyMode.Live
                              && user.AlertPush && strategy.Status == StrategyStatus.Running && db.PushSubscriptions.Any(p => p.UserId == user.Id)
                              && strategy.TemplateKey != "ai"
                              && !db.Notifications.Any(x => x.SignalId == signal.Id && x.Kind == NotificationKind.Signal && x.Channel == NotificationChannel.Push)
                          let repeat = db.Notifications.Any(x => x.Kind == NotificationKind.Signal && x.Channel == NotificationChannel.Push && x.SentAt > recent
                              && db.Signals.Any(o => o.Id == x.SignalId && o.StrategyId == signal.StrategyId && o.AssetId == signal.AssetId && o.Side == signal.Side))
                          select new { signal, strategy, user.Id, user.Locale, asset, repeat }).ToListAsync(cancellationToken);
        var sent = 0;
        var seen = new HashSet<(Guid, Guid, TradeSide)>();
        foreach (var row in rows.OrderBy(r => r.signal.CreatedAt))
        {
            if (await ClaimAsync(row.Id, row.signal.Id, NotificationKind.Signal, row.signal.SignalDate, cancellationToken, NotificationChannel.Push) is not { } claimed) continue;
            // Only a send that worked holds back the batch's other signal for the stock: after a failed one, the next is tried.
            if (row.repeat || seen.Contains(SameThing(row.signal)))
            {
                await NotSentAsync(claimed, "Not sent: this stock's alert went out today.");
                continue;
            }
            if (await PushAsync(claimed, row.Id, PushTexts.SignalAlert(Line(row.signal, row.strategy, row.asset), row.Locale), cancellationToken))
            {
                sent++;
                seen.Add(SameThing(row.signal));
            }
        }
        return sent;
    }

    /// <summary>
    /// "The buy signal has ended": once per channel, for each live buy signal of the last <see cref="AlertDays"/> days that ended
    /// without an answer (missed) because its rules no longer hold, after the user was alerted of it on that channel. Not for a
    /// signal closed because the strategy was archived or the stock unticked: the user did that.
    /// </summary>
    public async Task<int> SendSignalEndedAsync(DateOnly today, CancellationToken cancellationToken)
    {
        var since = today.AddDays(-AlertDays);
        var rows = await (from signal in db.Signals.AsNoTracking()
                          join strategy in db.Strategies.AsNoTracking() on signal.StrategyId equals strategy.Id
                          join user in db.Users.AsNoTracking() on strategy.UserId equals user.Id
                          join asset in db.Assets.AsNoTracking() on signal.AssetId equals asset.Id
                          where signal.Side == TradeSide.Buy && signal.Status == SignalStatus.Missed && signal.EndedOn >= since
                              && strategy.Mode == StrategyMode.Live && strategy.Status == StrategyStatus.Running && asset.Active
                              && db.StrategyAssets.Any(a => a.StrategyId == strategy.Id && a.AssetId == asset.Id && a.Active && a.RemovedAt == null)
                              && user.EmailVerifiedAt != null && user.AlertEmails
                              && strategy.TemplateKey != "ai"
                              && db.Notifications.Any(x => x.SignalId == signal.Id && x.Kind == NotificationKind.Signal && x.Channel == NotificationChannel.Email && x.SentAt != null)
                              && !db.Notifications.Any(x => x.SignalId == signal.Id && x.Kind == NotificationKind.SignalEnded && x.Channel == NotificationChannel.Email)
                          select new { signal, strategy, user.Id, user.Email, user.Locale, user.UnsubscribeToken, asset }).ToListAsync(cancellationToken);

        var sent = 0;
        foreach (var row in rows)
        {
            if (await ClaimAsync(row.Id, row.signal.Id, NotificationKind.SignalEnded, row.signal.EndedOn!.Value, cancellationToken) is not { } claimed) continue;
            if (await SendAsync(claimed, row.Email, () => Task.FromResult<EmailContent?>(EmailTexts.SignalEnded(Line(row.signal, row.strategy, row.asset), row.signal.EndedOn!.Value, AppUrl, row.Locale, row.UnsubscribeToken)), cancellationToken)) sent++;
        }
        return await SettledAsync(sent);
    }

    /// <summary>The push twin of <see cref="SendSignalEndedAsync"/>: to the user's devices, after the signal's own push.</summary>
    public async Task<int> PushSignalEndedAsync(DateOnly today, CancellationToken cancellationToken)
    {
        if (push.PublicKey is null) return 0;
        var since = today.AddDays(-AlertDays);
        var rows = await (from signal in db.Signals.AsNoTracking()
                          join strategy in db.Strategies.AsNoTracking() on signal.StrategyId equals strategy.Id
                          join user in db.Users.AsNoTracking() on strategy.UserId equals user.Id
                          join asset in db.Assets.AsNoTracking() on signal.AssetId equals asset.Id
                          where signal.Side == TradeSide.Buy && signal.Status == SignalStatus.Missed && signal.EndedOn >= since
                              && strategy.Mode == StrategyMode.Live && strategy.Status == StrategyStatus.Running && asset.Active
                              && db.StrategyAssets.Any(a => a.StrategyId == strategy.Id && a.AssetId == asset.Id && a.Active && a.RemovedAt == null)
                              && user.AlertPush && db.PushSubscriptions.Any(p => p.UserId == user.Id)
                              && strategy.TemplateKey != "ai"
                              && db.Notifications.Any(x => x.SignalId == signal.Id && x.Kind == NotificationKind.Signal && x.Channel == NotificationChannel.Push && x.SentAt != null)
                              && !db.Notifications.Any(x => x.SignalId == signal.Id && x.Kind == NotificationKind.SignalEnded && x.Channel == NotificationChannel.Push)
                          select new { signal, strategy, user.Id, user.Locale, asset }).ToListAsync(cancellationToken);
        var sent = 0;
        foreach (var row in rows)
        {
            if (await ClaimAsync(row.Id, row.signal.Id, NotificationKind.SignalEnded, row.signal.EndedOn!.Value, cancellationToken, NotificationChannel.Push) is not { } claimed) continue;
            if (await PushAsync(claimed, row.Id, PushTexts.SignalEnded(Line(row.signal, row.strategy, row.asset), row.signal.EndedOn!.Value, row.Locale), cancellationToken)) sent++;
        }
        return sent;
    }

    /// <summary>The push twin of <see cref="SendSnoozeRemindersAsync"/>, with the same morning rules.</summary>
    public async Task<int> PushSnoozeRemindersAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (push.PublicKey is null) return 0;
        var (earliest, latest) = ReminderDays(now);
        var rows = await (from signal in db.Signals.AsNoTracking()
                          join strategy in db.Strategies.AsNoTracking() on signal.StrategyId equals strategy.Id
                          join user in db.Users.AsNoTracking() on strategy.UserId equals user.Id
                          join asset in db.Assets.AsNoTracking() on signal.AssetId equals asset.Id
                          where signal.Status == SignalStatus.Waiting && signal.SnoozedUntil != null && signal.SnoozedUntil <= latest && signal.SnoozedUntil >= earliest
                              && strategy.Mode == StrategyMode.Live && user.AlertPush && strategy.Status == StrategyStatus.Running
                              && db.PushSubscriptions.Any(p => p.UserId == user.Id)
                              && strategy.TemplateKey != "ai"
                              && !db.Notifications.Any(x => x.SignalId == signal.Id && x.Kind == NotificationKind.Snoozed
                                  && x.Channel == NotificationChannel.Push && x.ForDate == signal.SnoozedUntil)
                          select new { signal, strategy, user.Id, user.TimeZone, user.Locale, asset }).ToListAsync(cancellationToken);
        var sent = 0;
        foreach (var row in rows)
        {
            var day = row.signal.SnoozedUntil!.Value;
            if (!IsMorningOf(day, LocalTime(now, row.TimeZone))) continue;
            if (await ClaimAsync(row.Id, row.signal.Id, NotificationKind.Snoozed, day, cancellationToken, NotificationChannel.Push) is not { } claimed) continue;
            if (await PushAsync(claimed, row.Id, PushTexts.SnoozeReminder(Line(row.signal, row.strategy, row.asset), row.Locale), cancellationToken)) sent++;
        }
        return sent;
    }

    /// <summary>The push twin of <see cref="SendAiRoundRemindersAsync"/>: one per user and due day.</summary>
    public async Task<int> PushAiRoundRemindersAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (push.PublicKey is null) return 0;
        var (earliest, latest) = ReminderDays(now);
        var rows = await (from round in db.AiRounds.AsNoTracking()
                          join strategy in db.Strategies.AsNoTracking() on round.StrategyId equals strategy.Id
                          join user in db.Users.AsNoTracking() on strategy.UserId equals user.Id
                          where round.Status == AiRoundStatus.Due && round.DueOn <= latest && round.DueOn >= earliest && round.DueOn > strategy.StartedOn
                              && user.AlertPush && strategy.Status == StrategyStatus.Running && db.PushSubscriptions.Any(p => p.UserId == user.Id)
                              && !db.Notifications.Any(x => x.UserId == user.Id && x.Kind == NotificationKind.AiRound
                                  && x.Channel == NotificationChannel.Push && x.ForDate == round.DueOn)
                          select new { round.DueOn, strategy.Id, strategy.Name, UserId = user.Id, user.TimeZone, user.Locale }).ToListAsync(cancellationToken);
        var sent = 0;
        foreach (var group in rows.GroupBy(r => (r.UserId, r.DueOn)))
        {
            var first = group.First();
            if (!IsMorningOf(group.Key.DueOn, LocalTime(now, first.TimeZone))) continue;
            if (await ClaimAsync(group.Key.UserId, null, NotificationKind.AiRound, group.Key.DueOn, cancellationToken, NotificationChannel.Push) is not { } claimed) continue;
            var strategies = group.OrderBy(r => r.Name).Select(r => (r.Id, r.Name)).ToList();
            if (await PushAsync(claimed, group.Key.UserId, PushTexts.AiRoundDue(strategies, group.Key.DueOn, first.Locale), cancellationToken)) sent++;
        }
        return sent;
    }

    /// <summary>
    /// The due days worth looking at: no time zone is more than a day from UTC, so later days can wait, and older ones
    /// (a day plus a day for time zones) are no longer reminded; the overview still lists them.
    /// </summary>
    static (DateOnly Earliest, DateOnly Latest) ReminderDays(DateTimeOffset now)
    {
        var utcDay = DateOnly.FromDateTime(now.UtcDateTime);
        return (utcDay.AddDays(-2), utcDay.AddDays(1));
    }

    /// <summary>From 07:00 of the day in the user's time zone, until the end of the next day (a day gone isn't reminded late).</summary>
    static bool IsMorningOf(DateOnly day, DateTimeOffset local)
    {
        var localDay = DateOnly.FromDateTime(local.DateTime);
        return !(localDay < day || (localDay == day && local.Hour < MorningHour) || localDay > day.AddDays(1));
    }

    static readonly TimeSpan FailingDeviceDays = TimeSpan.FromDays(7);
    const string PushAlertKey = "push:notifications";

    /// <summary>
    /// Sends a claimed push notification to every device of the user and records the result: sent if one device got it. A device
    /// the push service says is gone, or that has failed for a week, is forgotten. When none got it but one only failed this time
    /// (not a refusal, which would fail again every hour), the claim is given back for the hourly check to retry. No admin alert: devices come and go.
    /// </summary>
    async Task<bool> PushAsync(Guid notificationId, Guid userId, PushMessage message, CancellationToken cancellationToken)
    {
        var devices = await db.PushSubscriptions.AsNoTracking().Where(p => p.UserId == userId).ToListAsync(cancellationToken);
        var payload = message.ToJson();
        var now = DateTimeOffset.UtcNow;
        bool delivered = false, retry = false, refused = false;
        foreach (var device in devices)
        {
            PushOutcome outcome;
            try { outcome = await push.SendAsync(device, payload, cancellationToken); }
            // A shutdown mid-send gives the claim back: a device that got it already shows it once, by its tag.
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await ReleaseAsync(notificationId);
                throw;
            }
            catch (Exception error) when (error is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("Couldn't push notification {Id}: {Kind}", notificationId, error.GetType().Name);
                outcome = PushOutcome.Failed;
            }
            // A device failing for a week (a key it no longer matches, a push service that keeps refusing it) is gone too.
            var failing = outcome is PushOutcome.Failed or PushOutcome.Refused;
            if (failing && device.LastFailedAt < now - FailingDeviceDays) outcome = PushOutcome.Gone;
            delivered |= outcome == PushOutcome.Sent;
            retry |= outcome == PushOutcome.Failed;
            refused |= outcome == PushOutcome.Refused;
            var deviceRow = db.PushSubscriptions.Where(p => p.Id == device.Id);
            if (outcome == PushOutcome.Gone) await deviceRow.ExecuteDeleteAsync(CancellationToken.None);
            // LastFailedAt is when it started failing, so it is only set once and cleared by a delivery.
            else if (failing && device.LastFailedAt is null)
                await deviceRow.ExecuteUpdateAsync(p => p.SetProperty(x => x.LastFailedAt, now), CancellationToken.None);
            else if (outcome == PushOutcome.Sent && device.LastFailedAt is not null)
                await deviceRow.ExecuteUpdateAsync(p => p.SetProperty(x => x.LastFailedAt, (DateTimeOffset?)null), CancellationToken.None);
        }
        // A refusal can be the server's own setup (keys that don't match): the owner sees it on /admin/alerts, and it clears with the next delivery.
        if (refused) await alerts.RaiseAsync(AlertSource.Email, new NewAlert(PushAlertKey, "Web push refused",
            "A push service refused a notification for good (400, 403 or 413). One device: it may be gone. All of them: check VAPID_PUBLIC_KEY and VAPID_PRIVATE_KEY are a pair."), CancellationToken.None);
        else if (delivered) await alerts.ClearAsync(PushAlertKey, CancellationToken.None);
        // None got it but one may later (a push service down, a timeout): give the claim back, so the hourly check tries again.
        if (!delivered && retry)
        {
            await ReleaseAsync(notificationId);
            return false;
        }
        await db.Notifications.Where(n => n.Id == notificationId)
            .ExecuteUpdateAsync(n => n.SetProperty(x => x.SentAt, delivered ? now : (DateTimeOffset?)null)
                .SetProperty(x => x.FailedAt, delivered ? (DateTimeOffset?)null : now)
                .SetProperty(x => x.Error, delivered ? null : "No device got it."), CancellationToken.None);
        db.ChangeTracker.Clear();
        return delivered;
    }

    static DateTimeOffset LocalTime(DateTimeOffset now, string timeZone)
    {
        try { return TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(timeZone)); }
        catch (Exception error) when (error is TimeZoneNotFoundException or InvalidTimeZoneException) { return now; } // UTC, as the dashboard does.
    }

    record SummaryData(
        IReadOnlyList<Strategy> Strategies,
        IReadOnlyList<SignalLine> Waiting,
        IReadOnlyList<SignalLine> Changed,
        IReadOnlyList<StockChangeLine> StockChanges,
        IReadOnlyList<ScoreLine> Scoreboard,
        IReadOnlyList<(Guid StrategyId, string Name)> AiRoundsDue,
        bool HasChanges,
        bool Complete);

    async Task<SummaryData> LoadSummaryDataAsync(Guid userId, DateOnly today, DateOnly sessionDate, CancellationToken cancellationToken)
    {
        var strategies = await db.Strategies.AsNoTracking().Where(s => s.UserId == userId && s.Status == StrategyStatus.Running).ToListAsync(cancellationToken);
        var ids = strategies.Select(s => s.Id).ToList();
        if (ids.Count == 0)
            return new SummaryData([], [], [], [], [], [], false, true);

        var signals = await (from signal in db.Signals.AsNoTracking()
                             join asset in db.Assets.AsNoTracking() on signal.AssetId equals asset.Id
                             where ids.Contains(signal.StrategyId) && (signal.Status == SignalStatus.Waiting || signal.SignalDate == sessionDate)
                             select new { signal, asset }).ToListAsync(cancellationToken);
        var byId = strategies.ToDictionary(s => s.Id);
        SignalLine ToLine(Signal s, Asset a) => Line(s, byId[s.StrategyId], a);
        var waiting = signals.Where(x => x.signal.Status == SignalStatus.Waiting).Select(x => ToLine(x.signal, x.asset)).OrderBy(l => l.Date).ToList();
        var changed = signals.Where(x => x.signal.SignalDate == sessionDate && x.signal.Status != SignalStatus.Waiting).Select(x => ToLine(x.signal, x.asset)).ToList();

        var recent = await (from eval in db.DailyEvaluations.AsNoTracking()
                            join asset in db.Assets.AsNoTracking() on eval.AssetId equals asset.Id
                            where ids.Contains(eval.StrategyId) && eval.Date >= sessionDate.AddDays(-RecentDays)
                            select new { eval, asset.Name }).ToListAsync(cancellationToken);
        var closePct = (await templates.AllAsync(cancellationToken)).ToDictionary(t => t.Key, t => t.ClosePct);
        var stockChanges = StockChangeLines(recent.Select(x => (x.eval, x.Name)), sessionDate, byId, closePct);
        var hasChanges = stockChanges.Count > 0 || signals.Any(x => x.signal.SignalDate == sessionDate);
        var complete = hasChanges || strategies.Where(s => s.StartedOn <= sessionDate
                && (s.StartedOn >= sessionDate.AddDays(-RecentDays) || recent.Any(x => x.eval.StrategyId == s.Id)))
            .All(s => recent.Any(x => x.eval.StrategyId == s.Id && x.eval.Date == sessionDate));

        var roundsDue = await db.AiRounds.AsNoTracking().Where(r => ids.Contains(r.StrategyId) && r.Status == AiRoundStatus.Due && r.DueOn <= today)
            .OrderBy(r => r.DueOn).Select(r => new { r.StrategyId, Name = byId[r.StrategyId].Name }).ToListAsync(cancellationToken);
        var counts = await db.Signals.AsNoTracking().Where(s => ids.Contains(s.StrategyId)).GroupBy(s => s.StrategyId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);
        var scoreboard = strategies.OrderBy(s => s.Name).Select(s => new ScoreLine(s.Name, s.Mode, s.StartedOn, counts.GetValueOrDefault(s.Id))).ToList();

        return new SummaryData(strategies, waiting, changed, stockChanges, scoreboard, [.. roundsDue.Select(r => (r.StrategyId, r.Name))], hasChanges, complete);
    }

    /// <summary>
    /// The summary's email, or null on a quiet day: no signal on the session and no stock change. <c>Complete</c> says whether every
    /// running strategy the daily check has been checking (any day of the recent window) was checked for the session, so a
    /// quiet verdict is final.
    /// </summary>
    async Task<(EmailContent? Content, bool Complete)> ComposeSummaryAsync(Guid userId, DateOnly today, DateOnly lastSession, string locale, string unsubscribeToken, CancellationToken cancellationToken)
    {
        var data = await LoadSummaryDataAsync(userId, today, lastSession, cancellationToken);
        if (!data.HasChanges)
            return (null, data.Complete);

        return (EmailTexts.Overview(lastSession, data.Waiting, data.Changed, data.Scoreboard, AppUrl, locale, data.AiRoundsDue, unsubscribeToken, data.StockChanges), true);
    }

    /// <summary>
    /// Summary data for the dedicated daily summary page, up to date from the most recent price check session.
    /// </summary>
    public async Task<DailySummaryDto> GetDailySummaryAsync(Guid userId, string timeZone, string locale, TimeProvider time, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var lastSession = UsSession.LastComplete(now);
        var runningStrategies = await db.Strategies.AsNoTracking().Where(s => s.UserId == userId && s.Status == StrategyStatus.Running).ToListAsync(cancellationToken);
        if (runningStrategies.Count == 0)
            return new DailySummaryDto(lastSession, false, [], [], [], [], []);

        var ids = runningStrategies.Select(s => s.Id).ToList();
        var latestEval = await db.DailyEvaluations.AsNoTracking().Where(e => ids.Contains(e.StrategyId)).MaxAsync(e => (DateOnly?)e.Date, cancellationToken);
        var sessionDate = latestEval is not null && latestEval < lastSession ? latestEval.Value : lastSession;

        var local = LocalTime(now, timeZone);
        var today = DateOnly.FromDateTime(local.DateTime);

        var data = await LoadSummaryDataAsync(userId, today, sessionDate, cancellationToken);
        var counts = await db.Signals.AsNoTracking().Where(s => ids.Contains(s.StrategyId)).GroupBy(s => s.StrategyId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);

        return new DailySummaryDto(
            sessionDate,
            data.HasChanges,
            [.. data.Waiting.Select(w => new SummaryWaitingSignal(w.SignalId, w.StrategyId, w.StrategyName, w.StockName, w.Side, w.Date, w.StepNo, w.MaxSteps, w.ClosePrice))],
            [.. data.AiRoundsDue.Select(r => new SummaryWaitingAiRound(r.StrategyId, r.Name))],
            [.. data.Changed.Select(c => new SummaryChangedSignal(c.SignalId, c.StrategyId, c.StrategyName, c.Mode, c.StockName, c.Side, c.Date, c.ClosePrice, c.StepNo, c.MaxSteps, EmailTexts.SignalText(c, locale).Replace("\n", " ")))],
            [.. data.StockChanges.Select(c => new SummaryStockChange(c.StrategyId, c.AssetId, c.StrategyName, c.Mode, c.StockName, c.Kind, c.DistancePct, EmailTexts.StockChangeText(c, locale)))],
            [.. runningStrategies.OrderBy(s => s.Name).Select(s => new SummaryStrategyScore(s.Id, s.Name, s.Mode, s.StartedOn, counts.GetValueOrDefault(s.Id)))]);
    }

    /// <summary>
    /// The stock changes of every running strategy: each stock evaluated on the last session against its latest
    /// earlier evaluation of the recent days (see <see cref="StockChanges.Latest"/>). A stock without an earlier day (just added) has nothing to compare.
    /// </summary>
    static List<StockChangeLine> StockChangeLines(IEnumerable<(DailyEvaluation Eval, string Name)> recent, DateOnly lastSession, Dictionary<Guid, Strategy> strategies,
        Dictionary<string, decimal> closePct) =>
        recent.GroupBy(x => (x.Eval.StrategyId, x.Eval.AssetId))
            .Select(g => g.OrderByDescending(x => x.Eval.Date).ToList())
            .Where(days => days[0].Eval.Date == lastSession)
            .Select(days => (days, change: StockChanges.Latest([.. days.Select(d => (d.Eval.Status, d.Eval.Facts.RootElement))],
                closePct[strategies[days[0].Eval.StrategyId].TemplateKey])))
            .Where(x => x.change is not null)
            .Select(x =>
            {
                var strategy = strategies[x.days[0].Eval.StrategyId];
                return new StockChangeLine(strategy.Id, x.days[0].Eval.AssetId, strategy.Name, strategy.Mode, strategy.TemplateKey, x.days[0].Name, x.change!.Value.Kind, x.change.Value.DistancePct);
            })
            .OrderBy(c => c.StrategyName).ThenBy(c => c.StockName).ToList();

    static SignalLine Line(Signal signal, Strategy strategy, Asset asset) => SignalLine.From(signal, strategy, asset);

    /// <summary>What makes two signals the same thing for an alert: strategy, stock and side.</summary>
    static (Guid, Guid, TradeSide) SameThing(Signal signal) => (signal.StrategyId, signal.AssetId, signal.Side);

    /// <summary>Takes the right to send this notification, once: its id, or null when it was already claimed (a retry, a second run).</summary>
    async Task<Guid?> ClaimAsync(Guid userId, Guid? signalId, NotificationKind kind, DateOnly forDate, CancellationToken cancellationToken,
        NotificationChannel channel = NotificationChannel.Email)
    {
        var notification = new Notification { UserId = userId, SignalId = signalId, Kind = kind, Channel = channel, ForDate = forDate };
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
    /// Any other error is recorded and does not stop the others. A compose that returns null (a quiet day's summary) sends nothing,
    /// and the claim stays with that reason, so later runs don't look again.
    /// </summary>
    async Task<bool> SendAsync(Guid notificationId, string to, Func<Task<EmailContent?>> compose, CancellationToken cancellationToken)
    {
        string? error = null;
        var shutDown = false;
        try
        {
            if (await compose() is not { } content)
            {
                await db.Notifications.Where(n => n.Id == notificationId)
                    .ExecuteUpdateAsync(n => n.SetProperty(x => x.Error, "Not sent: nothing changed."), CancellationToken.None);
                db.ChangeTracker.Clear();
                return false;
            }
            await sender.SendAsync(new OutgoingEmail(to, content), cancellationToken);
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
            if (await db.Notifications.AnyAsync(n => n.Channel == NotificationChannel.Email && n.FailedAt > since, CancellationToken.None)) return sent;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogWarning("The email alert couldn't be checked: {Reason}", error.Message);
            return sent;
        }
        await alerts.ClearAsync(EmailAlertKey, CancellationToken.None);
        return sent;
    }

    /// <summary>Keeps the claim without sending, so no later run sends it, and says why.</summary>
    async Task NotSentAsync(Guid notificationId, string reason)
    {
        await db.Notifications.Where(n => n.Id == notificationId)
            .ExecuteUpdateAsync(n => n.SetProperty(x => x.Error, reason), CancellationToken.None);
        db.ChangeTracker.Clear();
    }

    /// <summary>Gives the claim back: nothing was sent, so the next run may send it.</summary>
    async Task ReleaseAsync(Guid notificationId)
    {
        await db.Notifications.Where(n => n.Id == notificationId).ExecuteDeleteAsync(CancellationToken.None);
        db.ChangeTracker.Clear();
    }
}
