using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Auth;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Backtest;

/// <summary>
/// /admin/backtest, for the owner (the admin group's policy): runs one backtest in the request (ROADMAP item 54), and saves a strategy's
/// own test settings.
/// </summary>
public static class BacktestEndpoints
{
    public static void MapBacktest(RouteGroupBuilder admin)
    {
        admin.MapPost("/backtest", async Task<Results<Ok<BacktestRun>, BadRequest<string>, Conflict<string>>> (BacktestRequest request, BacktestRunner runner,
            CancellationToken cancellationToken) =>
        {
            try
            {
                return TypedResults.Ok(await runner.RunAsync(request, cancellationToken));
            }
            catch (BacktestRunException error) when (error.MissingData)
            {
                return TypedResults.Conflict(error.Message);
            }
            catch (BacktestRunException error)
            {
                return TypedResults.BadRequest(error.Message);
            }
        })
        .WithSummary("Runs one backtest: the rules over the test draft, a list, one stock or an index fund, with the test's money; kept by its key.");

        admin.MapPut("/backtest/defaults/{strategyId:guid}", async Task<Results<NoContent, NotFound, BadRequest<string>>> (Guid strategyId,
            BacktestDefaults defaults, AppDbContext db, Sessions sessions, TemplateStore templates, CancellationToken cancellationToken) =>
        {
            var user = await sessions.RequiredAsync(cancellationToken);
            var strategy = await db.Strategies.SingleOrDefaultAsync(s => s.Id == strategyId && s.UserId == user.Id, cancellationToken);
            if (strategy is null) return TypedResults.NotFound();
            // Checked against the template in use, as a test of it is.
            var template = await templates.FindAsync(strategy.TemplateKey, cancellationToken);
            if (template is null) return TypedResults.NotFound();
            if (defaults.Error(template) is { } error) return TypedResults.BadRequest(error);
            strategy.BacktestDefaults = defaults.ToJson();
            await db.SaveChangesAsync(cancellationToken);
            return TypedResults.NoContent();
        })
        .WithSummary("Saves a strategy's own test settings: what its backtest opens on in place of the test defaults.");
    }
}
