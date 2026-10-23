using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Auth;

/// <summary>
/// Log-in sessions: a random token in the HttpOnly stl_session cookie, its hash in `sessions`. A session lasts 30
/// days from its last use, 90 at most. Also answers "who is calling?" once per request.
/// </summary>
public class Sessions(AppDbContext db, TimeProvider time, IHttpContextAccessor accessor, IConfiguration configuration)
{
    public const string CookieName = "stl_session";
    public static readonly TimeSpan IdleLength = TimeSpan.FromDays(30), MaxLength = TimeSpan.FromDays(90);
    static readonly TimeSpan TouchEvery = TimeSpan.FromMinutes(1);
    const string ItemKey = "stl.user";

    HttpContext Http => accessor.HttpContext ?? throw new InvalidOperationException("Sessions need a request.");

    /// <summary>The logged-in user, or null. Looked up once per request.</summary>
    public async Task<User?> CurrentAsync(CancellationToken cancellationToken)
    {
        if (Http.Items.TryGetValue(ItemKey, out var known)) return (User?)known;
        var user = await Resolve(cancellationToken);
        Http.Items[ItemKey] = user;
        return user;
    }

    async Task<User?> Resolve(CancellationToken cancellationToken)
    {
        if (Http.Request.Cookies[CookieName] is not { Length: > 0 and < 100 } token) return null;
        var hash = SecretTokens.Hash(token);
        var now = time.GetUtcNow();
        var session = await db.Sessions.SingleOrDefaultAsync(s => s.TokenHash == hash, cancellationToken);
        if (session is null || session.ExpiresAt <= now)
        {
            DeleteCookie();
            return null;
        }
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == session.UserId, cancellationToken);
        if (user is null) return null;
        if (BecomesOwner(user))
        {
            user.Role = UserRole.Owner;
            await db.SaveChangesAsync(cancellationToken);
        }
        if (now - session.LastUsedAt >= TouchEvery)
        {
            session.LastUsedAt = now;
            session.ExpiresAt = Expiry(session.CreatedAt, now);
            await db.SaveChangesAsync(cancellationToken);
            WriteCookie(token, session.ExpiresAt);
        }
        return user;
    }

    /// <summary>
    /// The owner is the user with the verified email in OWNER_EMAIL on the server, set to the owner role the next time
    /// they are looked up (so it holds on whichever database is in use). Other roles are set in the database.
    /// </summary>
    bool BecomesOwner(User user) =>
        user.Role != UserRole.Owner && user.EmailVerifiedAt is not null
        && configuration["OWNER_EMAIL"] is { Length: > 0 } owner && string.Equals(owner.Trim(), user.Email, StringComparison.OrdinalIgnoreCase);

    /// <summary>Logs this device in as the user: a new session and its cookie. The session it replaces, if any, ends.</summary>
    public async Task StartAsync(User user, CancellationToken cancellationToken)
    {
        if (Http.Request.Cookies[CookieName] is { Length: > 0 and < 100 } previous)
        {
            var previousHash = SecretTokens.Hash(previous);
            await db.Sessions.Where(s => s.TokenHash == previousHash).ExecuteDeleteAsync(cancellationToken);
        }
        var (token, hash) = SecretTokens.New();
        var now = time.GetUtcNow();
        var userAgent = Http.Request.Headers.UserAgent.ToString();
        db.Sessions.Add(new UserSession
        {
            UserId = user.Id, TokenHash = hash, CreatedAt = now, LastUsedAt = now, ExpiresAt = Expiry(now, now),
            UserAgent = userAgent.Length == 0 ? null : userAgent[..Math.Min(userAgent.Length, 300)],
        });
        await db.SaveChangesAsync(cancellationToken);
        WriteCookie(token, Expiry(now, now));
        Http.Items[ItemKey] = user;
    }

    /// <summary>Logs this device out.</summary>
    public async Task EndAsync(CancellationToken cancellationToken)
    {
        if (Http.Request.Cookies[CookieName] is { Length: > 0 } token)
        {
            var hash = SecretTokens.Hash(token);
            await db.Sessions.Where(s => s.TokenHash == hash).ExecuteDeleteAsync(cancellationToken);
        }
        DeleteCookie();
        Http.Items[ItemKey] = null;
    }

    /// <summary>Ends every session of the user, except this device's when <paramref name="keepThisOne"/>.</summary>
    public Task EndAllAsync(Guid userId, bool keepThisOne, CancellationToken cancellationToken)
    {
        var sessions = db.Sessions.Where(s => s.UserId == userId);
        if (keepThisOne && Http.Request.Cookies[CookieName] is { Length: > 0 } token)
        {
            var hash = SecretTokens.Hash(token);
            sessions = sessions.Where(s => s.TokenHash != hash);
        }
        return sessions.ExecuteDeleteAsync(cancellationToken);
    }

    static DateTimeOffset Expiry(DateTimeOffset createdAt, DateTimeOffset now) =>
        new[] { now + IdleLength, createdAt + MaxLength }.Min();

    void WriteCookie(string token, DateTimeOffset expires) =>
        Http.Response.Cookies.Append(CookieName, token, new CookieOptions
        {
            HttpOnly = true, Secure = Http.Request.IsHttps, SameSite = SameSiteMode.Lax, Path = "/", Expires = expires,
        });

    void DeleteCookie() => Http.Response.Cookies.Delete(CookieName, new CookieOptions { Path = "/" });
}

/// <summary>401 without a logged-in user (see <see cref="SessionAuthentication"/>).</summary>
public static class RequireUserExtensions
{
    public static TBuilder RequireUser<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization();
}
