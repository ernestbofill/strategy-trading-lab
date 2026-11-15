using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Admin;
using StrategyTradingLab.Api.Auth;
using StrategyTradingLab.Api.Common;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Email;
using StrategyTradingLab.Api.Evaluation;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Notifications;

/// <summary>
/// Sends the notifications (spec: Notifications), by email and by push: the live signal alert and its end, the summary of the
/// daily check, snoozed signals' and AI rounds' reminders, and the stock change pushes. Emails wait for the user's notification
/// hour (<see cref="NotifyTime"/>); pushes go as soon as the check has run; reminders come at the reminder hour on both. Each
/// goes out once per channel (<see cref="NotificationDelivery"/>): emails only to verified addresses with alert emails on, pushes
/// to the devices of a user with push on.
/// </summary>
public class NotificationService(AppDbContext db, IEmailSender sender, IPushSender push, SettingsStore settings, IConfiguration configuration, AdminAlerts alerts,
    DailySummary summary, TimeProvider time, ILogger<NotificationService> logger)
{
    readonly NotificationDelivery delivery = new(db, sender, push, alerts, time, logger);

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
    /// The users a channel reaches: by email, a confirmed address with alert emails on; by push, push on and a device (no confirmed
    /// email needed, and the email switches don't count: push has its own).
    /// </summary>
    IQueryable<User> Reachable(NotificationChannel channel) =>
        channel == NotificationChannel.Email
            ? db.Users.AsNoTracking().Where(u => u.EmailVerifiedAt != null && u.AlertEmails)
            : db.Users.AsNoTracking().Where(u => u.AlertPush && db.PushSubscriptions.Any(p => p.UserId == u.Id));

    static Recipient To(User user) => new(user.Id, user.Email, user.Locale, user.UnsubscribeToken, user.TimeZone, user.NotifyHour);

    /// <summary>Whether a channel can send at all: push needs its keys.</summary>
    bool Ready(NotificationChannel channel) => channel == NotificationChannel.Email || delivery.PushReady;

    public Task<int> SendSignalAlertsAsync(DateOnly today, CancellationToken cancellationToken) => SignalAlertsAsync(NotificationChannel.Email, today, cancellationToken);
    public Task<int> PushSignalAlertsAsync(DateOnly today, CancellationToken cancellationToken) => SignalAlertsAsync(NotificationChannel.Push, today, cancellationToken);
    public Task<int> SendSignalEndedAsync(DateOnly today, CancellationToken cancellationToken) => SignalEndedAsync(NotificationChannel.Email, today, cancellationToken);
    public Task<int> PushSignalEndedAsync(DateOnly today, CancellationToken cancellationToken) => SignalEndedAsync(NotificationChannel.Push, today, cancellationToken);
    public Task<int> SendSnoozeRemindersAsync(DateTimeOffset now, CancellationToken cancellationToken) => SnoozeRemindersAsync(NotificationChannel.Email, now, cancellationToken);
    public Task<int> PushSnoozeRemindersAsync(DateTimeOffset now, CancellationToken cancellationToken) => SnoozeRemindersAsync(NotificationChannel.Push, now, cancellationToken);
    public Task<int> SendAiRoundRemindersAsync(DateTimeOffset now, CancellationToken cancellationToken) => AiRoundRemindersAsync(NotificationChannel.Email, now, cancellationToken);
    public Task<int> PushAiRoundRemindersAsync(DateTimeOffset now, CancellationToken cancellationToken) => AiRoundRemindersAsync(NotificationChannel.Push, now, cancellationToken);

    /// <summary>
    /// An alert for each live signal of the last <see cref="AlertDays"/> days that is still waiting and has none yet on the
    /// channel. An email waits for the user's notification hour (<see cref="NotifyTime.AlertDueAt"/>); a push goes as soon as the
    /// check finds it. Looking back (instead of at the signals a run just made) is what retries an alert a run couldn't send
    /// (email not set up yet, a push service down, a shutdown) without ever sending one twice.
    /// </summary>
    async Task<int> SignalAlertsAsync(NotificationChannel channel, DateOnly today, CancellationToken cancellationToken)
    {
        if (!Ready(channel)) return 0;
        var now = time.GetUtcNow();
        var recent = now - RepeatWindow;
        var since = today.AddDays(-AlertDays);
        var rows = await (from signal in db.Signals.AsNoTracking()
                          join strategy in db.Strategies.AsNoTracking() on signal.StrategyId equals strategy.Id
                          join user in Reachable(channel) on strategy.UserId equals user.Id
                          join asset in db.Assets.AsNoTracking() on signal.AssetId equals asset.Id
                          where signal.SignalDate >= since && signal.Status == SignalStatus.Waiting && strategy.Mode == StrategyMode.Live
                              && strategy.Status == StrategyStatus.Running && strategy.TemplateKey != TemplateKeys.Ai
                              && !db.Notifications.Any(x => x.SignalId == signal.Id && x.Kind == NotificationKind.Signal && x.Channel == channel)
                          // A second check the same day (a re-run, a price that came in late) can signal the same stock again: it was alerted already.
                          let repeat = db.Notifications.Any(x => x.Kind == NotificationKind.Signal && x.Channel == channel && x.SentAt > recent
                              && db.Signals.Any(o => o.Id == x.SignalId && o.StrategyId == signal.StrategyId && o.AssetId == signal.AssetId && o.Side == signal.Side))
                          select new { signal, strategy, user, asset, repeat }).ToListAsync(cancellationToken);

        var sent = 0;
        var seen = new HashSet<(Guid, Guid, TradeSide)>();
        var due = rows.Where(r => channel == NotificationChannel.Push || now >= NotifyTime.AlertDueAt(r.signal.SignalDate, r.user.NotifyHour, r.user.TimeZone));
        foreach (var row in due.OrderBy(r => r.signal.CreatedAt))
        {
            if (await delivery.ClaimAsync(row.user.Id, row.signal.Id, NotificationKind.Signal, row.signal.SignalDate, cancellationToken, channel) is not { } claimed) continue;
            // Only a send that worked holds back the batch's other signal for the stock: after a failed one, the next is tried.
            if (row.repeat || seen.Contains(SameThing(row.signal)))
            {
                await delivery.NotSentAsync(claimed, "Not sent: this stock's alert went out today.");
                continue;
            }
            var line = Line(row.signal, row.strategy, row.asset);
            var to = To(row.user);
            if (await delivery.DeliverAsync(channel, claimed, to, () => EmailTexts.SignalAlert(line, AppUrl, to.Locale, to.UnsubscribeToken),
                    () => PushTexts.SignalAlert(line, to.Locale), cancellationToken))
            {
                sent++;
                seen.Add(SameThing(row.signal));
            }
        }
        return await delivery.SettledAsync(channel, sent);
    }

    /// <summary>
    /// "The buy signal has ended": once per channel, for each live buy signal of the last <see cref="AlertDays"/> days that ended
    /// without an answer (missed) because its rules no longer hold, after the user was alerted of it on that channel. Not for a
    /// signal closed because the strategy was archived or the stock unticked: the user did that. An email waits for the
    /// notification hour; a push goes as soon as the check ends it.
    /// </summary>
    async Task<int> SignalEndedAsync(NotificationChannel channel, DateOnly today, CancellationToken cancellationToken)
    {
        if (!Ready(channel)) return 0;
        var since = today.AddDays(-AlertDays);
        var rows = await (from signal in db.Signals.AsNoTracking()
                          join strategy in db.Strategies.AsNoTracking() on signal.StrategyId equals strategy.Id
                          join user in Reachable(channel) on strategy.UserId equals user.Id
                          join asset in db.Assets.AsNoTracking() on signal.AssetId equals asset.Id
                          where signal.Side == TradeSide.Buy && signal.Status == SignalStatus.Missed && signal.EndedOn >= since
                              && strategy.Mode == StrategyMode.Live && strategy.Status == StrategyStatus.Running && asset.Active
                              && db.StrategyAssets.Any(a => a.StrategyId == strategy.Id && a.AssetId == asset.Id && a.Active && a.RemovedAt == null)
                              && strategy.TemplateKey != TemplateKeys.Ai
                              && db.Notifications.Any(x => x.SignalId == signal.Id && x.Kind == NotificationKind.Signal && x.Channel == channel && x.SentAt != null)
                              && !db.Notifications.Any(x => x.SignalId == signal.Id && x.Kind == NotificationKind.SignalEnded && x.Channel == channel)
                          select new { signal, strategy, user, asset }).ToListAsync(cancellationToken);

        var now = time.GetUtcNow();
        var sent = 0;
        foreach (var row in rows.Where(r => channel == NotificationChannel.Push || now >= NotifyTime.AlertDueAt(r.signal.EndedOn!.Value, r.user.NotifyHour, r.user.TimeZone)))
        {
            var endedOn = row.signal.EndedOn!.Value;
            if (await delivery.ClaimAsync(row.user.Id, row.signal.Id, NotificationKind.SignalEnded, endedOn, cancellationToken, channel) is not { } claimed) continue;
            var line = Line(row.signal, row.strategy, row.asset);
            var to = To(row.user);
            if (await delivery.DeliverAsync(channel, claimed, to, () => EmailTexts.SignalEnded(line, endedOn, AppUrl, to.Locale, to.UnsubscribeToken),
                    () => PushTexts.SignalEnded(line, endedOn, to.Locale), cancellationToken)) sent++;
        }
        return await delivery.SettledAsync(channel, sent);
    }

    /// <summary>
    /// The summary of the last finished US session, once, to every user the daily check has evaluated that session for, on days
    /// something changed (a signal or a stock change; a quiet day sends nothing, and its claim says so): sent right
    /// after the check, and by the hourly check for whoever wasn't sent it then (email not set up yet, a restart, an address just
    /// confirmed). It waits for the user's notification hour (<see cref="NotifyTime.DueAt"/>). One per trading day, so a weekend
    /// sends Friday's once and not three times.
    /// </summary>
    public async Task<int> SendOverviewsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var lastSession = UsSession.LastComplete(now);
        // A summary held for an hour close to the next session's close gets few hourly runs before that close, or none (a time zone
        // 45 minutes off the hour): the previous session's is still looked at for those, while it is unclaimed.
        var lateFrom = UsSession.FinalAt(lastSession) - LateSummaryWindow;
        var sent = await SendOverviewsForAsync(UsSession.Before(lastSession), now, due => due >= lateFrom, cancellationToken)
            + await SendOverviewsForAsync(lastSession, now, _ => true, cancellationToken);
        return await delivery.SettledAsync(sent);
    }

    /// <summary>How close to the next session's close a summary's time can come and still be sent after that close.</summary>
    static readonly TimeSpan LateSummaryWindow = TimeSpan.FromHours(2);

    async Task<int> SendOverviewsForAsync(DateOnly lastSession, DateTimeOffset now, Func<DateTimeOffset, bool> looked, CancellationToken cancellationToken)
    {
        var users = await db.Users.AsNoTracking()
            .Where(u => u.OverviewEmail && u.EmailVerifiedAt != null && db.Strategies.Any(s => s.UserId == u.Id && s.Status == StrategyStatus.Running)
                && !db.Notifications.Any(x => x.UserId == u.Id && x.Kind == NotificationKind.Overview && x.Channel == NotificationChannel.Email && x.ForDate == lastSession)
                // Only once the daily check has evaluated that session for them: an overview of stale data would say nothing happened.
                && db.Strategies.Any(s => s.UserId == u.Id && db.DailyEvaluations.Any(e => e.StrategyId == s.Id && e.Date == lastSession)))
            .Select(u => new { u.Id, u.Email, u.TimeZone, u.Locale, u.UnsubscribeToken, u.NotifyHour }).ToListAsync(cancellationToken);
        var sent = 0;
        foreach (var user in users)
        {
            var due = NotifyTime.DueAt(lastSession, user.NotifyHour, user.TimeZone);
            if (now < due || !looked(due)) continue;
            if (await delivery.ClaimAsync(user.Id, null, NotificationKind.Overview, lastSession, cancellationToken) is not { } claimed) continue;

            // AI rounds due count by the user's own date, which may already be the day after the session.
            var today = UserClock.Today(user.TimeZone, now);
            // A quiet day is only final once every strategy has been checked for the session: until then (the daily check still
            // running, a strategy that couldn't be checked yet), the claim is given back and a later run looks again.
            var incomplete = false;
            if (await delivery.SendAsync(claimed, user.Email, async () =>
                {
                    var (content, complete) = await ComposeSummaryAsync(user.Id, today, lastSession, user.Locale, user.UnsubscribeToken, cancellationToken);
                    incomplete = content is null && !complete;
                    return content;
                }, cancellationToken)) sent++;
            else if (incomplete) await delivery.ReleaseAsync(claimed);
        }
        return sent;
    }

    /// <summary>
    /// The reminder of each snoozed signal, once per channel, from the reminder hour (<see cref="NotifyTime.ReminderHour"/>) of its
    /// snooze day in the user's time zone, while it still waits and its strategy runs (spec: Snooze). Snoozing again sets a later
    /// day, which gets its own reminder.
    /// </summary>
    async Task<int> SnoozeRemindersAsync(NotificationChannel channel, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!Ready(channel)) return 0;
        var (earliest, latest) = ReminderDays(now);
        var rows = await (from signal in db.Signals.AsNoTracking()
                          join strategy in db.Strategies.AsNoTracking() on signal.StrategyId equals strategy.Id
                          join user in Reachable(channel) on strategy.UserId equals user.Id
                          join asset in db.Assets.AsNoTracking() on signal.AssetId equals asset.Id
                          where signal.Status == SignalStatus.Waiting && signal.SnoozedUntil != null && signal.SnoozedUntil <= latest && signal.SnoozedUntil >= earliest
                              && strategy.Mode == StrategyMode.Live && strategy.Status == StrategyStatus.Running && strategy.TemplateKey != TemplateKeys.Ai
                              && !db.Notifications.Any(x => x.SignalId == signal.Id && x.Kind == NotificationKind.Snoozed
                                  && x.Channel == channel && x.ForDate == signal.SnoozedUntil)
                          select new { signal, strategy, user, asset }).ToListAsync(cancellationToken);

        var sent = 0;
        foreach (var row in rows)
        {
            var day = row.signal.SnoozedUntil!.Value;
            if (!IsMorningOf(day, UserClock.LocalNow(now, row.user.TimeZone), NotifyTime.ReminderHour(row.user.NotifyHour))) continue;
            if (await delivery.ClaimAsync(row.user.Id, row.signal.Id, NotificationKind.Snoozed, day, cancellationToken, channel) is not { } claimed) continue;
            var line = Line(row.signal, row.strategy, row.asset);
            var to = To(row.user);
            if (await delivery.DeliverAsync(channel, claimed, to, () => EmailTexts.SnoozeReminder(line, AppUrl, to.Locale, to.UnsubscribeToken),
                    () => PushTexts.SnoozeReminder(line, to.Locale), cancellationToken)) sent++;
        }
        return await delivery.SettledAsync(channel, sent);
    }

    /// <summary>
    /// "Time for the AI round", once per user, due day and channel, from the reminder hour of that day in the user's time zone
    /// (spec: Notifications, AI round due), listing every round of theirs due that day. Not for a strategy's first round, which is
    /// due the day it starts (Confirm opens it), nor for a round whose week has run out.
    /// </summary>
    async Task<int> AiRoundRemindersAsync(NotificationChannel channel, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!Ready(channel)) return 0;
        var (earliest, latest) = ReminderDays(now);
        var rows = await (from round in db.AiRounds.AsNoTracking()
                          join strategy in db.Strategies.AsNoTracking() on round.StrategyId equals strategy.Id
                          join user in Reachable(channel) on strategy.UserId equals user.Id
                          where round.Status == AiRoundStatus.Due && round.DueOn <= latest && round.DueOn >= earliest && round.DueOn > strategy.StartedOn
                              && strategy.Status == StrategyStatus.Running
                              && !db.Notifications.Any(x => x.UserId == user.Id && x.Kind == NotificationKind.AiRound
                                  && x.Channel == channel && x.ForDate == round.DueOn)
                          select new { round.DueOn, strategy.Id, strategy.Name, user }).ToListAsync(cancellationToken);

        var sent = 0;
        foreach (var group in rows.GroupBy(r => (r.user.Id, r.DueOn)))
        {
            var to = To(group.First().user);
            if (!IsMorningOf(group.Key.DueOn, UserClock.LocalNow(now, to.TimeZone), NotifyTime.ReminderHour(to.NotifyHour))) continue;
            if (await delivery.ClaimAsync(to.Id, null, NotificationKind.AiRound, group.Key.DueOn, cancellationToken, channel) is not { } claimed) continue;
            var strategies = group.OrderBy(r => r.Name).Select(r => (r.Id, r.Name)).ToList();
            if (await delivery.DeliverAsync(channel, claimed, to, () => EmailTexts.AiRoundDue(strategies, AppUrl, to.Locale, to.UnsubscribeToken),
                    () => PushTexts.AiRoundDue(strategies, group.Key.DueOn, to.Locale), cancellationToken)) sent++;
        }
        return await delivery.SettledAsync(channel, sent);
    }

    /// <summary>
    /// A push for each stock of a running strategy (both modes) whose check of the last session made it a candidate or brought it
    /// close to its line (<see cref="StockChanges.Latest"/>), to the devices of a user with push on, as soon as the check has run.
    /// Once per stock, kind and session: a check run again on the same session, or the next one finding the stock as it was, sends
    /// nothing; it is pushed again only after it changes state again (spec: Notifications, Stock changes).
    /// </summary>
    public async Task<int> PushStockChangesAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!delivery.PushReady) return 0;
        var session = UsSession.LastComplete(now);
        var strategies = await db.Strategies.AsNoTracking()
            .Where(s => s.Status == StrategyStatus.Running && db.Users.Any(u => u.Id == s.UserId && u.AlertPush)
                && db.PushSubscriptions.Any(p => p.UserId == s.UserId)
                && db.DailyEvaluations.Any(e => e.StrategyId == s.Id && e.Date == session))
            .ToListAsync(cancellationToken);
        if (strategies.Count == 0) return 0;
        var byId = strategies.ToDictionary(s => s.Id);
        var ids = byId.Keys.ToList();
        var (_, changes) = await summary.StockChangesAsync(byId, session, cancellationToken);
        // Those already pushed for the session: the unique key would refuse them anyway, but this spares every hourly run a failed insert per stock.
        var pushed = (await db.Notifications.AsNoTracking()
                .Where(n => n.Channel == NotificationChannel.Push && n.ForDate == session && n.StrategyId != null && ids.Contains(n.StrategyId.Value))
                .Select(n => new { n.Kind, n.StrategyId, n.AssetId }).ToListAsync(cancellationToken))
            .Select(n => (n.Kind, n.StrategyId!.Value, n.AssetId!.Value)).ToHashSet();
        var userIds = strategies.Select(s => s.UserId).Distinct().ToList();
        var locales = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Locale, cancellationToken);

        var sent = 0;
        foreach (var change in changes)
        {
            // No longer a candidate isn't pushed: there is nothing to look at.
            NotificationKind? kind = change.Kind switch
            {
                StockChangeKind.BecameCandidate => NotificationKind.StockCandidate,
                StockChangeKind.CameClose => NotificationKind.StockClose,
                _ => null,
            };
            if (kind is not { } k || pushed.Contains((k, change.StrategyId, change.AssetId))) continue;
            var userId = byId[change.StrategyId].UserId;
            if (await delivery.ClaimAsync(userId, null, k, session, cancellationToken, NotificationChannel.Push, (change.StrategyId, change.AssetId)) is not { } claimed) continue;
            if (await delivery.PushAsync(claimed, userId, PushTexts.StockChange(change, locales[userId]), cancellationToken)) sent++;
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

    /// <summary>From the reminder hour of the day in the user's time zone, until the end of the next day (a day gone isn't reminded late).</summary>
    static bool IsMorningOf(DateOnly day, DateTimeOffset local, int hour)
    {
        var localDay = DateOnly.FromDateTime(local.DateTime);
        return !(localDay < day || (localDay == day && local.Hour < hour) || localDay > day.AddDays(1));
    }

    /// <summary>
    /// The summary's email, or null on a quiet day: no signal on the session and no stock change. <c>Complete</c> says whether every
    /// running strategy the daily check has been checking (any day of the recent window) was checked for the session, so a
    /// quiet verdict is final.
    /// </summary>
    async Task<(EmailContent? Content, bool Complete)> ComposeSummaryAsync(Guid userId, DateOnly today, DateOnly lastSession, string locale, string unsubscribeToken, CancellationToken cancellationToken)
    {
        var data = await summary.LoadAsync(userId, today, lastSession, cancellationToken);
        if (!data.HasChanges)
            return (null, data.Complete);

        return (EmailTexts.Overview(lastSession, data.Waiting, data.Changed, data.Scoreboard, AppUrl, locale, data.AiRoundsDue, unsubscribeToken, data.StockChanges), true);
    }

    static SignalLine Line(Signal signal, Strategy strategy, Asset asset) => SignalLine.From(signal, strategy, asset);

    /// <summary>What makes two signals the same thing for an alert: strategy, stock and side.</summary>
    static (Guid, Guid, TradeSide) SameThing(Signal signal) => (signal.StrategyId, signal.AssetId, signal.Side);
}
