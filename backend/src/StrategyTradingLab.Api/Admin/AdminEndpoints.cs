using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.HttpResults;
using StrategyTradingLab.Api.Assets;
using StrategyTradingLab.Api.Auth;
using StrategyTradingLab.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Email;
using StrategyTradingLab.Api.MarketData;

namespace StrategyTradingLab.Api.Admin;

/// <summary>What the admin page shows for a setting. A secret's value is never sent, only whether it is set.</summary>
public record AdminSetting(string Key, string Group, string Description, string? Default, bool Secret, string? Value,
    bool IsSet, SettingSource Source);

/// <summary>Where the test email went.</summary>
public record TestEmailSent(string SentTo);

/// <summary>The database box: which one the API uses, this host's own, and each one's variable and host.</summary>
public record AdminDatabases(DatabaseName Active, DatabaseName Home, IReadOnlyList<DatabaseInfo> Databases);

public record DatabaseChoice(string Name);

/// <summary>The prices box: the next and last runs, what failed in the last one, and the newest stored day.</summary>
public record AdminPrices(bool Running, DateTimeOffset? NextRun, DateTimeOffset? LastStarted, DateTimeOffset? LastFinished,
    DateOnly? LatestDay, IReadOnlyList<RefreshResult> Problems, int Updated, int FullHistory, int? NewSignals, int EvaluationFailures);

/// <summary>One symbol asked of Yahoo by "Test Yahoo": its newest day and close, or why it didn't answer.</summary>
public record YahooCheck(string Symbol, bool Ok, DateOnly? Day, decimal? Close, string? Problem, int Milliseconds);

/// <summary>An asset on the admin page: its details, whether it is active, how many running strategies watch it, and its newest stored price date.</summary>
public record AdminAsset(Guid Id, string Symbol, string Exchange, string Name, string What, AssetKind Kind,
    string Currency, bool Active, int Strategies, DateOnly? LatestPriceDate);

public record AssetActive(bool Active);

/// <summary>A symbol to add, as Yahoo writes it ("AAPL", "SAP.DE").</summary>
public record AssetSymbol(string? Symbol);

/// <summary>What adding a symbol did: the row, whether it is new, and why its prices didn't load, if they didn't.</summary>
public record AdminAddedAsset(AdminAsset Asset, bool Created, string? Problem);

/// <summary>An asset's name and its English "what it does".</summary>
public record AssetEdit(string? Name, string? What);

/// <summary>A value to save. For a secret, <c>Expected</c> holds the values the page showed for the settings it is bound to.</summary>
public record SettingValue(string Value, IReadOnlyDictionary<string, string?>? Expected = null);

/// <summary>An hourly limit on an admin action, shared by everyone (the owner is the only caller).</summary>
public abstract class HourlyLimit(int permits) : IDisposable
{
    readonly FixedWindowRateLimiter limiter = new(new FixedWindowRateLimiterOptions
        { PermitLimit = permits, Window = TimeSpan.FromHours(1), QueueLimit = 0 });

    public bool TryAcquire() => limiter.AttemptAcquire().IsAcquired;

    public void Dispose() => limiter.Dispose();
}

/// <summary>5 test emails an hour in all, so the page can't be used to flood the account's inbox or Gmail's sending limit.</summary>
public sealed class TestEmailLimit() : HourlyLimit(5);

/// <summary>30 Yahoo checks an hour in all, so the page can't be used to hammer an unofficial endpoint.</summary>
public sealed class TestYahooLimit() : HourlyLimit(30);

