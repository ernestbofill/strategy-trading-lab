using Microsoft.AspNetCore.Http.HttpResults;

namespace StrategyTradingLab.Api.Backtest;

/// <summary>/admin/backtest, for the owner (the admin group's policy): runs one backtest in the request (ROADMAP item 54).</summary>
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
    }
}
