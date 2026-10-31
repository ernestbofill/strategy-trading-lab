using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using StrategyTradingLab.Api.Auth;
using StrategyTradingLab.Api.Email;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Tests;

public class SecurityTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Api_responses_include_security_headers()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/version");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").FirstOrDefault());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").FirstOrDefault());
        Assert.Equal("strict-origin-when-cross-origin", response.Headers.GetValues("Referrer-Policy").FirstOrDefault());
        Assert.Equal("default-src 'none'; frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").FirstOrDefault());
    }

    [Fact]
    public async Task Verification_email_warns_unintended_recipient_and_provides_reclaim_link()
    {
        var queue = new EmailQueue(NullLogger<EmailQueue>.Instance);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PublicUrl"] = "https://strategytradinglab.com"
        }).Build();
        var emails = new AccountEmails(queue, config);

        emails.Verify("test@example.com", "token123", "en");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        OutgoingEmail? message = null;
        await foreach (var item in queue.ReadAllAsync(cts.Token))
        {
            message = item;
            break;
        }

        Assert.NotNull(message);
        Assert.Contains("If you didn't sign up for Strategy Trading Lab, ignore this email: the account will stay unconfirmed.", message!.Body);
        Assert.Contains("https://strategytradinglab.com/forgot-password", message.Body);
    }

    [Fact]
    public async Task Public_endpoints_enforce_rate_limits()
    {
        using var limitedFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting($"RateLimits:{Program.VersionLimit}", "1");
            builder.UseSetting($"RateLimits:{TemplateEndpoints.TemplatesLimit}", "1");
            builder.UseSetting($"RateLimits:{AccountEndpoints.LogOutLimit}", "1");
            builder.UseSetting($"RateLimits:{Program.HealthLimit}", "1");
        });

        var client = limitedFactory.CreateClient();

        // Version rate limit
        var version1 = await client.GetAsync("/version");
        Assert.Equal(HttpStatusCode.OK, version1.StatusCode);
        var version2 = await client.GetAsync("/version");
        Assert.Equal(HttpStatusCode.TooManyRequests, version2.StatusCode);

        // Health rate limit
        await client.GetAsync("/health");
        var health2 = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.TooManyRequests, health2.StatusCode);

        // Templates rate limit
        await client.GetAsync("/templates");
        var templates2 = await client.GetAsync("/templates");
        Assert.Equal(HttpStatusCode.TooManyRequests, templates2.StatusCode);

        // Log-out rate limit (requires X-Requested-With header)
        using var logoutReq1 = new HttpRequestMessage(HttpMethod.Post, "/account/log-out");
        logoutReq1.Headers.Add("X-Requested-With", "stl");
        await client.SendAsync(logoutReq1);

        using var logoutReq2 = new HttpRequestMessage(HttpMethod.Post, "/account/log-out");
        logoutReq2.Headers.Add("X-Requested-With", "stl");
        var logout2 = await client.SendAsync(logoutReq2);
        Assert.Equal(HttpStatusCode.TooManyRequests, logout2.StatusCode);
    }
}
