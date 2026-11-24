using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Admin;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Email;

namespace StrategyTradingLab.Api.Notifications;

/// <summary>Who a notification goes to: by email (a verified address) or to their devices.</summary>
public record Recipient(Guid Id, string Email, string Locale, string UnsubscribeToken, string TimeZone, int? NotifyHour);

/// <summary>
/// The bookkeeping of one scope's notifications (spec: Notifications): each is claimed first, a row in <c>notifications</c>
/// whose unique key is the dedupe contract, then sent by email or push, and the row says whether it worked. Emails that fail
/// raise the admin alert, which clears after a batch where every email went out. A user's pushes go out at least
/// <paramref name="pushGap"/> apart.
/// </summary>
public class NotificationDelivery(AppDbContext db, IEmailSender sender, IPushSender push, AdminAlerts alerts, TimeProvider time, ILogger logger, TimeSpan pushGap)
{
    /// <summary>Whether web push is set up (its keys): without them nothing is pushed.</summary>
    public bool PushReady => push.PublicKey is not null;

    /// <summary>Sends a claimed notification on its channel: the email, or the push to every device.</summary>
    public Task<bool> DeliverAsync(NotificationChannel channel, Guid claimed, Recipient to, Func<EmailContent> email, Func<PushMessage> pushed,
        CancellationToken cancellationToken) =>
        channel == NotificationChannel.Email
            ? SendAsync(claimed, to.Email, () => Task.FromResult<EmailContent?>(email()), cancellationToken)
            : PushAsync(claimed, to.Id, pushed(), cancellationToken);

    /// <summary>The end of a batch on a channel (<see cref="SettledAsync(int)"/> for email). Returns how many were sent.</summary>
    public Task<int> SettledAsync(NotificationChannel channel, int sent) =>
        channel == NotificationChannel.Email ? SettledAsync(sent) : Task.FromResult(sent);

    static readonly TimeSpan FailingDeviceDays = TimeSpan.FromDays(7);
    const string PushAlertKey = "push:notifications";

    /// <summary>
    /// Sends a claimed push notification to every device of the user and records the result: sent if one device got it. A device
    /// the push service says is gone, or that has failed for a week, is forgotten. When none got it but one only failed this time
    /// (not a refusal, which would fail again every hour), the claim is given back for the hourly check to retry. No admin alert: devices come and go.
    /// It waits until the user's last push is <c>pushGap</c> old: several at once seemed to reach a phone as one (Ernest, 2026-11-22).
    /// One push at a time in the API, wait included, so the check's pushes and the hourly run's can't go out together.
    /// </summary>
    public async Task<bool> PushAsync(Guid notificationId, Guid userId, PushMessage message, CancellationToken cancellationToken)
    {
        var locked = false;
        try
        {
            await PushLock.WaitAsync(cancellationToken);
            locked = true;
            return await SendPushAsync(notificationId, userId, message, cancellationToken);
        }
        // A shutdown before or during the send gives the claim back: a device that got it already shows it once, by its tag.
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await ReleaseAsync(notificationId);
            throw;
        }
        finally
        {
            if (locked) PushLock.Release();
        }
    }

    /// <summary>One API process: an in-process lock is enough to keep a user's pushes apart.</summary>
    static readonly SemaphoreSlim PushLock = new(1, 1);

    async Task<bool> SendPushAsync(Guid notificationId, Guid userId, PushMessage message, CancellationToken cancellationToken)
    {
        await WaitForGapAsync(userId, cancellationToken);
        var devices = await db.PushSubscriptions.AsNoTracking().Where(p => p.UserId == userId).ToListAsync(cancellationToken);
        var payload = message.ToJson();
        var now = time.GetUtcNow();
        bool delivered = false, retry = false, refused = false;
        foreach (var device in devices)
        {
            PushOutcome outcome;
            try { outcome = await push.SendAsync(device, payload, cancellationToken); }
            // A shutdown passes through to PushAsync, which gives the claim back.
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
                .SetProperty(x => x.Error, delivered ? null : "No device got it.")
                .SetProperty(x => x.Title, Clip(message.Title, PushTexts.TitleMax))
                .SetProperty(x => x.Body, Clip(message.Body, PushTexts.BodyMax)), CancellationToken.None);
        db.ChangeTracker.Clear();
        return delivered;
    }

    /// <summary>Cut to <paramref name="max"/>, never between an emoji's two halves (a lone surrogate the database refuses).</summary>
    static string Clip(string text, int max) =>
        text.Length <= max ? text : text[..(char.IsHighSurrogate(text[max - 1]) ? max - 1 : max)];

    /// <summary>Waits until <c>pushGap</c> has passed since the user's last push that went out.</summary>
    async Task WaitForGapAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (pushGap <= TimeSpan.Zero) return;
        var last = await db.Notifications.AsNoTracking()
            .Where(n => n.UserId == userId && n.Channel == NotificationChannel.Push && n.SentAt != null)
            .MaxAsync(n => n.SentAt, cancellationToken);
        if (last is { } sent && sent + pushGap - time.GetUtcNow() is var wait && wait > TimeSpan.Zero)
            await Task.Delay(wait, time, cancellationToken);
    }

    /// <summary>Takes the right to send this notification, once: its id, or null when it was already claimed (a retry, a second run).</summary>
    public async Task<Guid?> ClaimAsync(Guid userId, Guid? signalId, NotificationKind kind, DateOnly forDate, CancellationToken cancellationToken,
        NotificationChannel channel = NotificationChannel.Email, (Guid StrategyId, Guid AssetId)? stock = null)
    {
        var notification = new Notification
        {
            UserId = userId, SignalId = signalId, StrategyId = stock?.StrategyId, AssetId = stock?.AssetId, Kind = kind, Channel = channel, ForDate = forDate,
        };
        db.Notifications.Add(notification);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return notification.Id;
        }
        catch (DbUpdateException error) when (error.IsUniqueViolation())
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
    public async Task<bool> SendAsync(Guid notificationId, string to, Func<Task<EmailContent?>> compose, CancellationToken cancellationToken)
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
        var now = time.GetUtcNow();
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
    public async Task<int> SettledAsync(int sent)
    {
        if (!anySent || anyFailed) return sent;
        // Not while any other run failed in the last day (another time zone's overviews, the alerts): mixed results would
        // otherwise clear the alert and open it again each day, undoing a dismiss.
        var since = time.GetUtcNow() - FailureMemory;
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
    public async Task NotSentAsync(Guid notificationId, string reason)
    {
        await db.Notifications.Where(n => n.Id == notificationId)
            .ExecuteUpdateAsync(n => n.SetProperty(x => x.Error, reason), CancellationToken.None);
        db.ChangeTracker.Clear();
    }

    /// <summary>Gives the claim back: nothing was sent, so the next run may send it.</summary>
    public async Task ReleaseAsync(Guid notificationId)
    {
        await db.Notifications.Where(n => n.Id == notificationId).ExecuteDeleteAsync(CancellationToken.None);
        db.ChangeTracker.Clear();
    }
}
