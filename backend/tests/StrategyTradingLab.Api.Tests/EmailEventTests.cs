using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Email;

namespace StrategyTradingLab.Api.Tests;

public class EmailEventTests
{
    const string Key = "a-long-enough-webhook-key";

    static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> WithKey(AccountsApi api, string? key = Key) =>
        api.WithWebHostBuilder(b => { if (key is not null) b.UseSetting("BREVO_WEBHOOK_KEY", key); });

    static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    static async Task<List<EmailSuppression>> Rows(AccountsApi api, params string[] emails)
    {
        using var scope = api.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().EmailSuppressions.AsNoTracking()
            .Where(s => emails.Contains((string)s.Email)).ToListAsync();
    }

    static async Task Clean(AccountsApi api, params string[] emails)
    {
        using var scope = api.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().EmailSuppressions.Where(s => emails.Contains((string)s.Email)).ExecuteDeleteAsync();
    }

    [NeonFact]
    public async Task Bounces_invalid_addresses_and_complaints_are_recorded_and_other_events_are_not()
    {
        using var api = new AccountsApi();
        string[] emails = [api.NewEmail(), api.NewEmail(), api.NewEmail(), api.NewEmail()];
        try
        {
            using var brevo = WithKey(api).CreateClient(); // No cookie and no X-Requested-With header, as Brevo calls it.
            foreach (var (kind, email) in new[] { ("hard_bounce", emails[0]), ("invalid_email", emails[1]), ("spam", emails[2]), ("delivered", emails[3]), ("soft_bounce", emails[3]) })
                Assert.Equal(HttpStatusCode.NoContent, (await brevo.PostAsync($"/email/events?key={Key}", Json($$"""{"event":"{{kind}}","email":"{{email}}","id":1}"""))).StatusCode);

            var rows = (await Rows(api, emails)).ToDictionary(r => r.Email, r => r.Reason);
            Assert.Equal(SuppressionReason.Bounced, rows[emails[0]]);
            Assert.Equal(SuppressionReason.Invalid, rows[emails[1]]);
            Assert.Equal(SuppressionReason.Complaint, rows[emails[2]]);
            Assert.False(rows.ContainsKey(emails[3]));
        }
        finally
        {
            await Clean(api, emails);
        }
    }

