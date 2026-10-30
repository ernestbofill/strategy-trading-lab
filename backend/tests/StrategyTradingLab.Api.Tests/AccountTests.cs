using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StrategyTradingLab.Api.Data;

namespace StrategyTradingLab.Api.Tests;

/// <summary>Sign-up, log-in, sessions and the email links, end to end against the linked Neon branch.</summary>
public class AccountTests(AccountsApi api) : IClassFixture<AccountsApi>
{
    static async Task<string?> Detail(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString();

    static async Task<JsonElement> Me(HttpClient client) => await client.GetFromJsonAsync<JsonElement>("/account/me");

    [NeonFact]
    public async Task Sign_up_logs_in_at_once_and_the_emailed_link_verifies_the_email_once()
    {
        var (client, email) = await api.SignedUpAsync();

        var me = await Me(client);
        Assert.Equal(email, me.GetProperty("email").GetString());
        Assert.False(me.GetProperty("emailVerified").GetBoolean());

        var link = await api.Emails.WaitForAsync(email, "Confirm your email");
        Assert.NotNull(link);
        var token = CapturedEmails.Token(link!);
        using var stranger = api.CreateApiClient();
        Assert.Equal(HttpStatusCode.NoContent, (await stranger.PostAsJsonAsync("/account/verify-email", new { token })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.GetAsync("/account/me")).StatusCode); // The link logs no one in.
        Assert.True((await Me(client)).GetProperty("emailVerified").GetBoolean());

        var again = await client.PostAsJsonAsync("/account/verify-email", new { token });
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Equal("This link has expired. Ask for a new one.", await Detail(again));
    }

    [NeonFact]
    public async Task Sign_up_with_a_taken_email_answers_the_same_but_logs_no_one_in_and_warns_the_owner()
    {
        var (_, email) = await api.SignedUpAsync();
        using var other = api.CreateApiClient();

        var response = await other.PostAsJsonAsync("/account/sign-up", ApiClients.SignUpBody(email.ToUpperInvariant(), "another long password"));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("Check your email.", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("message").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await other.GetAsync("/account/me")).StatusCode);
        Assert.NotNull(await api.Emails.WaitForAsync(email, "Someone tried to sign up"));
    }

