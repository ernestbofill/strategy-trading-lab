using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Admin;
using StrategyTradingLab.Api.Auth;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.MarketData;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Assets;

/// <summary>
/// One search result: a stored asset has its <c>Id</c>, one only the price source knows has none until it is added.
/// <c>Exchange</c> is the MIC; <c>What</c> is its line in the list (a fund's name, a stock's sector and industry).
/// </summary>
public record AssetMatch(Guid? Id, string Symbol, string Name, string Exchange, string What);

public record AddAssetRequest(string? Symbol, string? Kind);

/// <summary>The added (or stored) asset, and whether its prices are in yet; a strategy on it waits for them.</summary>
public record AddedAssetDto(SecurityDto Asset, bool PricesIn);

/// <summary>
/// The fund picker (and later "+ Add a stock", ROADMAP item 14): search by name or symbol, and add what isn't stored yet
/// through <see cref="AssetCatalog"/>. For logged-in users (adding needs a confirmed email), rate limited per user, as both
/// call the price source.
/// </summary>
public static class AssetEndpoints
{
    public const string SearchLimit = "asset-search", AddLimit = "asset-add";
    public const int MaxQueryLength = 50, MaxResults = 10;

    public static void MapAssets(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/assets").WithTags("Assets").RequireUser();

        group.MapGet("/search", async Task<Results<Ok<List<AssetMatch>>, ValidationProblem, ProblemHttpResult>> (string? q, string? kind,
            AppDbContext db, Sessions sessions, IMarketDataSource source, CancellationToken cancellationToken) =>
        {
            var user = (await sessions.CurrentAsync(cancellationToken))!;
            var query = q?.Trim() ?? "";
            if (query.Length is < 2 or > MaxQueryLength) return Invalid("q", Messages.SearchLength.In(user.Locale, MaxQueryLength));
            if (KindFrom(kind) is not { } wanted) return Invalid("kind", Messages.PickStockOrFund.In(user.Locale));

            // The stored ones first: they need no adding. A symbol starts with the query, a name contains it.
            var escaped = EscapeLike(query);
            var stored = await db.Assets.AsNoTracking()
                .Where(a => a.Active && a.Kind == wanted
                    && (EF.Functions.ILike(a.Symbol, escaped + "%") || EF.Functions.ILike(a.Name, $"%{escaped}%")))
                .OrderBy(a => a.Name).Take(MaxResults)
                .Select(a => new AssetMatch(a.Id, a.Symbol, a.Name, a.Exchange,
                    a.Texts.Where(t => t.Locale == user.Locale).Select(t => t.What).FirstOrDefault()
                        ?? a.Texts.Where(t => t.Locale == TemplateTexts.DefaultLocale).Select(t => t.What).First()))
                .ToListAsync(cancellationToken);

            IReadOnlyList<SymbolMatch> found;
            try
            {
                found = await source.SearchAsync(query, cancellationToken);
            }
            catch (MarketDataException) when (stored.Count > 0)
            {
                return TypedResults.Ok(stored);
            }
            catch (MarketDataException)
            {
                return TypedResults.Problem(Messages.PriceSourceDown.In(user.Locale), statusCode: StatusCodes.Status502BadGateway);
            }

            var candidates = found
                .Where(m => AssetCatalog.KindOf(m.Type) == wanted && AssetCatalog.SupportedExchanges.Contains(AssetCatalog.Mic(m.Exchange)))
                .ToList();
            // Any stored row, hidden ones too, is left out: a stored one is listed above, a hidden one can't be picked.
            var symbols = candidates.Select(m => m.Symbol.ToUpperInvariant()).ToList();
            var known = (await db.Assets.Where(a => symbols.Contains(a.Symbol)).Select(a => a.Symbol).ToListAsync(cancellationToken)).ToHashSet();
            var fresh = candidates
                .Where(m => !known.Contains(m.Symbol.ToUpperInvariant()))
                .Select(m =>
                {
                    var asset = new Asset
                    {
                        Symbol = m.Symbol.ToUpperInvariant(), Exchange = AssetCatalog.Mic(m.Exchange), Name = m.Name, Kind = wanted,
                        Currency = "", Sector = m.Sector, Industry = m.Industry,
                    };
                    return new AssetMatch(null, asset.Symbol, asset.Name, asset.Exchange, AssetCatalog.What(asset));
                });
            return TypedResults.Ok(stored.Concat(fresh).Take(MaxResults).ToList());
        })
        .RequireRateLimiting(SearchLimit)
        .WithName("SearchAssets")
        .WithSummary("Stocks or funds by name or symbol: stored ones first, then the price source's from the exchanges the app supports.");

        group.MapPost("/", async Task<Results<Ok<AddedAssetDto>, ValidationProblem, ProblemHttpResult, Conflict<string>>> (AddAssetRequest request,
            Sessions sessions, AssetCatalog catalog, DatabaseSwitch databases, CancellationToken cancellationToken) =>
        {
            var user = (await sessions.CurrentAsync(cancellationToken))!;
            var symbol = request.Symbol?.Trim() ?? "";
            if (KindFrom(request.Kind) is not { } wanted) return Invalid("kind", Messages.PickStockOrFund.In(user.Locale));
            var refused = wanted == AssetKind.Fund ? Messages.NotAFund.In(user.Locale) : Messages.NotAStock.In(user.Locale);
            if (symbol.Length is 0 or > AssetCatalog.MaxSymbolLength) return Invalid("symbol", refused);
            // Adding fetches prices, and prices belong to the database this host owns (as on /admin/assets).
            if (databases.Active != databases.Home) return TypedResults.Conflict(Messages.CantAddNow.In(user.Locale));
            try
            {
                var added = await catalog.AddAsync(symbol, user.Id, cancellationToken,
                    (kind, exchange) => kind == wanted && AssetCatalog.SupportedExchanges.Contains(exchange));
                var asset = added.Asset;
                if (!asset.Active || asset.Kind != wanted) return Invalid("symbol", refused);
                var what = asset.Texts.FirstOrDefault(t => t.Locale == user.Locale)?.What
                    ?? asset.Texts.First(t => t.Locale == TemplateTexts.DefaultLocale).What;
                return TypedResults.Ok(new AddedAssetDto(new SecurityDto(asset.Id, asset.Symbol, asset.Name, what), added.Problem is null));
            }
            catch (UnsupportedCurrencyException)
            {
                return Invalid("symbol", refused);
            }
            catch (AssetNotFoundException)
            {
                return Invalid("symbol", refused);
            }
            catch (MarketDataException)
            {
                return TypedResults.Problem(Messages.PriceSourceDown.In(user.Locale), statusCode: StatusCodes.Status502BadGateway);
            }
        })
        // Adding writes to the assets every user shares, so it needs a confirmed email, as feedback does.
        .RequireAuthorization(Policies.VerifiedEmail)
        .RequireRateLimiting(AddLimit)
        .WithName("AddAsset")
        .WithSummary("Adds a stock or fund from a supported exchange by its symbol and loads its prices; a stored one is returned as it is.");
    }

    /// <summary>Users search and add stocks and funds; indexes are followed, not bought.</summary>
    static AssetKind? KindFrom(string? kind) => kind?.ToLowerInvariant() switch
    {
        "stock" => AssetKind.Stock,
        "fund" => AssetKind.Fund,
        _ => null,
    };

    static string EscapeLike(string text) => text.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");

    static ValidationProblem Invalid(string field, string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    /// <summary>The rate limit's key: the user, or the client's address before the session is read.</summary>
    public static string UserKey(HttpContext http) =>
        http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? AdminEndpoints.ClientKey(http.Connection.RemoteIpAddress);
}
