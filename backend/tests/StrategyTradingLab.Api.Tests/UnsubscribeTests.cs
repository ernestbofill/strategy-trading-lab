using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Email;

namespace StrategyTradingLab.Api.Tests;

public class UnsubscribeTests
{
    static async Task<JsonElement> Me(HttpClient client) => await client.GetFromJsonAsync<JsonElement>("/account/me");

    static async Task<string> TokenOf(AccountsApi api, string email)
    {
        using var scope = api.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.Where(u => (string)u.Email == email)
            .Select(u => u.UnsubscribeToken).SingleAsync();
    }

    [NeonFact]
    public async Task The_link_turns_off_one_kind_for_one_user_without_logging_in()
    {
        using var api = new AccountsApi();
        var (me, myEmail) = await api.SignedUpAsync();
        var (other, _) = await api.SignedUpAsync();
        using var stranger = api.CreateApiClient();
        var token = await TokenOf(api, myEmail);

        Assert.Equal(HttpStatusCode.NoContent, (await stranger.PostAsync($"/email/unsubscribe/{token}/overview", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await stranger.PostAsync($"/email/unsubscribe/{token}/overview", null)).StatusCode); // Twice is fine.

        var mine = await Me(me);
        Assert.False(mine.GetProperty("overviewEmail").GetBoolean());
        Assert.True(mine.GetProperty("alertEmails").GetBoolean());
        Assert.True((await Me(other)).GetProperty("overviewEmail").GetBoolean());

        Assert.Equal(HttpStatusCode.NoContent, (await stranger.PostAsync($"/email/unsubscribe/{token}/alerts", null)).StatusCode);
        Assert.False((await Me(me)).GetProperty("alertEmails").GetBoolean());
    }

    [NeonFact]
    public async Task A_mail_apps_one_click_post_needs_no_header_or_login()
    {
        using var api = new AccountsApi();
        var (me, email) = await api.SignedUpAsync();
        using var mailApp = api.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"/email/unsubscribe/{await TokenOf(api, email)}/overview")
        {
            Content = new FormUrlEncodedContent([new("List-Unsubscribe", "One-Click")]),
        };

        Assert.Equal(HttpStatusCode.NoContent, (await mailApp.SendAsync(request)).StatusCode);
        Assert.False((await Me(me)).GetProperty("overviewEmail").GetBoolean());
        Assert.Equal(HttpStatusCode.BadRequest, (await mailApp.PostAsync("/account/alert-emails", null)).StatusCode); // Everything else still needs it.
    }

    [NeonFact]
    public async Task A_guess_a_wrong_kind_or_a_page_load_changes_nothing()
    {
        using var api = new AccountsApi();
        var (me, email) = await api.SignedUpAsync();
        using var stranger = api.CreateApiClient();
        var token = await TokenOf(api, email);

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsync("/email/unsubscribe/not-a-token/overview", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsync($"/email/unsubscribe/{token}/everything", null)).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await stranger.GetAsync($"/email/unsubscribe/{token}/overview")).StatusCode); // A mail scanner's visit.

        var account = await Me(me);
        Assert.True(account.GetProperty("overviewEmail").GetBoolean());
        Assert.True(account.GetProperty("alertEmails").GetBoolean());
    }

    [NeonFact]
    public async Task The_account_can_turn_alerts_back_on()
    {
        using var api = new AccountsApi();
        var (me, email) = await api.SignedUpAsync();
        using var stranger = api.CreateApiClient();
        await stranger.PostAsync($"/email/unsubscribe/{await TokenOf(api, email)}/alerts", null);

        Assert.Equal(HttpStatusCode.NoContent, (await me.PostAsJsonAsync("/account/alert-emails", new { enabled = true })).StatusCode);
        Assert.True((await Me(me)).GetProperty("alertEmails").GetBoolean());
        Assert.Equal(HttpStatusCode.BadRequest, (await me.PostAsJsonAsync("/account/alert-emails", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.PostAsJsonAsync("/account/alert-emails", new { enabled = false })).StatusCode);
    }
}
