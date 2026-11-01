using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Data;

namespace StrategyTradingLab.Api.Email;

public static class UnsubscribeEndpoints
{
    public const string UnsubscribeLimit = "unsubscribe";

    public static void MapUnsubscribe(this IEndpointRouteBuilder app) =>
        // A POST on purpose: mail apps send one for the List-Unsubscribe header (RFC 8058), and the app's page sends one from its
        // button, while a GET (which mail scanners make for every link) never changes anything. The token is the whole credential
        // and can only turn emails off, so it needs no login and works from any device.
        app.MapPost("/email/unsubscribe/{token}/{kind}", async Task<Results<NoContent, NotFound>> (string token, string kind, AppDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (Unsubscribe.FromSlug(kind) is not { } which) return TypedResults.NotFound();
            var user = db.Users.Where(u => u.UnsubscribeToken == token);
            var changed = which == EmailKind.Overview
                ? await user.ExecuteUpdateAsync(u => u.SetProperty(x => x.OverviewEmail, false), cancellationToken)
                : await user.ExecuteUpdateAsync(u => u.SetProperty(x => x.AlertEmails, false), cancellationToken);
            return changed == 0 ? TypedResults.NotFound() : TypedResults.NoContent();
        })
        .RequireRateLimiting(UnsubscribeLimit)
        .WithTags("Email")
        .WithSummary("Turns one kind of email off for the user whose unsubscribe token is in the address (the link in the emails).");
}
