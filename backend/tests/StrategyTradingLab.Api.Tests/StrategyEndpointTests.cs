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
public class StrategyEndpointTests(AccountsApi factory) : IClassFixture<AccountsApi>
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
        var (client, email) = await factory.SignedUpAsync();
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
            Assert.Equal(-21, created.GetProperty("pastResult").GetProperty("worstYear").GetDecimal());
            Assert.Equal($"/strategies/{created.GetProperty("id").GetGuid()}", response.Headers.Location?.OriginalString);

            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var stored = await db.Strategies.Include(s => s.Assets).SingleAsync(s => s.Name == name);
                Assert.Equal(email, (await db.Users.SingleAsync(u => u.Id == stored.UserId)).Email);
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
    public async Task A_strategy_can_be_deleted_by_its_owner_only_and_frees_its_name()
    {
        var name = $"Test {Guid.NewGuid()}";
        var (client, _) = await factory.SignedUpAsync();
        var (other, _) = await factory.SignedUpAsync();
        try
        {
            var created = await (await client.PostAsJsonAsync("/strategies", Request("dip", name))).Content.ReadFromJsonAsync<JsonElement>();
            var id = created.GetProperty("id").GetGuid();

            Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/strategies/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/strategies/{id}")).StatusCode);

            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/strategies/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/strategies/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/strategies/{id}")).StatusCode);
            var dashboard = await client.GetFromJsonAsync<JsonElement>("/dashboard");
            Assert.DoesNotContain(dashboard.GetProperty("strategies").EnumerateArray(), s => s.GetProperty("id").GetGuid() == id);

            Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/strategies", Request("dip", name))).StatusCode);
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
        var (client, email) = await factory.SignedUpAsync();
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
        var (client, email) = await factory.SignedUpAsync();
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
        var (client, email) = await factory.SignedUpAsync();

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
    public async Task A_strategy_stores_the_settings_the_user_changed_over_the_tested_defaults()
    {
        var name = $"Test {Guid.NewGuid()}";
        var (client, _) = await factory.SignedUpAsync();
        try
        {
            var response = await client.PostAsJsonAsync("/strategies", new
            {
                templateKey = "dip", name, mode = "simulation",
                budget = new { amount = 3000, step = 100, maxSteps = 5 },
                assetIds = TemplateCatalog.Dip.Watches.Assets.Select(asset => asset.Id).Take(2),
                settings = new Dictionary<string, decimal> { ["peakDays"] = 40 },
            });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            using var scope = factory.Services.CreateScope();
            var stored = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Strategies.SingleAsync(s => s.Name == name);
            Assert.Equal(40, stored.Params.RootElement.GetProperty("peakDays").GetInt32());
            Assert.Equal(5, stored.Params.RootElement.GetProperty("zoneStartPct").GetDecimal());
        }
        finally
        {
            await Remove(name);
        }
    }

    [NeonFact]
    public async Task A_setting_out_of_range_is_a_validation_problem()
    {
        var (client, _) = await factory.SignedUpAsync();

        var response = await client.PostAsJsonAsync("/strategies", new
        {
            templateKey = "dip", name = $"Test {Guid.NewGuid()}", mode = "simulation",
            budget = new { amount = 3000, step = 100, maxSteps = 5 },
            assetIds = TemplateCatalog.Dip.Watches.Assets.Select(asset => asset.Id).Take(1),
            settings = new Dictionary<string, decimal> { ["peakDays"] = 200 },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("settings.peakDays", out _));
    }

    [NeonFact]
    public async Task Strategy_settings_can_be_updated_and_retrieved()
    {
        var name = $"Test {Guid.NewGuid()}";
        var (client, _) = await factory.SignedUpAsync();
        try
        {
            var startResponse = await client.PostAsJsonAsync("/strategies", Request("dip", name));
            Assert.Equal(HttpStatusCode.Created, startResponse.StatusCode);
            var created = await startResponse.Content.ReadFromJsonAsync<JsonElement>();
            var strategyId = created.GetProperty("id").GetGuid();

            var detail = await client.GetFromJsonAsync<JsonElement>($"/strategies/{strategyId}");
            Assert.Equal(60, detail.GetProperty("settings").GetProperty("peakDays").GetDecimal());

            var updateResponse = await client.PutAsJsonAsync($"/strategies/{strategyId}/settings", new
            {
                settings = new Dictionary<string, decimal> { ["peakDays"] = 40, ["zoneStartPct"] = 10, ["zoneEndPct"] = 15 }
            });
            Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

            var updatedDetail = await client.GetFromJsonAsync<JsonElement>($"/strategies/{strategyId}");
            Assert.Equal(40, updatedDetail.GetProperty("settings").GetProperty("peakDays").GetDecimal());
            Assert.Equal(10, updatedDetail.GetProperty("settings").GetProperty("zoneStartPct").GetDecimal());
            Assert.Equal(15, updatedDetail.GetProperty("settings").GetProperty("zoneEndPct").GetDecimal());
        }
        finally
        {
            await Remove(name);
        }
    }

    [NeonFact]
    public async Task Updating_settings_with_invalid_value_returns_validation_problem()
    {
        var name = $"Test {Guid.NewGuid()}";
        var (client, _) = await factory.SignedUpAsync();
        try
        {
            var startResponse = await client.PostAsJsonAsync("/strategies", Request("dip", name));
            var created = await startResponse.Content.ReadFromJsonAsync<JsonElement>();
            var strategyId = created.GetProperty("id").GetGuid();

            var response = await client.PutAsJsonAsync($"/strategies/{strategyId}/settings", new
            {
                settings = new Dictionary<string, decimal> { ["peakDays"] = 999 }
            });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            await Remove(name);
        }
    }

    [NeonFact]
    public async Task An_unknown_template_is_not_found()
    {
        var (client, email) = await factory.SignedUpAsync();

        var response = await client.PostAsJsonAsync("/strategies", new
        {
            templateKey = "nope", name = "x", mode = "live",
            budget = new { amount = 3000, step = 100, maxSteps = 5 }, assetIds = Array.Empty<Guid>(),
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
