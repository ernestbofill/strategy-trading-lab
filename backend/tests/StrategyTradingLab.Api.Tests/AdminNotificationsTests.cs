using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Notifications;

namespace StrategyTradingLab.Api.Tests;

/// <summary>/admin/notifications: a test push to a user's devices, with a fake push service, and the list that records it.</summary>
public class AdminNotificationsTests
{
    sealed class FakePush : IPushSender
    {
        public List<string> Payloads { get; } = [];
        public string? PublicKey => "key";
        public Task<PushOutcome> SendAsync(PushSubscription subscription, string payload, CancellationToken cancellationToken)
        {
            Payloads.Add(payload);
            return Task.FromResult(PushOutcome.Sent);
        }
    }

    sealed class Api(FakePush push) : AccountsApi
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services => services.AddSingleton<IPushSender>(push));
        }
    }

    [NeonFact]
    public async Task A_test_push_reaches_the_users_devices_and_is_listed_every_time()
    {
        var push = new FakePush();
        using var api = new Api(push);
        using var admin = await api.OwnerAsync();
        var (_, email) = await api.VerifiedAsync();
        Guid userId;
        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            userId = await db.Users.Where(u => (string)u.Email == email).Select(u => u.Id).SingleAsync();
            db.PushSubscriptions.Add(new PushSubscription { UserId = userId, Endpoint = $"https://fcm.googleapis.com/fcm/send/{Guid.NewGuid():N}", P256dh = "p", Auth = "a" });
            await db.SaveChangesAsync();
        }

        var page = await admin.GetFromJsonAsync<JsonElement>("/admin/notifications");
        Assert.Contains(page.GetProperty("recipients").EnumerateArray(), r => r.GetProperty("email").GetString() == email);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/admin/notifications/test", new { userId, title = " ", body = "Hi" })).StatusCode);
        // Twice the same day: a test push isn't deduped.
        for (var i = 0; i < 2; i++)
        {
            var response = await admin.PostAsJsonAsync("/admin/notifications/test", new { userId, title = "Hello", body = $"Test {i}" });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sentAt").GetString());
        }
        Assert.Equal(2, push.Payloads.Count);
        Assert.Contains("\"title\":\"Hello\"", push.Payloads[0]);

        var listed = (await admin.GetFromJsonAsync<JsonElement>("/admin/notifications")).GetProperty("pushes").EnumerateArray()
            .Where(p => p.GetProperty("userId").GetGuid() == userId).ToList();
        Assert.Equal(["Test 1", "Test 0"], listed.Select(p => p.GetProperty("body").GetString()));
        Assert.All(listed, p => Assert.Equal("test", p.GetProperty("kind").GetString()));

        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/admin/notifications/test",
            new { userId = Guid.NewGuid(), title = "Hello", body = "Hi" })).StatusCode);
    }
}
