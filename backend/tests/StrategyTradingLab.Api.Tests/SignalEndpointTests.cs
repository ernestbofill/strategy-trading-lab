using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Signals;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Tests;

/// <summary>Confirm a signal, end to end: a user's live strategy with waiting signals, answered through the API.</summary>
public class SignalEndpointTests(AccountsApi factory) : IClassFixture<AccountsApi>
{
    static readonly DateOnly SignalDay = new(2026, 10, 22);

    /// <summary>A signed-up user's strategy on a euro stock (no exchange rate needed), with two waiting signals and the next day's price.</summary>
    async Task<(HttpClient Client, Guid StrategyId, Guid AssetId, Guid First, Guid Second)> SeedAsync(StrategyMode mode = StrategyMode.Live)
    {
        var (client, email) = await factory.SignedUpAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        user.EmailVerifiedAt = DateTimeOffset.UtcNow; // A snooze needs an address its reminder can go to.
        var tag = Guid.NewGuid().ToString("N")[..10];
        var asset = new Asset { Symbol = $"S{tag}", Exchange = "TEST", Name = "Test asset", Currency = "EUR" };
        db.Assets.Add(asset);
        await db.SaveChangesAsync();
        db.Prices.AddRange(
            new PriceDaily { AssetId = asset.Id, Date = SignalDay, Open = 185, High = 188, Low = 184, Close = 187.5m, Volume = 1000 },
            new PriceDaily { AssetId = asset.Id, Date = SignalDay.AddDays(1), Open = 190, High = 191, Low = 189, Close = 190.5m, Volume = 1000 });
        var strategy = new Strategy
        {
            UserId = user.Id, TemplateKey = "dip", Name = $"Signals {tag}", Mode = mode, Status = StrategyStatus.Running, StartedOn = new DateOnly(2026, 10, 1),
            Currency = "EUR", Budget = new Budget(3000, 100, 5), Params = TemplateCatalog.Dip.ParamsJson(),
            Assets = [new StrategyAsset { AssetId = asset.Id, Active = true, AddedAt = DateTimeOffset.UtcNow }],
        };
        db.Strategies.Add(strategy);
        await db.SaveChangesAsync();
        Signal Waiting(DateOnly day, int step) => new()
        {
            StrategyId = strategy.Id, AssetId = asset.Id, Side = TradeSide.Buy, StepNo = step, MaxSteps = 5, StepAmount = 100, SignalDate = day,
            ClosePrice = 187.5m, Facts = JsonDocument.Parse("""{"belowPeakPct":8.2}"""),
            Status = mode == StrategyMode.Live ? SignalStatus.Waiting : SignalStatus.Confirmed, AnsweredAt = mode == StrategyMode.Live ? null : DateTimeOffset.UtcNow,
        };
        var (first, second) = (Waiting(SignalDay, 1), Waiting(SignalDay.AddDays(-10), 2));
        db.Signals.AddRange(first, second);
        await db.SaveChangesAsync();
        return (client, strategy.Id, asset.Id, first.Id, second.Id);
    }

    async Task CleanAsync(Guid assetId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Strategies.Where(s => s.Assets.Any(a => a.AssetId == assetId)).ExecuteDeleteAsync();
        await db.Assets.Where(a => a.Id == assetId).ExecuteDeleteAsync();
    }

    static object Trade(decimal price = 190, decimal amount = 98.80m) =>
        new { date = SignalDay.AddDays(1), price, enteredAs = "amount", amount, fee = 1.20m, fxSource = "reference" };

    [NeonFact]
    public async Task The_signal_comes_with_the_next_days_open_and_the_step_without_its_fee()
    {
        var (client, strategyId, assetId, first, _) = await SeedAsync();
        try
        {
            var signal = await client.GetFromJsonAsync<JsonElement>($"/signals/{first}");

            Assert.Equal(strategyId, signal.GetProperty("strategyId").GetGuid());
            Assert.Equal("waiting", signal.GetProperty("status").GetString());
            Assert.Equal("8.2% below its recent peak, still above its 50-day average", signal.GetProperty("reason").GetString());
            var prefill = signal.GetProperty("prefill");
            Assert.Equal((SignalDay.AddDays(1), 190m, 98.80m),
                (DateOnly.Parse(prefill.GetProperty("date").GetString()!), prefill.GetProperty("price").GetDecimal(), prefill.GetProperty("amount").GetDecimal()));
            Assert.Equal(0, signal.GetProperty("rates").GetArrayLength()); // Euro to euro: no rate to show.
        }
        finally
        {
            await CleanAsync(assetId);
        }
    }

