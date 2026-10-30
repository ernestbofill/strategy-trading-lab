using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using StrategyTradingLab.Api.Admin;
using StrategyTradingLab.Api.Auth;
using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Tests;

/// <summary>Invite codes, the owner's invites and the invite-only gate on sign-up (spec: Sign-up, Admin · Invites).</summary>
public class InviteTests(AccountsApi api) : IClassFixture<AccountsApi>
{
    static async Task<JsonElement> Json(HttpResponseMessage response) => await response.Content.ReadFromJsonAsync<JsonElement>();

    static async Task<JsonElement> NewInvite(HttpClient owner, object? body = null)
    {
        var response = await owner.PostAsJsonAsync("/admin/invites", body ?? new { firstName = "Anna", lastName = "Puig", locale = "ca" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await Json(response);
    }

    /// <summary>A host on the same database whose sign-ups are invite only (the server's value; the page's would win).</summary>
    Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> InviteOnly() =>
        api.WithWebHostBuilder(b => b.UseSetting(SettingDefinitions.SignUps, SettingDefinitions.SignUpsInviteOnly));

    [Fact]
    public void Codes_are_two_groups_of_four_and_typed_codes_are_read_forgivingly()
    {
        var code = InviteCodes.New();
        Assert.Matches("^[A-Z2-9]{4}-[A-Z2-9]{4}$", code);
        Assert.DoesNotContain(code, c => c is '0' or 'O' or '1' or 'I' or 'L');
        Assert.Equal(code, InviteCodes.Normalize($"  {code.ToLowerInvariant().Replace("-", " ")} "));
        Assert.Null(InviteCodes.Normalize("ABCD-EFG0")); // 0 is never in a code.
        Assert.Null(InviteCodes.Normalize("ABCD"));
        Assert.Null(InviteCodes.Normalize(null));
    }

    [Fact]
    public void Names_are_trimmed_and_too_long_or_empty_ones_refused()
    {
        Assert.Equal("Anna Maria", AccountRules.NormalizeName("  Anna   Maria "));
        Assert.Null(AccountRules.NormalizeName("   "));
        Assert.Null(AccountRules.NormalizeName(new string('a', AccountRules.NameMax + 1)));
    }

    [NeonFact]
    public async Task Only_the_owner_makes_invites()
    {
        using var user = await api.SignedUpAsync(UserRole.Vip);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.PostAsJsonAsync("/admin/invites", new { firstName = "A", locale = "en" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/admin/invites")).StatusCode);
        using var stranger = api.CreateApiClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.GetAsync("/admin/invites")).StatusCode);
    }

    [NeonFact]
    public async Task An_invite_has_a_link_a_code_and_a_message_in_its_language_and_can_be_emailed()
    {
        using var owner = await api.OwnerAsync();
        var email = api.NewEmail();
        var invite = await NewInvite(owner, new { firstName = "Anna", lastName = "Puig", email, locale = "ca", send = true });

        var code = invite.GetProperty("code").GetString()!;
        Assert.EndsWith($"/sign-up?invite={code}", invite.GetProperty("link").GetString());
        var message = invite.GetProperty("message").GetString()!;
        Assert.StartsWith("Hola, Anna!", message);
        Assert.Contains(code, message);
        Assert.Equal("vip", invite.GetProperty("role").GetString());
        Assert.NotEqual(JsonValueKind.Null, invite.GetProperty("sentAt").ValueKind);

        var sent = await api.Emails.WaitForAsync(email, "Test t'ha convidat a Strategy Trading Lab");
        Assert.NotNull(sent);
        Assert.Contains(code, sent!.Body);
        Assert.NotNull(sent.ReplyTo); // Answers go to the owner.

        var list = await owner.GetFromJsonAsync<JsonElement>("/admin/invites");
        Assert.Contains(list.EnumerateArray(), i => i.GetProperty("code").GetString() == code);
    }

    [NeonFact]
    public async Task A_new_invite_needs_a_first_name_a_language_and_an_email_to_send_it()
    {
        using var owner = await api.OwnerAsync();
        var response = await owner.PostAsJsonAsync("/admin/invites", new { firstName = " ", locale = "fr", send = true });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await Json(response)).GetProperty("errors");
        foreach (var field in new[] { "firstName", "locale", "email" }) Assert.True(errors.TryGetProperty(field, out _), field);
    }

    [NeonFact]
    public async Task Sign_up_with_an_invite_gives_the_vip_role_once_and_records_who_joined()
    {
        using var owner = await api.OwnerAsync();
        var invite = await NewInvite(owner);
        var code = invite.GetProperty("code").GetString()!;

        using var visitor = api.CreateApiClient();
        var options = await visitor.GetFromJsonAsync<JsonElement>($"/account/sign-up-options?invite={code.ToLowerInvariant()}");
        Assert.Equal("Anna", options.GetProperty("invite").GetProperty("firstName").GetString());
        Assert.Equal("ca", options.GetProperty("invite").GetProperty("locale").GetString());

        var email = api.NewEmail();
        Assert.Equal(HttpStatusCode.Accepted, (await visitor.PostAsJsonAsync("/account/sign-up", ApiClients.SignUpBody(email, inviteCode: code))).StatusCode);
        var me = await visitor.GetFromJsonAsync<JsonElement>("/account/me");
        Assert.Equal("vip", me.GetProperty("role").GetString());
        Assert.Equal("Test", me.GetProperty("firstName").GetString());

        // Used up: the link no longer greets anyone, and a second sign-up with it is refused.
        var after = await api.CreateApiClient().GetFromJsonAsync<JsonElement>($"/account/sign-up-options?invite={code}");
        Assert.Equal(JsonValueKind.Null, after.GetProperty("invite").ValueKind);
        var again = await api.CreateApiClient().PostAsJsonAsync("/account/sign-up", ApiClients.SignUpBody(api.NewEmail(), inviteCode: code));
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.True((await Json(again)).GetProperty("errors").TryGetProperty("inviteCode", out _));

        var listed = (await owner.GetFromJsonAsync<JsonElement>("/admin/invites")).EnumerateArray().Single(i => i.GetProperty("code").GetString() == code);
        Assert.Equal(1, listed.GetProperty("uses").GetInt32());
        Assert.Equal(email, listed.GetProperty("joined")[0].GetProperty("email").GetString());
    }

    [NeonFact]
    public async Task A_taken_email_answers_the_same_and_leaves_the_invite_unused()
    {
        using var owner = await api.OwnerAsync();
        var code = (await NewInvite(owner)).GetProperty("code").GetString()!;
        var (_, taken) = await api.SignedUpAsync();

        var response = await api.CreateApiClient().PostAsJsonAsync("/account/sign-up", ApiClients.SignUpBody(taken, "another long password", code));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var options = await api.CreateApiClient().GetFromJsonAsync<JsonElement>($"/account/sign-up-options?invite={code}");
        Assert.NotEqual(JsonValueKind.Null, options.GetProperty("invite").ValueKind);
    }

    [NeonFact]
    public async Task A_revoked_invite_stops_working()
    {
        using var owner = await api.OwnerAsync();
        var invite = await NewInvite(owner);
        var id = invite.GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsync($"/admin/invites/{id}/revoke", null)).StatusCode);

