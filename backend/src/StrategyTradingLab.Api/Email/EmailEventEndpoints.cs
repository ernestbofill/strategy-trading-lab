using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Email;

/// <summary>
/// Brevo's transactional webhook (Brevo: Transactional → Settings → Webhook, with the events Hard bounce, Invalid email and
/// Spam): it tells the app which addresses can't be sent to, so the sender stops trying them (<see cref="SmtpEmailSender"/>).
/// </summary>
public static class EmailEventEndpoints
{
    public const string EventsLimit = "email-events";
    public const int KeyMin = 16;

    public static void MapEmailEvents(this IEndpointRouteBuilder app) =>
        // The key is in the address (Brevo can't sign its calls), set by Ernest in api.env as BREVO_WEBHOOK_KEY. Without one the
        // endpoint doesn't exist, and a wrong key looks the same. No cookie is used, so there is nothing for CSRF to forge.
        app.MapPost("/email/events", async Task<Results<NoContent, NotFound, BadRequest>> (HttpRequest request, IConfiguration configuration,
            AppDbContext db, ILoggerFactory loggers, CancellationToken cancellationToken) =>
        {
            if (configuration["BREVO_WEBHOOK_KEY"] is not { Length: >= KeyMin } expected
                || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(request.Query["key"].ToString()), Encoding.UTF8.GetBytes(expected)))
                return TypedResults.NotFound();

            JsonElement body;
            try
            {
                body = await request.ReadFromJsonAsync<JsonElement>(cancellationToken);
            }
            catch (Exception error) when (error is JsonException or InvalidOperationException) // Not JSON, or not sent as JSON.
            {
                return TypedResults.BadRequest();
            }

            var logger = loggers.CreateLogger(typeof(EmailEventEndpoints));
            // One event per call; an array is accepted too, in case a batch is switched on.
            var events = body.ValueKind == JsonValueKind.Array ? body.EnumerateArray().ToList() : [body];
            foreach (var item in events)
            {
                if (item.ValueKind != JsonValueKind.Object || Reason(item) is not { } reason
                    || !item.TryGetProperty("email", out var address) || address.ValueKind != JsonValueKind.String
                    || address.GetString()?.Trim() is not { Length: > 0 and <= 320 } email)
                    continue;
                if (await AddAsync(db, email, reason, cancellationToken))
                    logger.LogInformation("Stopped sending to an address: the mail service reported it as {Reason}.", reason);
            }
            return TypedResults.NoContent();
        })
        .WithMetadata(new SkipCsrfCheck())
        .RequireRateLimiting(EventsLimit)
        .WithTags("Email")
        .WithSummary("Brevo's webhook: records the addresses it reports as bounced, invalid or complaining, so the app stops sending to them.");

    /// <summary>Hard bounces, invalid addresses and spam complaints end sending; everything else (opens, soft bounces, blocks that pass) doesn't.</summary>
    static SuppressionReason? Reason(JsonElement item) =>
        item.TryGetProperty("event", out var name) && name.ValueKind == JsonValueKind.String ? name.GetString() switch
        {
            "hard_bounce" => SuppressionReason.Bounced,
            "invalid_email" => SuppressionReason.Invalid,
            "spam" => SuppressionReason.Complaint,
            _ => null,
        } : null;

    /// <summary>Adds the address unless it is on the list already (Brevo repeats events). True when it was added.</summary>
    static async Task<bool> AddAsync(AppDbContext db, string email, SuppressionReason reason, CancellationToken cancellationToken)
    {
        var stored = reason.ToString().ToLowerInvariant(); // The stored values: bounced, invalid, complaint.
        return await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO email_suppressions (id, email, reason, created_at) VALUES ({Guid.NewGuid()}, {email}, {stored}, now()) ON CONFLICT (email) DO NOTHING",
            cancellationToken) == 1;
    }
}
