using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.HttpResults;
using StrategyTradingLab.Api.Auth;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Email;

namespace StrategyTradingLab.Api.Admin;

/// <summary>What the admin page shows for a setting. A secret's value is never sent, only whether it is set.</summary>
public record AdminSetting(string Key, string Group, string Description, string? Default, bool Secret, string? Value,
    bool IsSet, SettingSource Source);

/// <summary>Where the test email went.</summary>
public record TestEmailSent(string SentTo);

/// <summary>The database box: which one the API uses, this host's own, and each one's variable and host.</summary>
public record AdminDatabases(DatabaseName Active, DatabaseName Home, IReadOnlyList<DatabaseInfo> Databases);

public record DatabaseChoice(string Name);

/// <summary>A value to save. For a secret, <c>Expected</c> holds the values the page showed for the settings it is bound to.</summary>
public record SettingValue(string Value, IReadOnlyDictionary<string, string?>? Expected = null);

/// <summary>5 test emails an hour in all, so the page can't be used to flood the account's inbox or Gmail's sending limit.</summary>
public sealed class TestEmailLimit : IDisposable
{
    readonly FixedWindowRateLimiter limiter = new(new FixedWindowRateLimiterOptions
        { PermitLimit = 5, Window = TimeSpan.FromHours(1), QueueLimit = 0 });

    public bool TryAcquire() => limiter.AttemptAcquire().IsAcquired;

    public void Dispose() => limiter.Dispose();
}

/// <summary>
/// The admin settings page's API, for the owner only: a logged-in user whose role is <see cref="UserRole.Owner"/>.
/// 401 without a log-in, 403 for any other role.
/// </summary>
public static class AdminEndpoints
{
    public static void MapAdmin(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin").WithTags("Admin");

        var settings = group.MapGroup("/settings").AddEndpointFilter(RequireAdmin);
        MapDatabase(group);

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
        .AddEndpointFilter(RequireAdmin)
        .WithSummary("Sends a test email to the mail account itself, with the settings as they are now.");

        settings.MapGet("/", async (SettingsStore store, CancellationToken cancellationToken) =>
            (await store.AllAsync(cancellationToken)).Select(Dto).ToList())
        .WithSummary("The settings the admin page can set, with where each value comes from.");

        settings.MapPut("/{key}", async Task<Results<NoContent, NotFound, ValidationProblem, Conflict<string>>> (string key,
            SettingValue request, SettingsStore store, CancellationToken cancellationToken) =>
        {
            if (SettingDefinitions.Find(key) is not { } definition) return TypedResults.NotFound();
            // A secret is kept exactly as typed: a password may start or end with a space.
            var value = string.IsNullOrWhiteSpace(request.Value) ? "" : definition.Secret ? request.Value : request.Value.Trim();
            var error = value.Length == 0 ? "Type a value, or use Default."
                : value.Length > 500 ? "Use at most 500 characters."
                : value.Any(char.IsControl) ? "Use printable characters only."
                : definition.Validate?.Invoke(value);
            if (error is not null) return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["value"] = [error] });

            return await store.SetAsync(definition, value, request.Expected, cancellationToken)
                ? TypedResults.NoContent()
                : TypedResults.Conflict("The mail settings changed since this page loaded. Reload, check them, then type the password again.");
        })
        .WithSummary("Sets a setting. The saved password only counts while the mail server, port and user it was saved with stay.");

        settings.MapDelete("/{key}", async Task<Results<NoContent, NotFound>> (string key, bool? clear, SettingsStore store,
            CancellationToken cancellationToken) =>
        {
            if (SettingDefinitions.Find(key) is not { } definition) return TypedResults.NotFound();
            if (clear == true) await store.ClearAsync(definition, cancellationToken);
            else await store.ResetAsync(key, cancellationToken);
            return TypedResults.NoContent();
        })
        .WithSummary("Back to the server's value or the default; with ?clear=true, no value at all.");
    }

    /// <summary>
    /// The database box: switch dev / production. Their URLs are only in the server's environment; a switch is tested
    /// first (see <see cref="DatabaseSwitch"/>).
    /// </summary>
    static void MapDatabase(RouteGroupBuilder group)
    {
        var database = group.MapGroup("/database").AddEndpointFilter(RequireAdmin);

        database.MapGet("/", (DatabaseSwitch databases) => new AdminDatabases(databases.Active, databases.Home, databases.Databases()))
        .WithSummary("Which database the API uses, and each one's server variable and host.");

        database.MapPost("/active", (DatabaseChoice request, DatabaseSwitch databases, CancellationToken cancellationToken) =>
            Switch(request.Name, name => databases.SwitchAsync(name, cancellationToken)))
        .WithSummary("Switches the API to the dev or production database, once it answers and has every migration.");
    }

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

    static async ValueTask<object?> RequireAdmin(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var user = await http.RequestServices.GetRequiredService<Sessions>().CurrentAsync(http.RequestAborted);
        if (user is null) return TypedResults.Unauthorized();
        return user.Role == UserRole.Owner ? await next(context) : TypedResults.StatusCode(StatusCodes.Status403Forbidden);
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
