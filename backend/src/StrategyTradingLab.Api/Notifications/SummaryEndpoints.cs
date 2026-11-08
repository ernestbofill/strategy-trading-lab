using StrategyTradingLab.Api.Auth;

namespace StrategyTradingLab.Api.Notifications;

public static class SummaryEndpoints
{
    public static void MapSummary(this IEndpointRouteBuilder app)
    {
        app.MapGet("/summary", async (NotificationService notifications, Sessions sessions, TimeProvider time, CancellationToken cancellationToken) =>
        {
            var user = (await sessions.CurrentAsync(cancellationToken))!;
            return TypedResults.Ok(await notifications.GetDailySummaryAsync(user.Id, user.TimeZone, user.Locale, time, cancellationToken));
        })
        .RequireUser()
        .WithTags("Summary")
        .WithName("GetDailySummary")
        .WithSummary("The user's daily summary from the most recent price check session.");
    }
}
