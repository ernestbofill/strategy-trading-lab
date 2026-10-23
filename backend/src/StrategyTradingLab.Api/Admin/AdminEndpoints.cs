using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Email;
using StrategyTradingLab.Api.MarketData;

namespace StrategyTradingLab.Api.Admin;

/// <summary>What the admin page shows for a setting. A secret's value is never sent, only whether it is set.</summary>
public record AdminSetting(string Key, string Group, string Description, string? Default, bool Secret, string? Value,
    bool IsSet, SettingSource Source);

public record AdminLogIn(string Password);

/// <summary>Where the test email went.</summary>
public record TestEmailSent(string SentTo);

/// <summary>The database box: which one the API uses, this host's own, and each one's variable and host.</summary>
public record AdminDatabases(DatabaseName Active, DatabaseName Home, IReadOnlyList<DatabaseInfo> Databases);

public record DatabaseChoice(string Name);

/// <summary>The prices box: the next and last runs, what failed in the last one, and the newest stored day.</summary>
public record AdminPrices(bool Running, DateTimeOffset? NextRun, DateTimeOffset? LastStarted, DateTimeOffset? LastFinished,
    DateOnly? LatestDay, IReadOnlyList<RefreshResult> Problems, int Updated, int FullHistory);

/// <summary>One symbol asked of Yahoo by "Test Yahoo": its newest day and close, or why it didn't answer.</summary>
public record YahooCheck(string Symbol, bool Ok, DateOnly? Day, decimal? Close, string? Problem, int Milliseconds);

/// <summary>A value to save. For a secret, <c>Expected</c> holds the values the page showed for the settings it is bound to.</summary>
public record SettingValue(string Value, IReadOnlyDictionary<string, string?>? Expected = null);

/// <summary>
/// The admin settings page's API, behind a fixed password until accounts have an admin role (Ernest's call):
/// ADMIN_PASSWORD on the server, else "STL". Log-in sets a short-lived, encrypted, HttpOnly, SameSite=Strict cookie.
/// </summary>
public static class AdminEndpoints
{
    public const string LogInRateLimit = "admin-log-in";
    public const string TestEmailRateLimit = "admin-test-email";
    public const string TestYahooRateLimit = "admin-test-yahoo";
    const string CookieName = "stl_admin";
    const string DefaultPassword = "STL";
    static readonly TimeSpan SessionLength = TimeSpan.FromHours(8);

