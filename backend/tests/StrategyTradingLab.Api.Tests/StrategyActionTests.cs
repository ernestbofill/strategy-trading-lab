using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Tests;

/// <summary>The strategy page's actions, end to end: rename, the alerts switch, End, and archiving an ended strategy.</summary>
public class StrategyActionTests(AccountsApi factory) : IClassFixture<AccountsApi>
{
    static object Request(string name, string mode = "live") => new
    {
        templateKey = "dip", name, mode,
        budget = new { amount = 3000, step = 100, maxSteps = 5 },
        assetIds = TemplateCatalog.Dip.Watches.Assets.Select(asset => asset.Id).Take(2),
    };

    async Task<Guid> StartAsync(HttpClient client, string name, string mode = "live")
    {
        var response = await client.PostAsJsonAsync("/strategies", Request(name, mode));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    async Task RemoveAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Users.Where(u => (string)u.Email == email).ExecuteDeleteAsync();
    }

    static string Tag() => Guid.NewGuid().ToString("N")[..10];

    [NeonFact]
    public async Task A_strategy_is_renamed_with_the_name_rules_of_Confirm()
    {
        var (client, email) = await factory.VerifiedAsync(); // Live strategies need a confirmed email (#89).
        var (other, otherEmail) = await factory.VerifiedAsync(); // Live strategies need a confirmed email (#89).
        try
        {
            var (first, second) = ($"First {Tag()}", $"Second {Tag()}");
            await StartAsync(client, first);
            var id = await StartAsync(client, second);

            Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/strategies/{id}/name", new { name = first.ToUpperInvariant() })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/strategies/{id}/name", new { name = "   " })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/strategies/{id}/name", new { name = new string('x', 61) })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/strategies/{id}/name", new { name = "Mine" })).StatusCode);

            var renamed = $"Renamed {Tag()}";
            Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/strategies/{id}/name", new { name = $"  {renamed}  " })).StatusCode);
            var detail = await client.GetFromJsonAsync<JsonElement>($"/strategies/{id}");
            Assert.Equal(renamed, detail.GetProperty("name").GetString());
            // Its own name in other capitals is no clash.
            Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/strategies/{id}/name", new { name = renamed.ToUpperInvariant() })).StatusCode);
        }
        finally
        {
            await RemoveAsync(email);
            await RemoveAsync(otherEmail);
        }
    }

    [NeonFact]
    public async Task The_alerts_switch_turns_a_running_strategys_notifications_off_and_on()
    {
        var (client, email) = await factory.VerifiedAsync(); // Live strategies need a confirmed email (#89).
        try
        {
            var id = await StartAsync(client, $"Alerts {Tag()}");
            Assert.True((await client.GetFromJsonAsync<JsonElement>($"/strategies/{id}")).GetProperty("notifications").GetBoolean());

            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/strategies/{id}/notifications", new { enabled = (bool?)null })).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/strategies/{id}/notifications", new { enabled = false })).StatusCode);
            Assert.False((await client.GetFromJsonAsync<JsonElement>($"/strategies/{id}")).GetProperty("notifications").GetBoolean());
            Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/strategies/{id}/notifications", new { enabled = true })).StatusCode);
            Assert.True((await client.GetFromJsonAsync<JsonElement>($"/strategies/{id}")).GetProperty("notifications").GetBoolean());

            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/strategies/{id}/end", null)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/strategies/{id}/notifications", new { enabled = false })).StatusCode);
        }
        finally
        {
            await RemoveAsync(email);
        }
    }

    [NeonFact]
    public async Task Ending_a_strategy_misses_its_waiting_signals_frees_its_name_and_refuses_trades()
    {
        var (client, email) = await factory.VerifiedAsync(); // Live strategies need a confirmed email (#89).
        try
        {
            var name = $"End {Tag()}";
            var id = await StartAsync(client, name);
            var assetId = TemplateCatalog.Dip.Watches.Assets.First().Id;
            Guid signalId, tradeId;
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var signal = new Signal
                {
                    StrategyId = id, AssetId = assetId, Side = TradeSide.Buy, StepNo = 1, MaxSteps = 5, StepAmount = 100,
                    SignalDate = new DateOnly(2026, 9, 22), ClosePrice = 100, Facts = JsonDocument.Parse("""{"belowPeakPct":8.2}"""),
                    Status = SignalStatus.Waiting, SnoozedUntil = new DateOnly(2026, 9, 23),
                };
                var manual = new UserTrade
                {
                    StrategyId = id, AssetId = assetId, TradeDate = new DateOnly(2026, 9, 22), Side = TradeSide.Buy, Price = 100, EnteredAs = EnteredAs.Amount,
                    Amount = 99, Shares = 0.99m, Fee = 1, FxRate = 1, FxSource = FxSource.Reference, Source = TradeSource.Manual,
                };
                db.Signals.Add(signal);
                db.UserTrades.Add(manual);
                await db.SaveChangesAsync();
                (signalId, tradeId) = (signal.Id, manual.Id);
            }

            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/strategies/{id}/end", null)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/strategies/{id}/end", null)).StatusCode);

            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var strategy = await db.Strategies.SingleAsync(s => s.Id == id);
                Assert.Equal(StrategyStatus.Ended, strategy.Status);
                Assert.NotNull(strategy.EndedAt);
                var signal = await db.Signals.SingleAsync(s => s.Id == signalId);
                Assert.Equal(SignalStatus.Missed, signal.Status);
                Assert.NotNull(signal.EndedOn);
                Assert.Null(signal.SnoozedUntil);
            }

            var detail = await client.GetFromJsonAsync<JsonElement>($"/strategies/{id}");
            Assert.Equal("ended", detail.GetProperty("status").GetString());
            Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", detail.GetProperty("endedOn").GetString());
            Assert.False(detail.GetProperty("archived").GetBoolean());

            // Its history is final.
            var trade = new { assetId, side = "buy", date = new DateOnly(2026, 9, 22), price = 100, enteredAs = "amount", amount = 99, fee = 1, fxSource = "reference" };
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/strategies/{id}/trades", trade)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/trades/{tradeId}", trade)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/trades/{tradeId}")).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/signals/{signalId}/trade", trade)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/signals/{signalId}/skip", new { reason = "noCash" })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/strategies/{id}/settings", new { settings = new Dictionary<string, decimal>() })).StatusCode);

            // Its name is free again, and it can still be renamed, also to a running strategy's name.
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/strategies", Request(name))).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/strategies/{id}/name", new { name })).StatusCode);
        }
        finally
        {
            await RemoveAsync(email);
        }
    }

    [NeonFact]
    public async Task Ending_an_ai_strategy_skips_its_round_due()
    {
        var (client, email) = await factory.VerifiedAsync(); // Live strategies need a confirmed email (#89).
        try
        {
            Guid id;
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var user = await db.Users.SingleAsync(u => (string)u.Email == email);
                var strategy = new Strategy
                {
                    UserId = user.Id, TemplateKey = TemplateCatalog.Ai.Key, Name = $"AI {Tag()}", Mode = StrategyMode.Simulation, Status = StrategyStatus.Running,
                    StartedOn = new DateOnly(2026, 10, 1), Currency = "EUR", Budget = new Budget(3000, 100, 5), Params = TemplateCatalog.Ai.ParamsJson(),
                };
                db.Strategies.Add(strategy);
                db.AiRounds.Add(new AiRound { StrategyId = strategy.Id, DueOn = strategy.StartedOn, Status = AiRoundStatus.Due });
                await db.SaveChangesAsync();
                id = strategy.Id;
            }

            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/strategies/{id}/end", null)).StatusCode);

            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                Assert.Equal([AiRoundStatus.Skipped], await db.AiRounds.Where(r => r.StrategyId == id).Select(r => r.Status).ToListAsync());
            }
            // Opening the page or the dashboard adds no new round to an ended strategy.
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/strategies/{id}")).StatusCode);
            var dashboard = await client.GetFromJsonAsync<JsonElement>("/dashboard");
            Assert.DoesNotContain(dashboard.GetProperty("waiting").EnumerateArray(), w => w.GetProperty("strategyId").GetGuid() == id);
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                Assert.False(await db.AiRounds.AnyAsync(r => r.StrategyId == id && r.Status == AiRoundStatus.Due));
            }
        }
        finally
        {
            await RemoveAsync(email);
        }
    }

    [NeonFact]
    public async Task Ended_strategies_follow_the_running_ones_on_the_dashboard_and_can_be_archived()
    {
        var (client, email) = await factory.VerifiedAsync(); // Live strategies need a confirmed email (#89).
        try
        {
            var archived = await StartAsync(client, $"Archived {Tag()}");
            var ended = await StartAsync(client, $"Ended {Tag()}", "simulation");
            var running = await StartAsync(client, $"Running {Tag()}");

            // Only an ended strategy can be archived.
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/strategies/{archived}/archive", null)).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/strategies/{archived}/end", null)).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/strategies/{ended}/end", null)).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/strategies/{archived}/archive", null)).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/strategies/{archived}/archive", null)).StatusCode);

            var strategies = (await client.GetFromJsonAsync<JsonElement>("/dashboard")).GetProperty("strategies").EnumerateArray().ToList();
            Assert.Equal([running, ended, archived], strategies.Select(s => s.GetProperty("id").GetGuid()));
            Assert.Equal(["running", "ended", "ended"], strategies.Select(s => s.GetProperty("status").GetString()));
            Assert.Equal([false, false, true], strategies.Select(s => s.GetProperty("archived").GetBoolean()));
            Assert.Equal(JsonValueKind.Null, strategies[0].GetProperty("endedOn").ValueKind);
            Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", strategies[1].GetProperty("endedOn").GetString());
            Assert.True((await client.GetFromJsonAsync<JsonElement>($"/strategies/{archived}")).GetProperty("archived").GetBoolean());

            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/strategies/{archived}/unarchive", null)).StatusCode);
            Assert.False((await client.GetFromJsonAsync<JsonElement>($"/strategies/{archived}")).GetProperty("archived").GetBoolean());
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/strategies/{running}/unarchive", null)).StatusCode);
        }
        finally
        {
            await RemoveAsync(email);
        }
    }
}