/// <summary>
/// The admin settings page's API, for the owner only: a logged-in user whose role is <see cref="UserRole.Owner"/>.
/// 401 without a log-in, 403 for any other role (the <see cref="Policies.Owner"/> policy).
/// </summary>
public static class AdminEndpoints
{
    public static void MapAdmin(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin").WithTags("Admin").RequireAuthorization(Policies.Owner);

        var settings = group.MapGroup("/settings");
        MapDatabase(group);
        MapPrices(group);
        MapAssets(group);

        // Only ever to the account itself, so the page can't be used to send mail from it to anyone.
        group.MapPost("/test-email", async Task<Results<Ok<TestEmailSent>, ProblemHttpResult>> (IEmailSender email,
            TestEmailLimit limit, TimeProvider time, CancellationToken cancellationToken) =>
        {
            if (!limit.TryAcquire())
                return TypedResults.Problem("Too many test emails: try again in an hour.", statusCode: StatusCodes.Status429TooManyRequests);
            try
            {
                var sentTo = await email.SendToAccountAsync("Strategy Trading Lab: test email",
                    $"This is a test email from the Strategy Trading Lab admin page, sent {time.GetUtcNow():yyyy-MM-dd HH:mm} UTC.\n\n" +
                    "If you got it, the app can send its emails.", cancellationToken);
                return TypedResults.Ok(new TestEmailSent(sentTo));
            }
            catch (EmailNotSetUpException error)
            {
                return TypedResults.Problem(error.Message, statusCode: StatusCodes.Status400BadRequest);
            }
            catch (EmailException error)
            {
                return TypedResults.Problem(error.Message, statusCode: StatusCodes.Status502BadGateway);
            }
        })
        .WithSummary("Sends a test email to the mail account itself, with the settings as they are now.");

        settings.MapGet("/", async (SettingsStore store, CancellationToken cancellationToken) =>
            (await store.AllAsync(cancellationToken)).Select(Dto).ToList())
        .WithSummary("The settings the admin page can set, with where each value comes from.");

        settings.MapPut("/{key}", async Task<Results<NoContent, NotFound, ValidationProblem, Conflict<string>>> (string key,
            SettingValue request, SettingsStore store, PriceRefreshState prices, CancellationToken cancellationToken) =>
        {
            if (SettingDefinitions.Find(key) is not { } definition) return TypedResults.NotFound();
            // A secret is kept exactly as typed: a password may start or end with a space.
            var value = string.IsNullOrWhiteSpace(request.Value) ? "" : definition.Secret ? request.Value : request.Value.Trim();
            var error = value.Length == 0 ? "Type a value, or use Default."
                : value.Length > 500 ? "Use at most 500 characters."
                : value.Any(char.IsControl) ? "Use printable characters only."
                : definition.Validate?.Invoke(value);
            if (error is not null) return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["value"] = [error] });

            var saved = await store.SetAsync(definition, value, request.Expected, cancellationToken);
            if (saved && IsPriceSetting(key)) prices.ScheduleChanged();
            return saved
                ? TypedResults.NoContent()
                : TypedResults.Conflict("The mail settings changed since this page loaded. Reload, check them, then type the password again.");
        })
        .WithSummary("Sets a setting. The saved password only counts while the mail server, port and user it was saved with stay.");

        settings.MapDelete("/{key}", async Task<Results<NoContent, NotFound>> (string key, bool? clear, SettingsStore store,
            PriceRefreshState prices, CancellationToken cancellationToken) =>
        {
            if (SettingDefinitions.Find(key) is not { } definition) return TypedResults.NotFound();
            if (clear == true) await store.ClearAsync(definition, cancellationToken);
            else await store.ResetAsync(key, cancellationToken);
            if (IsPriceSetting(key)) prices.ScheduleChanged();
            return TypedResults.NoContent();
        })
        .WithSummary("Back to the server's value or the default; with ?clear=true, no value at all.");
    }

    static bool IsPriceSetting(string key) => key.StartsWith("PRICES_");

    /// <summary>The prices box: the last refresh, a button to run it now, and a check that Yahoo answers.</summary>
    static void MapPrices(RouteGroupBuilder group)
    {
        var prices = group.MapGroup("/prices");

        prices.MapGet("/", async (PriceRefreshState state, AppDbContext db, CancellationToken cancellationToken) =>
        {
            var latest = await db.Prices.MaxAsync(p => (DateOnly?)p.Date, cancellationToken);
            var results = state.LastResults;
            return new AdminPrices(state.Running, state.NextRun, state.LastStarted, state.LastFinished, latest,
                [.. results.Where(r => r.Outcome == RefreshOutcome.Failed)],
                results.Count(r => r.Outcome == RefreshOutcome.Updated), results.Count(r => r.Outcome == RefreshOutcome.FullHistory),
                state.LastEvaluation?.NewSignals, state.LastEvaluation?.Failed ?? 0);
        })
        .WithSummary("The price refresh: the next and last runs and what failed in the last one.");

        prices.MapPost("/refresh", Results<Accepted, Conflict<string>> (PriceRefreshState state, DatabaseSwitch databases) =>
            databases.Active != databases.Home ? TypedResults.Conflict("The API is switched to another database: switch back to refresh the prices.")
            : state.Request() ? TypedResults.Accepted((string?)null)
            : TypedResults.Conflict("A refresh is already running."))
        .WithSummary("Refreshes the prices now, in the background, whatever the schedule says.");

        prices.MapPost("/test", async Task<Results<Ok<List<YahooCheck>>, ProblemHttpResult>> (IMarketDataSource source, TestYahooLimit limit,
            TimeProvider time, CancellationToken cancellationToken) =>
        {
            if (!limit.TryAcquire())
                return TypedResults.Problem("Too many tests: try again in an hour.", statusCode: StatusCodes.Status429TooManyRequests);
            // A stock fund and an exchange rate: the two kinds of data the refresh reads.
            var checks = new List<YahooCheck>();
            foreach (var (symbol, adjusted) in new[] { ("SPY", true), ("EURUSD=X", false) })
            {
                var started = time.GetTimestamp();
                YahooCheck Check(bool ok, DateOnly? day, decimal? close, string? problem) =>
                    new(symbol, ok, day, close, problem, (int)time.GetElapsedTime(started).TotalMilliseconds);
                try
                {
                    var days = await source.DailyAsync(symbol, fullHistory: false, adjusted: adjusted, cancellationToken);
                    checks.Add(days.Count == 0
                        ? Check(false, null, null, "Yahoo answered with no days.")
                        : Check(true, days[^1].Date, days[^1].Close, null));
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    checks.Add(Check(false, null, null, error.Message));
                }
            }
            return TypedResults.Ok(checks);
        })
        .WithSummary("Asks Yahoo for a fund and an exchange rate and says whether it answered. Stores nothing.");
    }

    /// <summary>
    /// The database box: switch dev / production. Their URLs are only in the server's environment; a switch is tested
    /// first (see <see cref="DatabaseSwitch"/>).
    /// </summary>
    static void MapDatabase(RouteGroupBuilder group)
    {
        var database = group.MapGroup("/database");

        database.MapGet("/", (DatabaseSwitch databases) => new AdminDatabases(databases.Active, databases.Home, databases.Databases()))
        .WithSummary("Which database the API uses, and each one's server variable and host.");

        database.MapPost("/active", (DatabaseChoice request, DatabaseSwitch databases, CancellationToken cancellationToken) =>
            Switch(request.Name, name => databases.SwitchAsync(name, cancellationToken)))
        .WithSummary("Switches the API to the dev or production database, once it answers and has every migration.");
    }

    static void MapAssets(RouteGroupBuilder group)
    {
        var assets = group.MapGroup("/assets");

        assets.MapGet("/", async (AppDbContext db, CancellationToken cancellationToken) =>
        {
            var rows = await AdminAssets(db, db.Assets).ToListAsync(cancellationToken);
            return rows.OrderBy(a => a.Name, StringComparer.InvariantCultureIgnoreCase)
                .ThenBy(a => a.Symbol, StringComparer.InvariantCultureIgnoreCase)
                .ToList();
        })
        .WithSummary("All assets, with how many running strategies watch each and its newest stored price date.");

        assets.MapPost("/", async Task<Results<Ok<AdminAddedAsset>, ValidationProblem, ProblemHttpResult, Conflict<string>>> (AssetSymbol request,
            ClaimsPrincipal principal, AssetCatalog catalog, AppDbContext db, DatabaseSwitch databases, CancellationToken cancellationToken) =>
        {
            var symbol = request.Symbol?.Trim() ?? "";
            if (SymbolProblem(symbol) is { } symbolProblem) return Invalid("symbol", symbolProblem);
            // Adding fetches prices, and prices belong to the database this host owns (as Refresh now).
            if (databases.Active != databases.Home)
                return TypedResults.Conflict("The API is switched to another database: switch back to add an asset.");
            try
            {
                var added = await catalog.AddAsync(symbol, Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), cancellationToken);
                var row = await AdminAssets(db, db.Assets.Where(a => a.Id == added.Asset.Id)).SingleAsync(cancellationToken);
                return TypedResults.Ok(new AdminAddedAsset(row, added.Created, added.Problem));
            }
            catch (AssetNotFoundException error)
            {
                return Invalid("symbol", error.Message);
            }
            catch (MarketDataException error)
            {
                return TypedResults.Problem(error.Message, statusCode: StatusCodes.Status502BadGateway);
            }
        })
        .WithSummary("Adds a stock, fund or index by its Yahoo symbol and fetches its price history; an asset already there is returned as it is.");

        assets.MapPut("/{id:guid}", async Task<Results<NoContent, NotFound, ValidationProblem>> (Guid id, AssetEdit request,
            AppDbContext db, CancellationToken cancellationToken) =>
        {
            var name = request.Name?.Trim() ?? "";
            var what = request.What?.Trim() ?? "";
            var errors = new Dictionary<string, string[]>();
            if (TextProblem(name, 100) is { } nameProblem) errors["name"] = [nameProblem];
            if (TextProblem(what, 200) is { } whatProblem) errors["what"] = [whatProblem];
            if (errors.Count > 0) return TypedResults.ValidationProblem(errors);

            var asset = await db.Assets.Include(a => a.Texts).SingleOrDefaultAsync(a => a.Id == id, cancellationToken);
            if (asset is null) return TypedResults.NotFound();
            asset.Name = name;
            if (asset.Texts.SingleOrDefault(t => t.Locale == Templates.TemplateTexts.DefaultLocale) is { } text) text.What = what;
            else asset.Texts.Add(new AssetText { Locale = Templates.TemplateTexts.DefaultLocale, What = what });
            await db.SaveChangesAsync(cancellationToken);
            return TypedResults.NoContent();
        })
        .WithSummary("Changes an asset's name and its English \"what it does\".");

        assets.MapPut("/{id:guid}/active", async Task<Results<NoContent, NotFound>> (Guid id, AssetActive request,
            AppDbContext db, CancellationToken cancellationToken) =>
        {
            var updated = await db.Assets.Where(a => a.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.Active, request.Active), cancellationToken);
            return updated == 0 ? TypedResults.NotFound() : TypedResults.NoContent();
        })
        .WithSummary("Hides or restores an asset.");
    }

    /// <summary>The admin page's rows for those assets.</summary>
    static IQueryable<AdminAsset> AdminAssets(AppDbContext db, IQueryable<Asset> assets)
    {
        // The strategies that keep its prices fetched daily (PriceRefresher.Watched), so a stock unticked but still held isn't shown as unused.
        var listed = PriceRefresher.Listings(db);
        return assets
            .AsNoTracking()
            .Select(asset => new AdminAsset(
                asset.Id,
                asset.Symbol,
                asset.Exchange,
                asset.Name,
                asset.Texts.Where(t => t.Locale == Templates.TemplateTexts.DefaultLocale).Select(t => t.What).FirstOrDefault() ?? "",
                asset.Kind,
                asset.Currency,
                asset.Active,
                listed.Count(sa => sa.AssetId == asset.Id),
                db.Prices.Where(p => p.AssetId == asset.Id).Max(p => (DateOnly?)p.Date)));
    }

    static ValidationProblem Invalid(string field, string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    static string? SymbolProblem(string symbol) =>
        symbol.Length == 0 ? "Type a symbol."
        : symbol.Length > AssetCatalog.MaxSymbolLength || symbol.Any(c => char.IsWhiteSpace(c) || char.IsControl(c))
            ? "Type one symbol, as Yahoo writes it (AAPL, SAP.DE)."
        : null;

    static string? TextProblem(string text, int max) =>
        text.Length == 0 ? "Type something."
        : text.Length > max ? $"Use at most {max} characters."
        : text.Any(char.IsControl) ? "Use printable characters only."
        : null;

    static async Task<Results<NoContent, NotFound, ProblemHttpResult>> Switch(string? name, Func<DatabaseName, Task> change)
    {
        // By name only: "dev" or "production". Enum.TryParse would also take a number.
        if (Enum.GetValues<DatabaseName>().Cast<DatabaseName?>().FirstOrDefault(d => d.ToString()!.Equals(name, StringComparison.OrdinalIgnoreCase))
            is not { } known)
            return TypedResults.NotFound();
        try
        {
            await change(known);
            return TypedResults.NoContent();
        }
        catch (DatabaseChangeException error)
        {
            return TypedResults.Problem(error.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    static AdminSetting Dto(EffectiveSetting setting)
    {
        var d = setting.Definition;
        return new(d.Key, d.Group, d.Description, d.Default, d.Secret, d.Secret ? null : setting.Value,
            setting.Value is not null, setting.Source);
    }

    /// <summary>The rate limit key for a client: its IPv4 address, or its IPv6 /64.</summary>
    public static string ClientKey(System.Net.IPAddress? address)
    {
        if (address is null) return "unknown";
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6) return address.ToString();
        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return new System.Net.IPAddress(bytes) + "/64";
    }
}