        var response = await api.CreateApiClient().PostAsJsonAsync("/account/sign-up",
            ApiClients.SignUpBody(api.NewEmail(), inviteCode: invite.GetProperty("code").GetString()));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await (await api.OwnerAsync()).PostAsync($"/admin/invites/{id}/revoke", null)).StatusCode); // Another owner's.
    }

    [NeonFact]
    public async Task Invite_only_refuses_a_sign_up_without_an_invite_and_takes_one_with_it()
    {
        using var owner = await api.OwnerAsync(); // Signed up before the gate.
        using var gated = InviteOnly();
        var code = (await NewInvite(owner)).GetProperty("code").GetString()!;
        using var visitor = gated.CreateApiClient();

        Assert.True((await visitor.GetFromJsonAsync<JsonElement>("/account/sign-up-options")).GetProperty("inviteOnly").GetBoolean());
        var refused = await visitor.PostAsJsonAsync("/account/sign-up", ApiClients.SignUpBody(api.NewEmail()));
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal("Sign-ups are by invitation for now. Enter your invite code.",
            (await Json(refused)).GetProperty("errors").GetProperty("inviteCode")[0].GetString());

        Assert.Equal(HttpStatusCode.Accepted,
            (await visitor.PostAsJsonAsync("/account/sign-up", ApiClients.SignUpBody(api.NewEmail(), inviteCode: code))).StatusCode);
    }
}
