using System.Text;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Email;
using StrategyTradingLab.Api.Notifications;

namespace StrategyTradingLab.Api.Admin;

/// <summary>A user a test push can go to: push on and at least one device.</summary>
public record AdminPushRecipient(Guid Id, string Email, int Devices);

/// <summary>A push on /admin/notifications. <c>Title</c> and <c>Body</c> are null for pushes sent before they were kept.</summary>
public record AdminPush(Guid Id, Guid UserId, string Email, NotificationKind Kind, string? Title, string? Body,
    DateTimeOffset CreatedAt, DateTimeOffset? SentAt, DateTimeOffset? FailedAt, string? Error);

public record AdminNotifications(IReadOnlyList<AdminPushRecipient> Recipients, IReadOnlyList<AdminPush> Pushes);

public record TestPush(Guid UserId, string? Title, string? Body);

/// <summary>30 test pushes an hour in all, so the page can't flood a user's phone.</summary>
public sealed class TestPushLimit() : HourlyLimit(30);

/// <summary>/admin/notifications: the pushes sent, newest first, and a test push to a user's devices.</summary>
public static class AdminNotificationsEndpoints
{
    /// <summary>How many pushes the page lists: the newest.</summary>
    const int Listed = 300;

    public static void MapNotifications(RouteGroupBuilder group)
    {
        var notifications = group.MapGroup("/notifications");

        notifications.MapGet("/", async (AppDbContext db, CancellationToken cancellationToken) =>
        {
            var recipients = await db.Users.AsNoTracking()
                .Where(u => u.AlertPush && db.PushSubscriptions.Any(p => p.UserId == u.Id))
                .OrderBy(u => u.Email)
                .Select(u => new AdminPushRecipient(u.Id, u.Email, db.PushSubscriptions.Count(p => p.UserId == u.Id)))
                .ToListAsync(cancellationToken);
            var pushes = await db.Notifications.AsNoTracking()
                .Where(n => n.Channel == NotificationChannel.Push)
                .OrderByDescending(n => n.CreatedAt).Take(Listed)
                .Join(db.Users, n => n.UserId, u => u.Id, (n, u) => new AdminPush(n.Id, n.UserId, u.Email, n.Kind, n.Title, n.Body,
                    n.CreatedAt, n.SentAt, n.FailedAt, n.Error))
                .ToListAsync(cancellationToken);
            return new AdminNotifications(recipients, pushes);
        })
        .WithSummary("The users a test push can go to, and the newest pushes sent, with their result.");

        notifications.MapPost("/test", async Task<Results<Ok<AdminPush>, ValidationProblem, Conflict<string>, ProblemHttpResult>> (
            TestPush request, AppDbContext db, IEmailSender email, IPushSender push, AdminAlerts alerts, TimeProvider time,
            TestPushLimit limit, ILogger<TestPushLimit> logger, CancellationToken cancellationToken) =>
        {
            var title = request.Title?.Trim() ?? "";
            var body = request.Body?.Trim() ?? "";
            var errors = new Dictionary<string, string[]>();
            if (title.Length is 0 or > PushTexts.TitleMax) errors["title"] = [$"Write a title of up to {PushTexts.TitleMax} characters."];
            if (body.Length is 0 or > PushTexts.BodyMax) errors["body"] = [$"Write a text of up to {PushTexts.BodyMax} characters."];
            // A push service takes about 4 KB, and the JSON escapes each accent or emoji to 6 to 12 bytes.
            else if (errors.Count == 0 && Encoding.UTF8.GetByteCount(PushTexts.Test(Guid.Empty, title, body).ToJson()) > PushTexts.PayloadMax)
                errors["body"] = ["Title and text are too long together for a push (accents and emoji count more): shorten either."];
            if (errors.Count > 0) return TypedResults.ValidationProblem(errors);

            // No gap to wait for: the page waits for the answer. The send lock still keeps it apart from the check's pushes, so it may wait for those.
            var delivery = new NotificationDelivery(db, email, push, alerts, time, logger, TimeSpan.Zero);
            if (!delivery.PushReady) return TypedResults.Conflict("Push isn't set up on this server: VAPID_PUBLIC_KEY and VAPID_PRIVATE_KEY are missing.");
            var user = await db.Users.AsNoTracking().Where(u => u.Id == request.UserId)
                .Select(u => new { u.Email, u.AlertPush, Devices = db.PushSubscriptions.Count(p => p.UserId == u.Id) })
                .FirstOrDefaultAsync(cancellationToken);
            if (user is null) return TypedResults.Conflict("That user no longer exists. Reload the page.");
            if (!user.AlertPush) return TypedResults.Conflict("That user has turned notifications off. Reload the page.");
            if (user.Devices == 0) return TypedResults.Conflict("That user has no device with notifications on. Reload the page.");
            if (!limit.TryAcquire())
                return TypedResults.Problem("Too many test pushes: try again in an hour.", statusCode: StatusCodes.Status429TooManyRequests);

            var now = time.GetUtcNow();
            var claimed = (await delivery.ClaimAsync(request.UserId, null, NotificationKind.Test, DateOnly.FromDateTime(now.UtcDateTime),
                cancellationToken, NotificationChannel.Push))!.Value;
            // Not the request's token: a page that gave up waiting for the lock mustn't lose a push that went out, or its row.
            await delivery.PushAsync(claimed, request.UserId, PushTexts.Test(claimed, title, body), CancellationToken.None);

            // A push service that didn't answer gives the claim back (the row is gone): keep it, as failed, so the list shows the try.
            var row = await db.Notifications.AsNoTracking().FirstOrDefaultAsync(n => n.Id == claimed, cancellationToken);
            if (row is null)
            {
                row = new Notification
                {
                    Id = claimed, UserId = request.UserId, Kind = NotificationKind.Test, Channel = NotificationChannel.Push,
                    ForDate = DateOnly.FromDateTime(now.UtcDateTime), FailedAt = now, Title = title, Body = body,
                    Error = "No push service answered. Try again later.", CreatedAt = now,
                };
                db.Notifications.Add(row);
                await db.SaveChangesAsync(CancellationToken.None);
            }
            return TypedResults.Ok(new AdminPush(row.Id, row.UserId, user.Email, row.Kind, row.Title, row.Body, row.CreatedAt, row.SentAt,
                row.FailedAt, row.Error));
        })
        .WithSummary("Sends a test push with this title and text to every device of a user, and records it. 409 when push isn't set up or the user has no device.");
    }
}
