using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using System.Threading.RateLimiting;
using StrategyTradingLab.Api.Assets;
using StrategyTradingLab.Api.Auth;
using StrategyTradingLab.Api.Email;
using StrategyTradingLab.Api.Feedback;
using StrategyTradingLab.Api.Notifications;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Common;

/// <summary>
/// The API's rate limits, each a fixed window: per client for the account and public endpoints (guessing, sign-up spam and
/// email floods hit these first), per user for the asset search and adding (both call the price source). Each policy's name is
/// a constant on its endpoints. <c>RateLimits:&lt;policy&gt;</c> overrides the permits; only the tests set it, to sign up many users.
/// </summary>
public static class RateLimits
{
    public static IServiceCollection AddAppRateLimits(this IServiceCollection services, IConfiguration configuration) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            // A null key: not limited.
            void Fixed(string policy, Func<HttpContext, string?> key, int permits, TimeSpan window) => options.AddPolicy(policy, http =>
                key(http) is { } partition
                    ? RateLimitPartition.GetFixedWindowLimiter(partition, _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = configuration.GetValue($"RateLimits:{policy}", permits), Window = window,
                    })
                    : RateLimitPartition.GetNoLimiter("unlimited"));
            void PerClient(string policy, int permits, TimeSpan window) => Fixed(policy, http => ClientKey(http.Connection.RemoteIpAddress), permits, window);

            PerClient(AccountEndpoints.SignUpLimit, 5, TimeSpan.FromHours(1));
            PerClient(AccountEndpoints.LogInLimit, 10, TimeSpan.FromMinutes(15));
            PerClient(AccountEndpoints.EmailLimit, 5, TimeSpan.FromHours(1));
            PerClient(AccountEndpoints.PasswordLimit, 10, TimeSpan.FromMinutes(15));
            PerClient(AccountEndpoints.LogOutLimit, 30, TimeSpan.FromMinutes(1));
            PerClient(FeedbackEndpoints.SubmitLimit, 30, TimeSpan.FromHours(1));
            PerClient(UnsubscribeEndpoints.UnsubscribeLimit, 120, TimeSpan.FromMinutes(1));
            PerClient(EmailEventEndpoints.EventsLimit, 600, TimeSpan.FromMinutes(1));
            PerClient(TemplateEndpoints.TemplatesLimit, 60, TimeSpan.FromMinutes(1));
            PerClient(Program.HealthLimit, 120, TimeSpan.FromMinutes(1));
            PerClient(Program.VersionLimit, 60, TimeSpan.FromMinutes(1));
            // A look-up with a code tells whether it works, so it is limited like a guess; one without a code (every Sign-up
            // load, many visitors behind one address) only says whether sign-ups are invite only, and isn't limited.
            Fixed(AccountEndpoints.InviteLimit,
                http => string.IsNullOrEmpty(http.Request.Query["invite"]) ? null : ClientKey(http.Connection.RemoteIpAddress),
                30, TimeSpan.FromHours(1));
            Fixed(AssetEndpoints.SearchLimit, UserKey, 100, TimeSpan.FromHours(1));
            Fixed(AssetEndpoints.AddLimit, UserKey, 10, TimeSpan.FromHours(1));
        });

    /// <summary>The rate limit key for a client: its IPv4 address, or its IPv6 /64.</summary>
    public static string ClientKey(IPAddress? address)
    {
        if (address is null) return "unknown";
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (address.AddressFamily != AddressFamily.InterNetworkV6) return address.ToString();
        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return new IPAddress(bytes) + "/64";
    }

    /// <summary>The rate limit key for a user: the user, or the client's address before the session is read.</summary>
    public static string UserKey(HttpContext http) =>
        http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ClientKey(http.Connection.RemoteIpAddress);
}
