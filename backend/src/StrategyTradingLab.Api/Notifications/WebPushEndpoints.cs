using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Auth;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Notifications;

/// <summary>What the browser's <c>PushSubscription.toJSON()</c> gives.</summary>
public record PushSubscriptionRequest(string? Endpoint, PushSubscriptionKeys? Keys);
public record PushSubscriptionKeys(string? P256dh, string? Auth);
public record PushEndpointRequest(string? Endpoint);

/// <summary>The key to subscribe with; null when push isn't set up on this server (the app then offers email only).</summary>
public record PushKey(string? PublicKey);

/// <summary>This device's push subscription (spec: Notifications; Choose the mode's Alerts box, the account page).</summary>
public static class WebPushEndpoints
{
    /// <summary>
    /// The push services browsers use. The server posts to the endpoint a browser hands it, so any other address is refused:
    /// it would let a user make the server call an address of their choosing.
    /// </summary>
    static readonly string[] PushHosts = ["fcm.googleapis.com", ".push.services.mozilla.com", ".notify.windows.com", ".push.apple.com"];

    public static bool IsPushService(string? endpoint, out Uri uri)
    {
        uri = null!;
        if (endpoint is null || endpoint.Length > 1000 || !Uri.TryCreate(endpoint, UriKind.Absolute, out var parsed)) return false;
        if (parsed.Scheme != Uri.UriSchemeHttps || !parsed.IsDefaultPort || parsed.UserInfo.Length > 0) return false;
        var host = parsed.IdnHost.ToLowerInvariant();
        if (!PushHosts.Any(known => known.StartsWith('.') ? host.EndsWith(known, StringComparison.Ordinal) : host == known)) return false;
        uri = parsed;
        return true;
    }

    /// <summary>A browser's key: base64url of the given length once decoded (65 for the P-256 point, 16 for the auth secret).</summary>
    static bool IsKey(string? value, int length)
    {
        if (value is null || value.Length > 200) return false;
        try { return WebEncoders.Base64UrlDecode(value) is { } bytes && bytes.Length == length && (length != 65 || bytes[0] == 4); }
        catch (FormatException) { return false; }
    }

    public static void MapWebPush(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/push").WithTags("Push").RequireUser();

        group.MapGet("/key", (IPushSender push) => TypedResults.Ok(new PushKey(push.PublicKey)))
            .WithSummary("The VAPID public key to subscribe with, or null when the server doesn't send push.");

        group.MapPut("/subscription", async Task<Results<NoContent, ValidationProblem>> (PushSubscriptionRequest request, AppDbContext db,
            Sessions sessions, HttpContext context, CancellationToken cancellationToken) =>
        {
            if (!IsPushService(request.Endpoint, out _) || !IsKey(request.Keys?.P256dh, 65) || !IsKey(request.Keys?.Auth, 16))
                return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["endpoint"] = [Messages.PushNotSupported.In(RequestLocale.From(context.Request))] });
            var user = (await sessions.CurrentAsync(cancellationToken))!;
            // One row per device: a device someone else used before (logged out, then this user logged in) now notifies this user only.
            var row = await db.PushSubscriptions.SingleOrDefaultAsync(s => s.Endpoint == request.Endpoint, cancellationToken);
            if (row is null)
                db.PushSubscriptions.Add(new PushSubscription { UserId = user.Id, Endpoint = request.Endpoint!, P256dh = request.Keys!.P256dh!, Auth = request.Keys.Auth! });
            else
            {
                row.UserId = user.Id;
                row.P256dh = request.Keys!.P256dh!;
                row.Auth = request.Keys.Auth!;
                row.LastFailedAt = null;
            }
            await db.SaveChangesAsync(cancellationToken);
            return TypedResults.NoContent();
        })
        .WithSummary("Saves this device's push subscription for the logged-in user (idempotent).");

        group.MapPost("/unsubscribe", async (PushEndpointRequest request, AppDbContext db, Sessions sessions, CancellationToken cancellationToken) =>
        {
            var user = (await sessions.CurrentAsync(cancellationToken))!;
            await db.PushSubscriptions.Where(s => s.UserId == user.Id && s.Endpoint == request.Endpoint).ExecuteDeleteAsync(cancellationToken);
            return TypedResults.NoContent();
        })
        .WithSummary("Forgets this device's push subscription, so it gets no more notifications.");
    }
}
