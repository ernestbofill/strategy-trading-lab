using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Tests;

/// <summary>The admin assets endpoints (/admin/assets), for the owner only.</summary>
public class AdminAssetsTests(AdminFixture fixture) : IClassFixture<AdminFixture>
{
    [NeonFact]
    public async Task Assets_need_the_owner_role()
    {
        using var stranger = fixture.CreateApiClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.GetAsync("/admin/assets")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await stranger.PutAsJsonAsync($"/admin/assets/{TemplateAssets.CBrain.Id}/active", new { active = false })).StatusCode);

        foreach (var role in new[] { UserRole.User, UserRole.Vip })
        {
            using var other = await fixture.SignedUpAsync(role);
            Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync("/admin/assets")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await other.PutAsJsonAsync($"/admin/assets/{TemplateAssets.CBrain.Id}/active", new { active = false })).StatusCode);
        }
    }

    [NeonFact]
    public async Task Lists_all_seeded_assets_and_toggles_whether_one_is_active()
    {
        var cBrainId = TemplateAssets.CBrain.Id;
        try
        {
            var list = (await fixture.Admin.GetFromJsonAsync<JsonElement>("/admin/assets")).EnumerateArray().ToList();
            Assert.True(list.Count >= TemplateAssets.All.Length);

            var cBrain = list.Single(a => a.GetProperty("id").GetGuid() == cBrainId);
            Assert.Equal("CBRAIN.CO", cBrain.GetProperty("symbol").GetString());
            Assert.Equal("XCSE", cBrain.GetProperty("exchange").GetString());
            Assert.Equal("cBrain", cBrain.GetProperty("name").GetString());
            Assert.Equal("Software for governments", cBrain.GetProperty("what").GetString());
            Assert.Equal("stock", cBrain.GetProperty("kind").GetString());
            Assert.Equal("DKK", cBrain.GetProperty("currency").GetString());
            Assert.True(cBrain.GetProperty("active").GetBoolean());

            var hide = await fixture.Admin.PutAsJsonAsync($"/admin/assets/{cBrainId}/active", new { active = false });
            Assert.Equal(HttpStatusCode.NoContent, hide.StatusCode);

            var afterHide = (await fixture.Admin.GetFromJsonAsync<JsonElement>("/admin/assets")).EnumerateArray()
                .Single(a => a.GetProperty("id").GetGuid() == cBrainId);
            Assert.False(afterHide.GetProperty("active").GetBoolean());

            var restore = await fixture.Admin.PutAsJsonAsync($"/admin/assets/{cBrainId}/active", new { active = true });
            Assert.Equal(HttpStatusCode.NoContent, restore.StatusCode);

            var unknown = await fixture.Admin.PutAsJsonAsync($"/admin/assets/{Guid.NewGuid()}/active", new { active = false });
            Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        }
        finally
        {
            await fixture.Admin.PutAsJsonAsync($"/admin/assets/{cBrainId}/active", new { active = true });
        }
    }
}