    public static void MapAdmin(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin").WithTags("Admin");

        group.MapPost("/log-in", Results<NoContent, UnauthorizedHttpResult> (AdminLogIn request, HttpContext http,
            IConfiguration configuration, IDataProtectionProvider dataProtection, TimeProvider time) =>
        {
            var password = AdminPassword(configuration);
            if (!PasswordMatches(request.Password ?? "", password)) return TypedResults.Unauthorized();

            var ticket = Protector(dataProtection).Protect(TicketFor(password), time.GetUtcNow() + SessionLength);
            http.Response.Cookies.Append(CookieName, ticket, new CookieOptions
            {
                HttpOnly = true, Secure = http.Request.IsHttps, SameSite = SameSiteMode.Strict,
                Path = "/", MaxAge = SessionLength,
            });
            return TypedResults.NoContent();
        })
        .RequireRateLimiting(LogInRateLimit)
        .WithSummary("Logs in to the admin page with its password.");

        group.MapPost("/log-out", (HttpContext http) =>
        {
            http.Response.Cookies.Delete(CookieName, new CookieOptions { Path = "/" });
            return TypedResults.NoContent();
        });

        var settings = group.MapGroup("/settings").AddEndpointFilter(RequireAdmin);
        MapDatabase(group);
        MapPrices(group);

        // Only ever to the account itself: with the page behind a fixed password, an address typed here would let
        // anyone who knows it send mail from the account.
        group.MapPost("/test-email", async Task<Results<Ok<TestEmailSent>, ProblemHttpResult>> (IEmailSender email,
            TimeProvider time, CancellationToken cancellationToken) =>
        {
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
        .AddEndpointFilter(RequireAdmin)
        .RequireRateLimiting(TestEmailRateLimit)
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
        var prices = group.MapGroup("/prices").AddEndpointFilter(RequireAdmin);

        prices.MapGet("/", async (PriceRefreshState state, AppDbContext db, CancellationToken cancellationToken) =>
        {
            var latest = await db.Prices.MaxAsync(p => (DateOnly?)p.Date, cancellationToken);
            var results = state.LastResults;
            return new AdminPrices(state.Running, state.NextRun, state.LastStarted, state.LastFinished, latest,
                [.. results.Where(r => r.Outcome == RefreshOutcome.Failed)],
                results.Count(r => r.Outcome == RefreshOutcome.Updated), results.Count(r => r.Outcome == RefreshOutcome.FullHistory));
        })
        .WithSummary("The price refresh: the next and last runs and what failed in the last one.");

        prices.MapPost("/refresh", Results<Accepted, Conflict<string>> (PriceRefreshState state) =>
            state.Request() ? TypedResults.Accepted((string?)null) : TypedResults.Conflict("A refresh is already running."))
        .WithSummary("Refreshes the prices now, in the background, whatever the schedule says.");

        prices.MapPost("/test", async (IMarketDataSource source, TimeProvider time, CancellationToken cancellationToken) =>
        {
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
            return checks;
        })
        .RequireRateLimiting(TestYahooRateLimit)
        .WithSummary("Asks Yahoo for a fund and an exchange rate and says whether it answered. Stores nothing.");
    }

    /// <summary>
    /// The database box: switch dev / production. Their URLs are only in the server's environment; a switch is tested
    /// first (see <see cref="DatabaseSwitch"/>). Outside Development it is refused while the admin password is the
    /// default, which anyone can read in this repo.
    /// </summary>
    static void MapDatabase(RouteGroupBuilder group)
    {
        var database = group.MapGroup("/database").AddEndpointFilter(RequireAdmin);

        database.MapGet("/", (DatabaseSwitch databases) => new AdminDatabases(databases.Active, databases.Home, databases.Databases()))
        .WithSummary("Which database the API uses, and each one's server variable and host.");

        database.MapPost("/active", (DatabaseChoice request, DatabaseSwitch databases, IConfiguration configuration,
            IHostEnvironment environment, CancellationToken cancellationToken) =>
            Switch(configuration, environment, request.Name, name => databases.SwitchAsync(name, cancellationToken)))
        .WithSummary("Switches the API to the dev or production database, once it answers and has every migration.");
    }

    static async Task<Results<NoContent, NotFound, ProblemHttpResult>> Switch(IConfiguration configuration, IHostEnvironment environment,
        string? name, Func<DatabaseName, Task> change)
    {
        // By name only: "dev" or "production". Enum.TryParse would also take a number.
        if (Enum.GetValues<DatabaseName>().Cast<DatabaseName?>().FirstOrDefault(d => d.ToString()!.Equals(name, StringComparison.OrdinalIgnoreCase))
            is not { } known)
            return TypedResults.NotFound();
        if (!environment.IsDevelopment() && configuration["ADMIN_PASSWORD"] is not { Length: > 0 })
            return TypedResults.Problem("Set ADMIN_PASSWORD on the server before changing the database here.",
                statusCode: StatusCodes.Status403Forbidden);
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

    static ITimeLimitedDataProtector Protector(IDataProtectionProvider dataProtection) =>
        dataProtection.CreateProtector("StrategyTradingLab.Admin.Session").ToTimeLimitedDataProtector();

    static async ValueTask<object?> RequireAdmin(EndpointFilterInvocationContext context, EndpointFilterDelegate next) =>
        IsAdmin(context.HttpContext) ? await next(context) : TypedResults.Unauthorized();

    /// <summary>Whether the request carries a valid admin cookie for the current admin password. Worked out once per request.</summary>
    public static bool IsAdmin(HttpContext http)
    {
        if (http.Items.TryGetValue(CookieName, out var known)) return (bool)known!;
        return (bool)(http.Items[CookieName] = CheckAdminCookie(http));
    }

    static bool CheckAdminCookie(HttpContext http)
    {
        if (http.Request.Cookies[CookieName] is not { } ticket) return false;
        var protector = Protector(http.RequestServices.GetRequiredService<IDataProtectionProvider>());
        try
        {
            return protector.Unprotect(ticket) == TicketFor(AdminPassword(http.RequestServices.GetRequiredService<IConfiguration>()));
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    /// <summary>The log-in rate limit's key for a client: its IPv4 address, or its IPv6 /64.</summary>
    public static string ClientKey(System.Net.IPAddress? address)
    {
        if (address is null) return "unknown";
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6) return address.ToString();
        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return new System.Net.IPAddress(bytes) + "/64";
    }

    static string AdminPassword(IConfiguration configuration) =>
        configuration["ADMIN_PASSWORD"] is { Length: > 0 } set ? set : DefaultPassword;

    /// <summary>The cookie's content: tied to the password, so changing ADMIN_PASSWORD logs every admin out.</summary>
    static string TicketFor(string password) => "admin:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password)))[..16];

    /// <summary>Compares hashes in constant time, so the answer's timing says nothing about the password.</summary>
    static bool PasswordMatches(string given, string expected) =>
        CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(given)), SHA256.HashData(Encoding.UTF8.GetBytes(expected)));
}
