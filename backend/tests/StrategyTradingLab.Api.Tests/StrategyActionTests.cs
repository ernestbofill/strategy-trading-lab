using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Tests;

/// <summary>The strategy page's actions, end to end: rename, Edit list, the alerts switch, Archive, and deleting an archived strategy.</summary>
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
    public async Task A_device_subscribes_once_moves_to_whoever_logs_in_and_unsubscribes()
    {
        var (first, firstEmail) = await factory.VerifiedAsync();
        var (second, secondEmail) = await factory.VerifiedAsync();
        try
        {
            var endpoint = $"https://fcm.googleapis.com/fcm/send/{Tag()}";
            var keys = new { p256dh = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode([4, .. new byte[64]]), auth = "AAAAAAAAAAAAAAAAAAAAAA" };
            Assert.Equal(HttpStatusCode.BadRequest,
                (await first.PutAsJsonAsync("/push/subscription", new { endpoint = "https://attacker.example/x", keys })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest,
                (await first.PutAsJsonAsync("/push/subscription", new { endpoint, keys = new { p256dh = "short", auth = "x" } })).StatusCode);

            Assert.Equal(HttpStatusCode.NoContent, (await first.PutAsJsonAsync("/push/subscription", new { endpoint, keys })).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await first.PutAsJsonAsync("/push/subscription", new { endpoint, keys })).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await second.PutAsJsonAsync("/push/subscription", new { endpoint, keys })).StatusCode);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var owner = await db.PushSubscriptions.Where(p => p.Endpoint == endpoint).Select(p => db.Users.Single(u => u.Id == p.UserId).Email).SingleAsync();
            Assert.Equal(secondEmail, owner);

            // Only the device's own user can forget it.
            Assert.Equal(HttpStatusCode.NoContent, (await first.PostAsJsonAsync("/push/unsubscribe", new { endpoint })).StatusCode);
            Assert.True(await db.PushSubscriptions.AnyAsync(p => p.Endpoint == endpoint));
            Assert.Equal(HttpStatusCode.NoContent, (await second.PostAsJsonAsync("/push/unsubscribe", new { endpoint })).StatusCode);
            Assert.False(await db.PushSubscriptions.AnyAsync(p => p.Endpoint == endpoint));
        }
        finally
        {
            await RemoveAsync(firstEmail);
            await RemoveAsync(secondEmail);
        }
    }

    [NeonFact]
    public async Task Archiving_a_strategy_misses_its_waiting_signals_frees_its_name_and_refuses_trades()
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

            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/strategies/{id}/archive", null)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/strategies/{id}/archive", null)).StatusCode);

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
            Assert.False(detail.TryGetProperty("archived", out _));

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
    public async Task Archiving_an_ai_strategy_skips_its_round_due()
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

            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/strategies/{id}/archive", null)).StatusCode);

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
    public async Task Archived_strategies_follow_the_running_ones_last_archived_first_and_only_they_can_be_deleted()
    {
        var (client, email) = await factory.VerifiedAsync(); // Live strategies need a confirmed email (#89).
        try
        {
            var first = await StartAsync(client, $"First {Tag()}");
            var second = await StartAsync(client, $"Second {Tag()}", "simulation");
            var running = await StartAsync(client, $"Running {Tag()}");

            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/strategies/{first}/archive", null)).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/strategies/{second}/archive", null)).StatusCode);

            var strategies = (await client.GetFromJsonAsync<JsonElement>("/dashboard")).GetProperty("strategies").EnumerateArray().ToList();
            Assert.Equal([running, second, first], strategies.Select(s => s.GetProperty("id").GetGuid()));
            Assert.Equal(["running", "ended", "ended"], strategies.Select(s => s.GetProperty("status").GetString()));
            Assert.Equal(JsonValueKind.Null, strategies[0].GetProperty("endedOn").ValueKind);
            Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", strategies[1].GetProperty("endedOn").GetString());

            // A running strategy is archived before it can go; an archived one is deleted for good.
            Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/strategies/{running}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/strategies/{running}")).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/strategies/{first}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/strategies/{first}")).StatusCode);
        }
        finally
        {
            await RemoveAsync(email);
        }
    }

    [NeonFact]
    public async Task Edit_list_stops_an_unticked_stocks_signals_keeps_its_trades_and_brings_it_back_from_that_day()
    {
        var (client, email) = await factory.VerifiedAsync();
        var (other, otherEmail) = await factory.VerifiedAsync();
        try
        {
            var id = await StartAsync(client, $"List {Tag()}");
            var dip = TemplateCatalog.Dip.Watches.Assets.Select(asset => asset.Id).ToList();
            var (a, b, c) = (dip[0], dip[1], dip[2]); // It started with A and B.
            Guid signalId;
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var signal = new Signal
                {
                    StrategyId = id, AssetId = a, Side = TradeSide.Buy, StepNo = 1, MaxSteps = 5, StepAmount = 100,
                    SignalDate = new DateOnly(2026, 9, 22), ClosePrice = 100, Facts = JsonDocument.Parse("{}"),
                    Status = SignalStatus.Waiting, SnoozedUntil = new DateOnly(2026, 9, 23),
                };
                db.Signals.Add(signal);
                db.UserTrades.Add(new UserTrade
                {
                    StrategyId = id, AssetId = a, TradeDate = new DateOnly(2026, 9, 22), Side = TradeSide.Buy, Price = 100, EnteredAs = EnteredAs.Amount,
                    Amount = 99, Shares = 0.99m, Fee = 1, FxRate = 1, FxSource = FxSource.Reference, Source = TradeSource.Manual,
                });
                await db.SaveChangesAsync();
                signalId = signal.Id;
            }

            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/strategies/{id}/assets", new { assetIds = Array.Empty<Guid>() })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/strategies/{id}/assets", new { assetIds = new[] { b, b } })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/strategies/{id}/assets", new { assetIds = new[] { b, Guid.NewGuid() } })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest,
                (await client.PutAsJsonAsync($"/strategies/{id}/assets", new { assetIds = Enumerable.Range(0, 61).Select(_ => Guid.NewGuid()) })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/strategies/{id}/assets", new { assetIds = new[] { b } })).StatusCode);

            // A unticked, C added: A's waiting signal is missed, and its row stays (removed) for its trade.
            Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/strategies/{id}/assets", new { assetIds = new[] { b, c } })).StatusCode);
            var detail = await client.GetFromJsonAsync<JsonElement>($"/strategies/{id}");
            var rows = detail.GetProperty("stocks").EnumerateArray().ToDictionary(r => r.GetProperty("assetId").GetGuid(), r => r.GetProperty("status").GetString());
            Assert.Equal("removed", rows[a]);
            Assert.NotEqual("removed", rows[b]);
            Assert.NotEqual("removed", rows[c]);
            Assert.Equal(3, rows.Count);
            // The user may still sell what they hold of an unticked stock.
            var sale = await client.PostAsJsonAsync($"/strategies/{id}/trades", new
                { assetId = a, side = "sell", date = new DateOnly(2026, 9, 23), price = 100m, enteredAs = "shares", shares = 0.99m, fee = 1m });
            Assert.Equal(HttpStatusCode.Created, sale.StatusCode);
            DateTimeOffset removedAt;
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var signal = await db.Signals.AsNoTracking().SingleAsync(s => s.Id == signalId);
                Assert.Equal(SignalStatus.Missed, signal.Status);
                Assert.Null(signal.SnoozedUntil);
                Assert.NotNull(signal.EndedOn);
                var listed = await db.StrategyAssets.AsNoTracking().SingleAsync(sa => sa.StrategyId == id && sa.AssetId == a);
                Assert.False(listed.Active);
                removedAt = listed.RemovedAt!.Value;
                Assert.Equal(2, await db.UserTrades.CountAsync(t => t.StrategyId == id && t.AssetId == a && t.DeletedAt == null));
            }

            // Ticked again: followed from now on, and B unticked with no trades leaves the page.
            Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/strategies/{id}/assets", new { assetIds = new[] { a, c } })).StatusCode);
            detail = await client.GetFromJsonAsync<JsonElement>($"/strategies/{id}");
            rows = detail.GetProperty("stocks").EnumerateArray().ToDictionary(r => r.GetProperty("assetId").GetGuid(), r => r.GetProperty("status").GetString());
            Assert.Equal(new HashSet<Guid> { a, c }, rows.Keys.ToHashSet());
            Assert.NotEqual("removed", rows[a]);
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var listed = await db.StrategyAssets.AsNoTracking().SingleAsync(sa => sa.StrategyId == id && sa.AssetId == a);
                Assert.True(listed.Active);
                Assert.Null(listed.RemovedAt);
                Assert.True(listed.AddedAt >= removedAt);
            }

            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/strategies/{id}/archive", null)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/strategies/{id}/assets", new { assetIds = new[] { a } })).StatusCode);
        }
        finally
        {
            await RemoveAsync(email);
            await RemoveAsync(otherEmail);
        }
    }
}