    [NeonFact]
    public async Task Sign_up_checks_every_field()
    {
        using var client = api.CreateApiClient();
        var email = api.NewEmail();

        var response = await client.PostAsJsonAsync("/account/sign-up",
            new { email = "not an email", password = "short", locale = "fr", timeZone = "Mars/Base", overviewEmail = true, disclaimerAccepted = false, currency = "XXX" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        foreach (var field in new[] { "firstName", "lastName", "email", "password", "locale", "timeZone", "disclaimerAccepted", "currency" })
            Assert.True(errors.TryGetProperty(field, out _), field);
        var sameAsEmail = await client.PostAsJsonAsync("/account/sign-up", ApiClients.SignUpBody(email, email));
        Assert.Equal(HttpStatusCode.BadRequest, sameAsEmail.StatusCode);
    }

    [NeonFact]
    public async Task The_currency_is_euro_unless_picked_at_sign_up_and_can_be_changed_in_the_account()
    {
        var (client, _) = await api.SignedUpAsync();
        Assert.Equal("EUR", (await Me(client)).GetProperty("currency").GetString());

        using var pounds = api.CreateApiClient();
        var email = api.NewEmail();
        var body = new { firstName = "Test", lastName = "User", email, password = "a long password", locale = "en", timeZone = "Europe/Madrid", overviewEmail = true, disclaimerAccepted = true, currency = "GBP" };
        Assert.Equal(HttpStatusCode.Accepted, (await pounds.PostAsJsonAsync("/account/sign-up", body)).StatusCode);
        Assert.Equal("GBP", (await Me(pounds)).GetProperty("currency").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/account/currency", new { currency = "USD" })).StatusCode);
        Assert.Equal("USD", (await Me(client)).GetProperty("currency").GetString());
        var refused = await client.PostAsJsonAsync("/account/currency", new { currency = "JPY" });
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal("USD", (await Me(client)).GetProperty("currency").GetString());
    }

    [NeonFact]
    public async Task The_locale_is_english_unless_picked_at_sign_up_and_can_be_changed_in_the_account()
    {
        var (client, _) = await api.SignedUpAsync();
        Assert.Equal("en", (await Me(client)).GetProperty("locale").GetString());

        using var catalan = api.CreateApiClient();
        var email = api.NewEmail();
        var body = new { firstName = "Test", lastName = "User", email, password = "a long password", locale = "ca", timeZone = "Europe/Madrid", overviewEmail = true, disclaimerAccepted = true, currency = "EUR" };
        Assert.Equal(HttpStatusCode.Accepted, (await catalan.PostAsJsonAsync("/account/sign-up", body)).StatusCode);
        Assert.Equal("ca", (await Me(catalan)).GetProperty("locale").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/account/locale", new { locale = "ca" })).StatusCode);
        Assert.Equal("ca", (await Me(client)).GetProperty("locale").GetString());
        var refused = await client.PostAsJsonAsync("/account/locale", new { locale = "fr" });
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal("ca", (await Me(client)).GetProperty("locale").GetString());
    }

    [NeonFact]
    public async Task The_daily_overview_can_be_switched_off_and_on_in_the_account()
    {
        var (client, _) = await api.SignedUpAsync();
        Assert.True((await Me(client)).GetProperty("overviewEmail").GetBoolean());

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/account/overview-email", new { enabled = false })).StatusCode);
        Assert.False((await Me(client)).GetProperty("overviewEmail").GetBoolean());
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/account/overview-email", new { enabled = true })).StatusCode);
        Assert.True((await Me(client)).GetProperty("overviewEmail").GetBoolean());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/account/overview-email", new { })).StatusCode); // A missing field is refused, not read as off.
        Assert.True((await Me(client)).GetProperty("overviewEmail").GetBoolean());
        using var visitor = api.CreateApiClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.PostAsJsonAsync("/account/overview-email", new { enabled = false })).StatusCode);
    }

    [NeonFact]
    public async Task A_wrong_password_and_an_unknown_email_get_the_same_answer()
    {
        var (_, email) = await api.SignedUpAsync();
        using var client = api.CreateApiClient();

        var wrongPassword = await client.PostAsJsonAsync("/account/log-in", new { email, password = "not the password" });
        var unknownEmail = await client.PostAsJsonAsync("/account/log-in", new { email = api.NewEmail(), password = "a long password" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        var wrongPasswordDetail = await Detail(wrongPassword);
        Assert.Equal("That email and password don't match.", wrongPasswordDetail);
        Assert.Equal(wrongPasswordDetail, await Detail(unknownEmail));
    }

    [NeonFact]
    public async Task Log_in_and_log_out()
    {
        var (_, email) = await api.SignedUpAsync();
        using var client = api.CreateApiClient();

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/account/log-in", new { email = $"  {email.ToUpperInvariant()} ", password = "a long password" })).StatusCode);
        Assert.Equal(email, (await Me(client)).GetProperty("email").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/account/log-out", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/account/me")).StatusCode);
    }

    [NeonFact]
    public async Task A_logged_out_cookie_no_longer_works_even_when_replayed()
    {
        using var raw = api.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = false });
        raw.DefaultRequestHeaders.Add("X-Requested-With", "stl");
        var signUp = await raw.PostAsJsonAsync("/account/sign-up", ApiClients.SignUpBody(api.NewEmail()));
        var cookie = signUp.Headers.GetValues("Set-Cookie").First(c => c.StartsWith("stl_session=")).Split(';')[0];
        Assert.Contains("httponly", signUp.Headers.GetValues("Set-Cookie").First(c => c.StartsWith("stl_session=")).ToLowerInvariant());

        async Task<HttpStatusCode> Send(HttpMethod method, string path)
        {
            using var request = new HttpRequestMessage(method, path);
            request.Headers.Add("Cookie", cookie);
            return (await raw.SendAsync(request)).StatusCode;
        }

        Assert.Equal(HttpStatusCode.OK, await Send(HttpMethod.Get, "/account/me"));
        Assert.Equal(HttpStatusCode.NoContent, await Send(HttpMethod.Post, "/account/log-out"));
        Assert.Equal(HttpStatusCode.Unauthorized, await Send(HttpMethod.Get, "/account/me"));
    }

