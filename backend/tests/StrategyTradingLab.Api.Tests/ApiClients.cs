using System.Collections.Concurrent;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using StrategyTradingLab.Api.Auth;
using StrategyTradingLab.Api.Email;

namespace StrategyTradingLab.Api.Tests;

static class ApiClients
{
    /// <summary>A client like the app: it sends the X-Requested-With header every state-changing request needs.</summary>
    public static HttpClient CreateApiClient(this WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Requested-With", "stl");
        return client;
    }

    public static object SignUpBody(string email, string password = "a long password") =>
        new { email, password, locale = "en", timeZone = "Europe/Madrid", overviewEmail = true, disclaimerAccepted = true };
}

/// <summary>Collects the emails the app sends, instead of sending them.</summary>
public sealed class CapturedEmails : IEmailSender
{
    readonly ConcurrentQueue<OutgoingEmail> sent = new();

    public Task<string> SendToAccountAsync(string subject, string body, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken)
    {
        sent.Enqueue(new(to, subject, body));
        return Task.CompletedTask;
    }

    /// <summary>The email to that address with that subject start, waiting for the background worker.</summary>
    /// <summary>The latest matching email, or with <paramref name="skip"/> the one after that many earlier matches.</summary>
    public async Task<OutgoingEmail?> WaitForAsync(string to, string subjectStart, int timeoutMs = 3000, int? skip = null)
    {
        for (var waited = 0; waited < timeoutMs; waited += 50)
        {
            var matches = sent.Where(e => e.To == to && e.Subject.StartsWith(subjectStart)).ToList();
            if ((skip is { } n ? matches.ElementAtOrDefault(n) : matches.LastOrDefault()) is { } email) return email;
            await Task.Delay(50);
        }
        return null;
    }

    public bool AnyTo(string to) => sent.Any(e => e.To == to);

    /// <summary>The token in an email's link.</summary>
    public static string Token(OutgoingEmail email) =>
        System.Text.RegularExpressions.Regex.Match(email.Body, @"token=([A-Za-z0-9_\-]+)").Groups[1].Value;
}

/// <summary>
/// An API whose emails are captured and whose account rate limits are high enough for the tests. The users it signs
/// up share a tag in their email and are deleted (with their strategies) when it is disposed.
/// </summary>
public class AccountsApi : WebApplicationFactory<Program>
{
    readonly string tag = Guid.NewGuid().ToString("N")[..12];
    public CapturedEmails Emails { get; } = new();

    public string NewEmail() => $"test-{tag}-{Guid.NewGuid():N}@example.com";

    /// <summary>A new account, logged in on the returned client.</summary>
    public async Task<(HttpClient Client, string Email)> SignedUpAsync(string password = "a long password")
    {
        var email = NewEmail();
        var client = this.CreateApiClient();
        (await client.PostAsJsonAsync("/account/sign-up", ApiClients.SignUpBody(email, password))).EnsureSuccessStatusCode();
        return (client, email);
    }

    public override async ValueTask DisposeAsync()
    {
        if (NeonFactAttribute.Configured)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<StrategyTradingLab.Api.Data.AppDbContext>();
            var prefix = $"test-{tag}-";
            await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteDeleteAsync(
                db.Users.Where(u => ((string)u.Email).StartsWith(prefix)));
        }
        await base.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        foreach (var policy in new[] { AccountEndpoints.SignUpLimit, AccountEndpoints.LogInLimit, AccountEndpoints.EmailLimit, AccountEndpoints.PasswordLimit })
            builder.UseSetting($"RateLimits:{policy}", "1000");
        builder.ConfigureTestServices(services => services.AddScoped<IEmailSender>(_ => Emails));
    }
}
