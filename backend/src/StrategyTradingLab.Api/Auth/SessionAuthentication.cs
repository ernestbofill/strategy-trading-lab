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
/// session, 403 for the wrong role or an unconfirmed email.
/// </summary>
public static class SessionAuthentication
{
    public const string Scheme = "Session";
    public const string VerifiedEmailClaim = "email_verified";

    public static void AddSessionAuthorization(this IServiceCollection services)
    {
        services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(Scheme, null);
        services.AddAuthorization(options =>
        {
            // The scheme is named on each policy.
            options.DefaultPolicy = new AuthorizationPolicyBuilder(Scheme).RequireAuthenticatedUser().Build();
            options.AddPolicy(Policies.Owner, policy =>
                policy.AddAuthenticationSchemes(Scheme).RequireRole(nameof(UserRole.Owner)));
            options.AddPolicy(Policies.VerifiedEmail, policy =>
                policy.AddAuthenticationSchemes(Scheme).RequireClaim(VerifiedEmailClaim, "true"));
        });
    }
}

public static class Policies
{
    public const string Owner = "Owner";
    public const string VerifiedEmail = "VerifiedEmail";
}

class SessionAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
    UrlEncoder encoder, IServiceProvider services) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // ASP.NET authenticates the default scheme on every request: only endpoints that require authorization, and
        // only with a session cookie, look the session up (so /version and /health work with the database down).
        if (Context.GetEndpoint()?.Metadata.GetMetadata<IAuthorizeData>() is null
            || !Context.Request.Cookies.ContainsKey(Sessions.CookieName))
            return AuthenticateResult.NoResult();
        var sessions = services.GetRequiredService<Sessions>();
        if (await sessions.CurrentAsync(Context.RequestAborted) is not { } user) return AuthenticateResult.NoResult();
        List<Claim> claims = [new(ClaimTypes.NameIdentifier, user.Id.ToString()), new(ClaimTypes.Role, user.Role.ToString())];
        if (user.EmailVerifiedAt is not null) claims.Add(new(SessionAuthentication.VerifiedEmailClaim, "true"));
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }
}
