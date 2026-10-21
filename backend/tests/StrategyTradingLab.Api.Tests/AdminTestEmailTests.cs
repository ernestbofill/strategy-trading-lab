using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using StrategyTradingLab.Api.Email;

namespace StrategyTradingLab.Api.Tests;

/// <summary>The admin page's test email, with a fake sender (no database, no SMTP). Each test has its own API and rate limit.</summary>
public class AdminTestEmailTests
{
    sealed class FakeSender(string? account, Exception? failure = null) : IEmailSender
    {
        public List<string> SentTo { get; } = [];
        public Task<string?> AccountAsync(CancellationToken cancellationToken) => Task.FromResult(account);

        public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken)
        {
            if (failure is not null) throw failure;
            SentTo.Add(to);
            return Task.CompletedTask;
        }
    }

    static WebApplicationFactory<Program> Api(FakeSender sender) => new WebApplicationFactory<Program>()
        .WithWebHostBuilder(b => b.ConfigureTestServices(services => services.AddScoped<IEmailSender>(_ => sender)));

    static async Task<HttpClient> Admin(WebApplicationFactory<Program> api)
    {
        var client = api.CreateClient();
        (await client.PostAsJsonAsync("/admin/log-in", new { password = "STL" })).EnsureSuccessStatusCode();
        return client;
    }

    [Fact]
    public async Task It_goes_only_to_the_mail_account_itself()
    {
        var sender = new FakeSender("me@gmail.com");
        using var api = Api(sender);
        using var admin = await Admin(api);

        var response = await admin.PostAsJsonAsync("/admin/test-email", new { to = "someone.else@example.com" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("me@gmail.com", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sentTo").GetString());
        Assert.Equal(["me@gmail.com"], sender.SentTo);
    }

    [Fact]
    public async Task It_needs_the_admin_cookie_and_strangers_dont_use_up_the_limit()
    {
        var sender = new FakeSender("me@gmail.com");
        using var api = Api(sender);
        using var stranger = api.CreateClient();

        for (var i = 0; i < 6; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.PostAsync("/admin/test-email", null)).StatusCode);

        Assert.Empty(sender.SentTo);
        using var admin = await Admin(api);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync("/admin/test-email", null)).StatusCode);
    }

    [Fact]
    public async Task Without_an_account_it_says_so()
    {
        using var api = Api(new FakeSender(null));
        using var admin = await Admin(api);

        var response = await admin.PostAsync("/admin/test-email", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Set the email account first.", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task A_refused_send_passes_on_the_reason()
    {
        using var api = Api(new FakeSender("me@gmail.com", new EmailException("smtp.gmail.com:587 refused it: Username and Password not accepted")));
        using var admin = await Admin(api);

        var response = await admin.PostAsync("/admin/test-email", null);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Contains("Username and Password not accepted", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task The_sixth_test_email_in_an_hour_is_refused()
    {
        using var api = Api(new FakeSender("me@gmail.com"));
        using var admin = await Admin(api);
        for (var i = 0; i < 5; i++) Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync("/admin/test-email", null)).StatusCode);

        Assert.Equal(HttpStatusCode.TooManyRequests, (await admin.PostAsync("/admin/test-email", null)).StatusCode);
    }
}
