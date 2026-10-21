using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.HttpResults;

namespace StrategyTradingLab.Api.Admin;

/// <summary>What the admin page shows for a setting. A secret's value is never sent, only whether it is set.</summary>
public record AdminSetting(string Key, string Group, string Description, string? Default, bool Secret, string? Value,
    bool IsSet, SettingSource Source);

public record AdminLogIn(string Password);

public record SettingValue(string Value);

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
            if (!PasswordMatches(request.Password ?? "", configuration["ADMIN_PASSWORD"] is { Length: > 0 } set ? set : DefaultPassword))
                return TypedResults.Unauthorized();

            var ticket = Protector(dataProtection).Protect("admin", time.GetUtcNow() + SessionLength);
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

        settings.MapPut("/{key}", async Task<Results<NoContent, NotFound, ValidationProblem>> (string key, SettingValue request,
            SettingsStore store, CancellationToken cancellationToken) =>
        {
            if (SettingDefinitions.Find(key) is not { } definition) return TypedResults.NotFound();
            var value = (request.Value ?? "").Trim();
            var error = value.Length == 0 ? "Type a value, or use Default." : value.Length > 500 ? "Use at most 500 characters." : definition.Validate?.Invoke(value);
            if (error is not null) return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["value"] = [error] });

            await store.SetAsync(definition, value, cancellationToken);
            return TypedResults.NoContent();
        })
        .WithSummary("Sets a setting. Changing the mail server or user clears the saved password.");

        settings.MapDelete("/{key}", async Task<Results<NoContent, NotFound>> (string key, bool? clear, SettingsStore store,
            CancellationToken cancellationToken) =>
        {
            if (SettingDefinitions.Find(key) is null) return TypedResults.NotFound();
            if (clear == true) await store.ClearAsync(key, cancellationToken);
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
            if (http.Request.Cookies[CookieName] is { } ticket && protector.Unprotect(ticket) == "admin") return await next(context);
        }
        catch (CryptographicException) { }
        return TypedResults.Unauthorized();
    }

    /// <summary>Compares hashes in constant time, so the answer's timing says nothing about the password.</summary>
    static bool PasswordMatches(string given, string expected) =>
        CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(given)), SHA256.HashData(Encoding.UTF8.GetBytes(expected)));
}
