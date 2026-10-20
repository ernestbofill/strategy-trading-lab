using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Tests;

/// <summary>Starting a strategy and the dashboard, end to end against the linked Neon branch. Each test removes what it adds.</summary>
public class StrategyEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    static object Request(string templateKey, string name, string mode = "simulation") => new
    {
        templateKey, name, mode,
        budget = templateKey == "trend" ? (object)new { amount = 3000, step = (int?)null, maxSteps = (int?)null }
                                        : new { amount = 3000, step = 100, maxSteps = 5 },
        assetIds = TemplateCatalog.Find(templateKey)!.Watches.Assets.Select(asset => asset.Id).Take(templateKey == "trend" ? 1 : 3),
    };

    async Task Remove(string name)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Strategies.Where(strategy => strategy.Name == name).ExecuteDeleteAsync();
    }

    [NeonFact]
    public async Task A_strategy_starts_with_the_templates_tested_defaults_and_shows_on_the_dashboard()
    {
        var name = $"Test {Guid.NewGuid()}";
        using var client = factory.CreateClient();
        try
        {
            var response = await client.PostAsJsonAsync("/strategies", Request("dip", $"  {name}  ", "live"));

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var created = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(name, created.GetProperty("name").GetString());
            Assert.Equal("live", created.GetProperty("mode").GetString());
            Assert.Equal("Dip in winners", created.GetProperty("templateName").GetString());
            Assert.Equal(3000, created.GetProperty("budget").GetDecimal());
            Assert.Equal(0, created.GetProperty("used").GetDecimal());
            Assert.Equal(JsonValueKind.Null, created.GetProperty("results").ValueKind);
            Assert.Equal(212, created.GetProperty("pastResult").GetProperty("template").GetDecimal());
            Assert.Equal($"/strategies/{created.GetProperty("id").GetGuid()}", response.Headers.Location?.OriginalString);

            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var stored = await db.Strategies.Include(s => s.Assets).SingleAsync(s => s.Name == name);
                Assert.Equal(DevSeed.DevUserId, stored.UserId);
                Assert.Equal("EUR", stored.Currency);
                Assert.Equal(StrategyTradingLab.Api.Data.Entities.StrategyStatus.Running, stored.Status);
                Assert.Equal(3, stored.Assets.Count(asset => asset.Active));
                Assert.Equal(60, stored.Params.RootElement.GetProperty("peakDays").GetInt32());
            }

            var dashboard = await client.GetFromJsonAsync<JsonElement>("/dashboard");
            Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", dashboard.GetProperty("today").GetString());
            Assert.Contains(dashboard.GetProperty("strategies").EnumerateArray(), s => s.GetProperty("name").GetString() == name);
            Assert.Equal(JsonValueKind.Array, dashboard.GetProperty("waiting").ValueKind);
        }
        finally
        {
            await Remove(name);
        }
    }

    [NeonFact]
    public async Task An_all_in_strategy_starts_without_steps()
    {
        var name = $"Test {Guid.NewGuid()}";
        using var client = factory.CreateClient();
        try
        {
            var response = await client.PostAsJsonAsync("/strategies", Request("trend", name));

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }
        finally
        {
            await Remove(name);
        }
    }

    [NeonFact]
    public async Task A_running_name_cannot_be_used_twice_ignoring_case()
    {
        var name = $"Test {Guid.NewGuid()}";
        using var client = factory.CreateClient();
        try
        {
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/strategies", Request("dip", name))).StatusCode);

            var again = await client.PostAsJsonAsync("/strategies", Request("bear", name.ToUpperInvariant()));

            Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        }
        finally
        {
            await Remove(name);
        }
    }

    [NeonFact]
    public async Task An_invalid_strategy_is_a_validation_problem_and_stores_nothing()
    {
        var name = $"Test {Guid.NewGuid()}";
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/strategies", new
        {
            templateKey = "dip", name, mode = "live",
            budget = new { amount = 100, step = 100, maxSteps = 5 }, assetIds = Array.Empty<Guid>(),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("budget.amount", out _));
        Assert.True(problem.GetProperty("errors").TryGetProperty("assetIds", out _));
        using var scope = factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().Strategies.AnyAsync(s => s.Name == name));
    }

    [NeonFact]
    public async Task An_unknown_template_is_not_found()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/strategies", new
        {
            templateKey = "nope", name = "x", mode = "live",
            budget = new { amount = 3000, step = 100, maxSteps = 5 }, assetIds = Array.Empty<Guid>(),
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
