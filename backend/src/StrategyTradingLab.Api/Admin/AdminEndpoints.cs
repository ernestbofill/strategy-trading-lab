using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.HttpResults;

namespace StrategyTradingLab.Api.Admin;

/// <summary>What the admin page shows for a setting. A secret's value is never sent, only whether it is set.</summary>
public record AdminSetting(string Key, string Group, string Description, string? Default, bool Secret, string? Value,
    bool IsSet, SettingSource Source);

public record AdminLogIn(string Password);

/// <summary>A value to save. For a secret, <c>Expected</c> holds the values the page showed for the settings it is bound to.</summary>
public record SettingValue(string Value, IReadOnlyDictionary<string, string?>? Expected = null);

/// <summary>
/// The admin settings page's API, behind a fixed password until accounts have an admin role (Ernest's call):
/// ADMIN_PASSWORD on the server, else "STL". Log-in sets a short-lived, encrypted, HttpOnly, SameSite=Strict cookie.
/// </summary>
public static class AdminEndpoints
{
    public const string LogInRateLimit = "admin-log-in";
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

    static AdminSetting Dto(EffectiveSetting setting)
    {
        var d = setting.Definition;
        return new(d.Key, d.Group, d.Description, d.Default, d.Secret, d.Secret ? null : setting.Value,
            setting.Value is not null, setting.Source);
    }

    static ITimeLimitedDataProtector Protector(IDataProtectionProvider dataProtection) =>
        dataProtection.CreateProtector("StrategyTradingLab.Admin.Session").ToTimeLimitedDataProtector();

    static async ValueTask<object?> RequireAdmin(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var protector = Protector(http.RequestServices.GetRequiredService<IDataProtectionProvider>());
        try
        {
            var expected = TicketFor(AdminPassword(http.RequestServices.GetRequiredService<IConfiguration>()));
            if (http.Request.Cookies[CookieName] is { } ticket && protector.Unprotect(ticket) == expected) return await next(context);
        }
        catch (CryptographicException) { }
        return TypedResults.Unauthorized();
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
