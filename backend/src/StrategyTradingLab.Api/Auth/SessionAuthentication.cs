using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Auth;

/// <summary>
/// Plugs the log-in session into ASP.NET authorization: the logged-in user becomes a principal with a role claim, so
/// endpoints use <c>RequireAuthorization()</c> (any user) or a policy such as <see cref="Policies.Owner"/>. 401 without a
/// session, 403 for the wrong role.
/// </summary>
public static class SessionAuthentication
{
    public const string Scheme = "Session";

    public static void AddSessionAuthorization(this IServiceCollection services)
    {
        services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(Scheme, null);
        services.AddAuthorization(options =>
        {
            // The scheme is named on each policy, so only endpoints that need a user look the session up.
            options.DefaultPolicy = new AuthorizationPolicyBuilder(Scheme).RequireAuthenticatedUser().Build();
            options.AddPolicy(Policies.Owner, policy =>
                policy.AddAuthenticationSchemes(Scheme).RequireRole(nameof(UserRole.Owner)));
        });
    }
}

public static class Policies
{
    public const string Owner = "Owner";
}

class SessionAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
    UrlEncoder encoder, IServiceProvider services) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // ASP.NET authenticates the default scheme on every request: without a session cookie, don't open the database.
        if (!Context.Request.Cookies.ContainsKey(Sessions.CookieName)) return AuthenticateResult.NoResult();
        var sessions = services.GetRequiredService<Sessions>();
        if (await sessions.CurrentAsync(Context.RequestAborted) is not { } user) return AuthenticateResult.NoResult();
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Role, user.Role.ToString())],
            Scheme.Name);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }
}
