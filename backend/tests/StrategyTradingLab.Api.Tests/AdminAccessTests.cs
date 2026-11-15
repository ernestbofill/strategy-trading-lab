using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using StrategyTradingLab.Api.Auth;
using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Tests;

/// <summary>Every /admin endpoint is for the owner only: one check of the routes, and one of what a request gets.</summary>
public class AdminAccessTests(AdminFixture fixture) : IClassFixture<AdminFixture>
{
    [NeonFact]
    public void Every_admin_endpoint_requires_the_owner_policy()
    {
        var admin = fixture.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.TrimStart('/').StartsWith("admin", StringComparison.Ordinal) == true).ToList();

        Assert.NotEmpty(admin);
        Assert.All(admin, e => Assert.Contains(e.Metadata.GetOrderedMetadata<IAuthorizeData>(), a => a.Policy == Policies.Owner));
    }

    [NeonFact]
    public async Task A_stranger_gets_401_and_any_other_role_403()
    {
        using var stranger = fixture.CreateApiClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.GetAsync("/admin/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.PutAsJsonAsync("/admin/settings/SMTP_USER", new { value = "a@b.c" })).StatusCode);
        stranger.DefaultRequestHeaders.Add("Cookie", "stl_session=forged");
        Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.GetAsync("/admin/settings")).StatusCode);

        foreach (var role in new[] { UserRole.User, UserRole.Vip })
        {
            using var other = await fixture.SignedUpAsync(role);
            Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync("/admin/settings")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await other.PutAsJsonAsync("/admin/settings/SMTP_USER", new { value = "a@b.c" })).StatusCode);
        }
    }
}