    [NeonFact]
    public async Task I_did_it_records_the_trade_once_and_the_strategy_counts_it()
    {
        var (client, strategyId, assetId, first, _) = await SeedAsync();
        try
        {
            var refused = await client.PostAsJsonAsync($"/signals/{first}/trade", Trade(price: 0));
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
            Assert.True((await refused.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("price", out _));

            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/signals/{first}/trade", Trade())).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/signals/{first}/trade", Trade())).StatusCode);

            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var trade = await db.UserTrades.SingleAsync(t => t.SignalId == first);
                Assert.Equal((98.80m, Math.Round(98.80m / 190, 6), 1.20m, 1m, FxSource.Reference, TradeSource.Signal),
                    (trade.Amount, trade.Shares, trade.Fee, trade.FxRate, trade.FxSource, trade.Source));
                var signal = await db.Signals.SingleAsync(s => s.Id == first);
                Assert.Equal(SignalStatus.Confirmed, signal.Status);
                Assert.NotNull(signal.AnsweredAt);
            }
            var page = await client.GetFromJsonAsync<JsonElement>($"/strategies/{strategyId}");
            Assert.Equal(100m, page.GetProperty("budget").GetProperty("used").GetDecimal()); // 98.80 and its 1.20 fee.
            Assert.Equal((1, 0, 0), (page.GetProperty("signals").GetProperty("did").GetInt32(), page.GetProperty("signals").GetProperty("skipped").GetInt32(),
                page.GetProperty("signals").GetProperty("missed").GetInt32()));
            var answer = (await client.GetFromJsonAsync<JsonElement>($"/signals/{first}")).GetProperty("answer");
            Assert.Equal(("confirmed", 190m), (answer.GetProperty("status").GetString(), answer.GetProperty("price").GetDecimal()));
        }
        finally
        {
            await CleanAsync(assetId);
        }
    }

    [NeonFact]
    public async Task I_skipped_it_keeps_the_reason_and_a_snooze_keeps_the_signal_waiting_until_the_next_weekday()
    {
        var (client, _, assetId, first, second) = await SeedAsync();
        try
        {
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/signals/{second}/skip", new { reason = "noCash" })).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/signals/{second}/trade", Trade())).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/signals/{second}/snooze", null)).StatusCode);

            using (var setup = factory.Services.CreateScope())
            {
                var setupDb = setup.ServiceProvider.GetRequiredService<AppDbContext>();
                await setupDb.Strategies.Where(s => s.Assets.Any(a => a.AssetId == assetId)).ExecuteUpdateAsync(s => s.SetProperty(x => x.Notifications, false));
                Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/signals/{first}/snooze", null)).StatusCode); // No reminder could be sent.
                await setupDb.Strategies.Where(s => s.Assets.Any(a => a.AssetId == assetId)).ExecuteUpdateAsync(s => s.SetProperty(x => x.Notifications, true));
            }

            var snooze = await client.PostAsync($"/signals/{first}/snooze", null);
            Assert.Equal(HttpStatusCode.OK, snooze.StatusCode);
            var until = DateOnly.Parse((await snooze.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("snoozedUntil").GetString()!);
            Assert.True(until > DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)) && until.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday));

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var skipped = await db.Signals.SingleAsync(s => s.Id == second);
            Assert.Equal((SignalStatus.Skipped, SkipReason.NoCash), (skipped.Status, skipped.SkipReason));
            var waiting = await db.Signals.SingleAsync(s => s.Id == first);
            Assert.Equal((SignalStatus.Waiting, until), (waiting.Status, waiting.SnoozedUntil));
            Assert.Empty(await db.UserTrades.Where(t => t.SignalId == second).ToListAsync());
        }
        finally
        {
            await CleanAsync(assetId);
        }
    }

    [NeonFact]
    public async Task Another_users_signal_and_a_simulations_signal_are_not_found()
    {
        var live = await SeedAsync();
        var simulation = await SeedAsync(StrategyMode.Simulation);
        try
        {
            var (other, _) = await factory.SignedUpAsync();
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/signals/{live.First}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/signals/{live.First}/trade", Trade())).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await simulation.Client.GetAsync($"/signals/{simulation.First}")).StatusCode);
        }
        finally
        {
            await CleanAsync(live.AssetId);
            await CleanAsync(simulation.AssetId);
        }
    }
}
