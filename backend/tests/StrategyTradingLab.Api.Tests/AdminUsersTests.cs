using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Tests;

/// <summary>The admin users page's API (/admin/users), for the owner only.</summary>
public class AdminUsersTests(AdminFixture fixture) : IClassFixture<AdminFixture>
{
    [NeonFact]
    public async Task Users_need_the_owner_role()
    {
        using var stranger = fixture.CreateApiClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.GetAsync("/admin/users")).StatusCode);
        foreach (var role in new[] { UserRole.User, UserRole.Vip })
        {
            using var other = await fixture.SignedUpAsync(role);
            Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync("/admin/users")).StatusCode);
        }
    }

    [NeonFact]
    public async Task Lists_each_account_with_its_strategies_and_counts_running_ones_by_template_and_mode()
    {
        var (client, email) = await fixture.VerifiedAsync(UserRole.Vip);
        var before = await fixture.Admin.GetFromJsonAsync<JsonElement>("/admin/users");
        var dip = (Func<JsonElement, JsonElement>)(page => page.GetProperty("counts").GetProperty("running").EnumerateArray()
            .Single(t => t.GetProperty("templateKey").GetString() == TemplateCatalog.Dip.Key));

        var request = new
        {
            templateKey = "dip", name = $"Users {Guid.NewGuid():N}"[..20], mode = "live",
            budget = new { amount = 3000, step = 100, maxSteps = 5 },
            assetIds = TemplateCatalog.Dip.Watches.Assets.Select(asset => asset.Id).Take(2),
        };
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/strategies", request)).StatusCode);

        var page = await fixture.Admin.GetFromJsonAsync<JsonElement>("/admin/users");
        var users = page.GetProperty("users").EnumerateArray().ToList();
        var user = users.Single(u => u.GetProperty("email").GetString() == email);
        Assert.Equal("vip", user.GetProperty("role").GetString());
        Assert.True(user.GetProperty("confirmed").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, user.GetProperty("lastVisit").ValueKind);
        Assert.Equal(1, user.GetProperty("running").GetInt32());
        Assert.Equal(0, user.GetProperty("archived").GetInt32());

        // Newest first; the counts take the new account and its live strategy in, and list every template.
        var joined = users.Select(u => u.GetProperty("joined").GetDateTimeOffset()).ToList();
        Assert.Equal(joined.OrderByDescending(d => d), joined);
        Assert.Equal(users.Count, page.GetProperty("counts").GetProperty("users").GetInt32());
        Assert.True(page.GetProperty("counts").GetProperty("users").GetInt32() > before.GetProperty("counts").GetProperty("users").GetInt32());
        // At least: other test classes may start strategies at the same time.
        Assert.True(dip(page).GetProperty("live").GetInt32() >= dip(before).GetProperty("live").GetInt32() + 1);
        Assert.Equal(TemplateCatalog.All.Count, page.GetProperty("counts").GetProperty("running").GetArrayLength());
        Assert.Equal("Winners at a discount", dip(page).GetProperty("templateName").GetString());
    }
}