    [NeonFact]
    public async Task A_repeated_event_a_batch_and_nonsense_are_all_fine()
    {
        using var api = new AccountsApi();
        string[] emails = [api.NewEmail(), api.NewEmail()];
        try
        {
            using var brevo = WithKey(api).CreateClient();
            var body = Json($$"""[{"event":"hard_bounce","email":"{{emails[0]}}"},{"event":"hard_bounce","email":"{{emails[0]}}"},{"event":"spam","email":"{{emails[1]}}"},{"event":"hard_bounce"},"x",{"email":"nobody@example.com"}]""");

            Assert.Equal(HttpStatusCode.NoContent, (await brevo.PostAsync($"/email/events?key={Key}", body)).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await brevo.PostAsync($"/email/events?key={Key}", Json($$"""{"event":"hard_bounce","email":"{{emails[0]}}"}"""))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await brevo.PostAsync($"/email/events?key={Key}", Json("{not json"))).StatusCode);

            Assert.Equal(2, (await Rows(api, emails)).Count);
        }
        finally
        {
            await Clean(api, emails);
        }
    }

    [NeonFact]
    public async Task Odd_values_and_a_wrong_content_type_never_make_a_500()
    {
        using var api = new AccountsApi();
        var email = api.NewEmail();
        try
        {
            using var brevo = WithKey(api).CreateClient();
            Assert.Equal(HttpStatusCode.NoContent, (await brevo.PostAsync($"/email/events?key={Key}", Json("""[{"event":"hard_bounce","email":5},{"event":1,"email":"a@b.c"},{"event":null,"email":null}]"""))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await brevo.PostAsync($"/email/events?key={Key}", new StringContent("event=hard_bounce", Encoding.UTF8, "text/plain"))).StatusCode);
            Assert.Empty(await Rows(api, email));
        }
        finally
        {
            await Clean(api, email);
        }
    }

    [NeonFact]
    public async Task Without_the_right_key_the_endpoint_does_not_exist()
    {
        using var api = new AccountsApi();
        var email = api.NewEmail();
        var body = Json($$"""{"event":"hard_bounce","email":"{{email}}"}""");
        try
        {
            using var wrong = WithKey(api).CreateClient();
            Assert.Equal(HttpStatusCode.NotFound, (await wrong.PostAsync("/email/events?key=not-the-key", body)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await wrong.PostAsync("/email/events", body)).StatusCode);

            using var noKeySet = WithKey(api, null).CreateClient();
            Assert.Equal(HttpStatusCode.NotFound, (await noKeySet.PostAsync("/email/events?key=", body)).StatusCode);
            using var shortKey = WithKey(api, "short").CreateClient();
            Assert.Equal(HttpStatusCode.NotFound, (await shortKey.PostAsync("/email/events?key=short", body)).StatusCode);

            Assert.Empty(await Rows(api, email));
        }
        finally
        {
            await Clean(api, email);
        }
    }

    [NeonFact]
    public async Task The_sender_skips_bulk_mail_to_a_suppressed_address_and_still_sends_the_rest()
    {
        using var api = new AccountsApi();
        var blocked = api.NewEmail();
        // A mail server that refuses the connection: sending to anyone not on the list fails, so a skip can't be mistaken for a send.
        using var configured = api.WithWebHostBuilder(b =>
        {
            b.UseSetting("SMTP_HOST", "127.0.0.1");
            b.UseSetting("SMTP_PORT", "1");
            b.UseSetting("SMTP_USER", "sender@example.com");
            b.UseSetting("SMTP_PASSWORD", "password");
        });
        try
        {
            using (var scope = api.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.EmailSuppressions.Add(new EmailSuppression { Email = blocked.ToUpperInvariant(), Reason = SuppressionReason.Bounced }); // Any case: it is citext.
                await db.SaveChangesAsync();
            }

            using var send = configured.Services.CreateScope();
            var sender = send.ServiceProvider.GetRequiredService<IEmailSender>();
            const string Unsubscribe = "https://app.example/api/email/unsubscribe/tok3n/alerts";
            // Bulk mail (it has an unsubscribe address) is skipped for a suppressed address, and says so.
            await Assert.ThrowsAsync<EmailSuppressedException>(() => sender.SendAsync(new OutgoingEmail(blocked, "Subject", "Body", UnsubscribeUrl: Unsubscribe), default));
            // An email someone just asked for still goes (it reaches the refusing server), and so does bulk mail to anyone else.
            var refused = await Assert.ThrowsAsync<EmailException>(() => sender.SendAsync(new OutgoingEmail(blocked, "Subject", "Body"), default));
            Assert.IsNotType<EmailSuppressedException>(refused);
            var other = await Assert.ThrowsAsync<EmailException>(() => sender.SendAsync(new OutgoingEmail(api.NewEmail(), "Subject", "Body", UnsubscribeUrl: Unsubscribe), default));
            Assert.IsNotType<EmailSuppressedException>(other);
        }
        finally
        {
            await Clean(api, blocked);
        }
    }

    [NeonFact]
    public async Task The_admin_users_list_says_which_accounts_are_blocked()
    {
        using var api = new AccountsApi();
        var (_, email) = await api.SignedUpAsync();
        using var owner = await api.OwnerAsync();
        try
        {
            using var brevo = WithKey(api).CreateClient();
            await brevo.PostAsync($"/email/events?key={Key}", Json($$"""{"event":"hard_bounce","email":"{{email}}"}"""));

            var users = await owner.GetFromJsonAsync<System.Text.Json.JsonElement>("/admin/users");
            var mine = users.GetProperty("users").EnumerateArray().Single(u => u.GetProperty("email").GetString() == email);
            Assert.Equal("bounced", mine.GetProperty("emailBlocked").GetString());
        }
        finally
        {
            await Clean(api, email);
        }
    }
}
