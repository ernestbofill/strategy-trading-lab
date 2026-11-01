using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using StrategyTradingLab.Api.Data.Entities;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using StrategyTradingLab.Api.Email;

namespace StrategyTradingLab.Api.Tests;

/// <summary>The admin page's test email, with a fake sender (no SMTP). Each test has its own API and rate limit.</summary>
public class AdminTestEmailTests
{
    sealed class FakeSender(bool setUp = true, Exception? failure = null) : IEmailSender
    {
        public List<string> SentTo { get; } = [];

        public Task SendAsync(OutgoingEmail email, CancellationToken cancellationToken)
        {
            if (!email.Subject.EndsWith("test email")) return Task.CompletedTask; // The sign-ups' own emails.
            if (!setUp) throw new EmailNotSetUpException();
            if (failure is not null) throw failure;
            SentTo.Add(email.To);
            return Task.CompletedTask;
        }
    }

    sealed class Api(FakeSender sender) : AccountsApi
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services => services.AddScoped<IEmailSender>(_ => sender));
        }
    }

    static Task<HttpClient> Admin(Api api) => api.OwnerAsync();

    [NeonFact]
    public async Task It_goes_only_to_the_signed_in_owner()
    {
        var sender = new FakeSender();
        using var api = new Api(sender);
        using var admin = await Admin(api);
        var me = (await admin.GetFromJsonAsync<JsonElement>("/account/me")).GetProperty("email").GetString()!;

        var response = await admin.PostAsJsonAsync("/admin/test-email", new { to = "someone.else@example.com" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(me, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sentTo").GetString());
        Assert.Equal([me], sender.SentTo);
    }

    [NeonFact]
    public async Task It_needs_the_owner_and_others_dont_use_up_the_limit()
    {
        var sender = new FakeSender();
        using var api = new Api(sender);
        using var stranger = api.CreateApiClient();

        for (var i = 0; i < 6; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.PostAsync("/admin/test-email", null)).StatusCode);

        using var user = await api.SignedUpAsync(UserRole.User);
        for (var i = 0; i < 6; i++)
            Assert.Equal(HttpStatusCode.Forbidden, (await user.PostAsync("/admin/test-email", null)).StatusCode);

        Assert.Empty(sender.SentTo);
        using var admin = await Admin(api);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync("/admin/test-email", null)).StatusCode);
    }

    [NeonFact]
    public async Task Without_an_account_it_says_so()
    {
        using var api = new Api(new FakeSender(false));
        using var admin = await Admin(api);

        var response = await admin.PostAsync("/admin/test-email", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.StartsWith("Email isn't set up", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString());
    }

    [NeonFact]
    public async Task A_refused_send_passes_on_the_reason()
    {
        using var api = new Api(new FakeSender(true, new EmailException("smtp.gmail.com:587 refused it: Username and Password not accepted")));
        using var admin = await Admin(api);

        var response = await admin.PostAsync("/admin/test-email", null);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Contains("Username and Password not accepted", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString());
    }

    [NeonFact]
    public async Task The_sixth_test_email_in_an_hour_is_refused()
    {
        using var api = new Api(new FakeSender());
        using var admin = await Admin(api);
        for (var i = 0; i < 5; i++) Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync("/admin/test-email", null)).StatusCode);

        Assert.Equal(HttpStatusCode.TooManyRequests, (await admin.PostAsync("/admin/test-email", null)).StatusCode);
    }
}
