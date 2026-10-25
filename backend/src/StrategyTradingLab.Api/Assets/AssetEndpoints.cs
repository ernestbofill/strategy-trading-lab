using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using StrategyTradingLab.Api.Auth;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.MarketData;

namespace StrategyTradingLab.Api.Assets;

public record AssetResolveRequest(string Symbol, AssetKind? Kind = null);

public static class AssetEndpoints
{
    public static void MapAssets(this IEndpointRouteBuilder app)
    {
        var assets = app.MapGroup("/assets").WithTags("Assets").RequireUser();

        assets.MapGet("/search", async Task<Results<Ok<IReadOnlyList<MarketQuote>>, ValidationProblem, ProblemHttpResult>> (
            string q, AssetCatalog catalog, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(q))
                return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["q"] = ["Type a symbol or company name."] });

            try
            {
                return TypedResults.Ok(await catalog.SearchAsync(q, cancellationToken));
            }
            catch (MarketDataException error)
            {
                return TypedResults.Problem(error.Message, statusCode: StatusCodes.Status400BadRequest);
            }
        })
        .WithName("SearchAssets")
        .WithSummary("Looks up a symbol or company in Yahoo Finance so a new asset can be added without a deploy.");

        assets.MapPost("/resolve", async Task<Results<Ok<AssetLookupResult>, ValidationProblem, ProblemHttpResult>> (
            AssetResolveRequest request, AssetCatalog catalog, CancellationToken cancellationToken) =>
        {
            try
            {
                return TypedResults.Ok(await catalog.EnsureAsync(request.Symbol, request.Kind, cancellationToken));
            }
            catch (ValidationException error)
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["symbol"] = [error.Message] });
            }
            catch (MarketDataException error)
            {
                return TypedResults.Problem(error.Message, statusCode: StatusCodes.Status400BadRequest);
            }
        })
        .WithName("ResolveAsset")
        .WithSummary("Creates or reuses an asset row, then fetches its price history and exchange rate if needed.");
    }
}
