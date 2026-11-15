using StrategyTradingLab.Api.Auth;

namespace StrategyTradingLab.Api.Notifications;

public static class SummaryEndpoints
{
    public static void MapSummary(this IEndpointRouteBuilder app)
    {
        app.MapGet("/summary", async (DailySummary summary, Sessions sessions, CancellationToken cancellationToken) =>
        {
            var user = await sessions.RequiredAsync(cancellationToken);
            return TypedResults.Ok(await summary.ForPageAsync(user.Id, user.TimeZone, user.Locale, cancellationToken));
        })
        .RequireUser()
        .WithTags("Summary")
        .WithName("GetDailySummary")
        .WithSummary("The user's daily summary from the most recent price check session.");
    }
}