    [NeonFact]
    public async Task Forgot_password_answers_the_same_and_the_link_resets_it_ending_every_session()
    {
        var (oldDevice, email) = await api.SignedUpAsync();
        using var client = api.CreateApiClient();

        var unknown = await client.PostAsJsonAsync("/account/forgot-password", new { email = api.NewEmail() });
        var known = await client.PostAsJsonAsync("/account/forgot-password", new { email });
        Assert.Equal(HttpStatusCode.Accepted, unknown.StatusCode);
        Assert.Equal(await unknown.Content.ReadAsStringAsync(), await known.Content.ReadAsStringAsync());

        var token = CapturedEmails.Token((await api.Emails.WaitForAsync(email, "Choose a new password"))!);
        var tooShort = await client.PostAsJsonAsync("/account/reset-password", new { token, password = "short" });
        Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/account/reset-password", new { token, password = "a brand new password" })).StatusCode);
        Assert.Equal(email, (await Me(client)).GetProperty("email").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await oldDevice.GetAsync("/account/me")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/account/reset-password", new { token, password = "yet another password" })).StatusCode);

        using var fresh = api.CreateApiClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await fresh.PostAsJsonAsync("/account/log-in", new { email, password = "a long password" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await fresh.PostAsJsonAsync("/account/log-in", new { email, password = "a brand new password" })).StatusCode);
    }

    [NeonFact]
    public async Task Change_password_needs_the_current_one_and_ends_the_other_sessions()
    {
        var (client, email) = await api.SignedUpAsync();
        using var otherDevice = api.CreateApiClient();
        await otherDevice.PostAsJsonAsync("/account/log-in", new { email, password = "a long password" });

        var wrong = await client.PostAsJsonAsync("/account/change-password", new { currentPassword = "nope", newPassword = "a brand new password" });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/account/change-password", new { currentPassword = "a long password", newPassword = "a brand new password" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/account/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await otherDevice.GetAsync("/account/me")).StatusCode);
    }

    [NeonFact]
    public async Task Changing_the_password_uses_up_reset_links_sent_before()
    {
        var (client, email) = await api.SignedUpAsync();
        await client.PostAsJsonAsync("/account/forgot-password", new { email = email.ToUpperInvariant() }); // Emails compare without case.
        var token = CapturedEmails.Token((await api.Emails.WaitForAsync(email, "Choose a new password"))!);

        await client.PostAsJsonAsync("/account/change-password", new { currentPassword = "a long password", newPassword = "a brand new password" });

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/account/reset-password", new { token, password = "yet another password" })).StatusCode);
    }

    [NeonFact]
    public async Task Two_reset_links_used_at_once_set_one_password_and_refuse_the_other()
    {
        var (client, email) = await api.SignedUpAsync();
        var tokens = new List<string>();
        for (var i = 0; i < 2; i++)
        {
            await client.PostAsJsonAsync("/account/forgot-password", new { email });
            var message = await api.Emails.WaitForAsync(email, "Choose a new password", skip: i);
            tokens.Add(CapturedEmails.Token(message!));
        }
        Assert.NotEqual(tokens[0], tokens[1]);

        using var a = api.CreateApiClient();
        using var b = api.CreateApiClient();
        var answers = await Task.WhenAll(
            a.PostAsJsonAsync("/account/reset-password", new { token = tokens[0], password = "first new password" }),
            b.PostAsJsonAsync("/account/reset-password", new { token = tokens[1], password = "second new password" }));

        Assert.Equal([HttpStatusCode.NoContent, HttpStatusCode.BadRequest], answers.Select(r => r.StatusCode).Order());
    }

    [NeonFact]
    public async Task Protected_routes_need_a_session_and_templates_dont()
    {
        using var visitor = api.CreateApiClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.GetAsync("/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.PostAsJsonAsync("/strategies", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.GetAsync("/account/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.PostAsync("/account/resend-verification", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.PostAsJsonAsync("/account/change-password", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.PostAsJsonAsync("/account/currency", new { currency = "USD" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.PostAsJsonAsync("/account/locale", new { locale = "ca" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await visitor.GetAsync("/templates")).StatusCode);
    }

    [NeonFact]
    public async Task A_state_changing_request_without_the_header_is_refused()
    {
        using var bare = api.CreateClient();

        var response = await bare.PostAsJsonAsync("/account/log-in", new { email = "a@b.c", password = "x" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [NeonFact]
    public async Task Logging_in_again_on_a_device_ends_its_previous_session()
    {
        var (client, email) = await api.SignedUpAsync();
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userId = await db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();

        await client.PostAsJsonAsync("/account/log-in", new { email, password = "a long password" });
        await client.PostAsJsonAsync("/account/log-in", new { email, password = "a long password" });

        Assert.Equal(1, await db.Sessions.CountAsync(s => s.UserId == userId));
    }

    [NeonFact]
    public async Task A_password_longer_than_any_account_can_have_is_refused_like_a_wrong_one()
    {
        var (_, email) = await api.SignedUpAsync();
        using var client = api.CreateApiClient();

        var response = await client.PostAsJsonAsync("/account/log-in", new { email, password = new string('a', 10_000) });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [NeonFact]
    public async Task The_seeded_user_is_gone()
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.False(await db.Users.AnyAsync(u => u.Id == new Guid("019a0000-0000-7000-8000-000000000001")));
        Assert.False(await db.Users.AnyAsync(u => u.Email == "ernest@strategytradinglab.local"));
    }
}

/// <summary>The per-email lock, in its own API so its log-in rate limit counts only these tries.</summary>
public class LogInLockTests(AccountsApi api) : IClassFixture<AccountsApi>
{
    [NeonFact]
    public async Task Five_wrong_passwords_lock_an_email_with_or_without_an_account()
    {
        var (_, email) = await api.SignedUpAsync();
        var unknown = api.NewEmail();
        using var client = api.CreateApiClient();

        foreach (var address in new[] { email, unknown })
        {
            for (var i = 0; i < 5; i++)
                Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/account/log-in", new { email = address, password = "wrong password" })).StatusCode);
            Assert.Equal((HttpStatusCode)429, (await client.PostAsJsonAsync("/account/log-in", new { email = address, password = "a long password" })).StatusCode);
        }
    }
}
