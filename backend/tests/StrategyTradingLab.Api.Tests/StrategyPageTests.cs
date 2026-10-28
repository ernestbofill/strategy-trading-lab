using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Evaluation;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Tests;

/// <summary>The strategy page and the dashboard's results, end to end: a strategy evaluated over a dip, then read through the API.</summary>
public class StrategyPageTests(AccountsApi factory) : IClassFixture<AccountsApi>
{
    sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    static readonly DateOnly First = new(2024, 1, 1);

    /// <summary>A user's dip strategy on a test stock that dips into the zone on day 300 (as in StrategyEvaluatorTests), evaluated.</summary>
    async Task<(HttpClient Client, Guid StrategyId, Guid AssetId)> EvaluatedAsync(StrategyMode mode)
    {
        var (client, email) = await factory.SignedUpAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        var tag = Guid.NewGuid().ToString("N")[..10];
        var asset = new Asset { Symbol = $"P{tag}", Exchange = "TEST", Name = "Test asset", Currency = "EUR" };
        db.Assets.Add(asset);
        var closes = Enumerable.Range(0, 300).Select(i => Math.Round(100m * (decimal)Math.Pow(1.003, i), 4)).ToList();
        closes.Add(Math.Round(closes.TakeLast(60).Max() * 0.94m, 4));
        closes.Add(closes[^1]);
        closes.Add(Math.Round(closes[^1] * 1.01m, 4)); // Day 302: up 1%, still in the zone.
        await db.SaveChangesAsync();
        db.Prices.AddRange(closes.Select((close, i) => new PriceDaily
            { AssetId = asset.Id, Date = First.AddDays(i), Open = close, High = close, Low = close, Close = close, Volume = 1000 }));
        var start = First.AddDays(299);
        var strategy = new Strategy
        {
            UserId = user.Id, TemplateKey = "dip", Name = $"Page {tag}", Mode = mode, Status = StrategyStatus.Running, StartedOn = start,
            Currency = "EUR", Budget = new Budget(3000, 100, 5), Params = TemplateCatalog.Dip.ParamsJson(),
            Assets = [new StrategyAsset { AssetId = asset.Id, Active = true, AddedAt = new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) }],
        };
        db.Strategies.Add(strategy);
        await db.SaveChangesAsync();
        await StrategyEvaluatorTests.CheckedBeforeAsync(db, strategy.Id, asset.Id, start);
        await new StrategyEvaluator(db, new Clock(new DateTimeOffset(2024, 12, 2, 12, 0, 0, TimeSpan.Zero)), NullLogger<StrategyEvaluator>.Instance)
            .RunAsync(default);
        return (client, strategy.Id, asset.Id);
    }

    async Task CleanAsync(Guid assetId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Strategies.Where(s => s.Assets.Any(a => a.AssetId == assetId)).ExecuteDeleteAsync();
        await db.Assets.Where(a => a.Id == assetId).ExecuteDeleteAsync();
    }

    [NeonFact]
    public async Task A_simulation_shows_its_paper_trade_result_steps_and_the_stock_building()
    {
        var (client, id, assetId) = await EvaluatedAsync(StrategyMode.Simulation);
        try
        {
            var page = await client.GetFromJsonAsync<JsonElement>($"/strategies/{id}");

            Assert.Equal("simulation", page.GetProperty("mode").GetString());
            Assert.Equal("Winners at a discount", page.GetProperty("templateName").GetString());
            var budget = page.GetProperty("budget");
            Assert.Equal(100m, budget.GetProperty("used").GetDecimal()); // The step, fee included.
            Assert.Equal((1, 30), (budget.GetProperty("steps").GetInt32(), budget.GetProperty("totalSteps").GetInt32()));
            Assert.Equal(JsonValueKind.Null, page.GetProperty("signals").ValueKind);
            Assert.Equal("the discount zone", page.GetProperty("words").GetProperty("zone").GetString());

            // Bought at day 301's open, valued at day 302's close (1% up), with €98.80 of shares for a €100 step (the fee is inside it).
            var expected = Math.Round((98.8m * 1.01m - 100m) / 100m * 100, 1);
            var results = page.GetProperty("results");
            Assert.Equal(expected, results.GetProperty("you").GetDecimal());
            Assert.Equal(JsonValueKind.Null, results.GetProperty("rules").ValueKind);
            Assert.Equal(expected, results.GetProperty("history").EnumerateArray().Last().GetProperty("you").GetDecimal());

            var stock = Assert.Single(page.GetProperty("stocks").EnumerateArray());
            Assert.Equal("building", stock.GetProperty("status").GetString());
            Assert.Equal(1, stock.GetProperty("steps").GetInt32());
            Assert.Equal(100m, stock.GetProperty("invested").GetDecimal());
            Assert.Equal(expected, stock.GetProperty("result").GetDecimal());

            var dashboard = await client.GetFromJsonAsync<JsonElement>("/dashboard");
            var card = dashboard.GetProperty("strategies").EnumerateArray().Single(s => s.GetProperty("id").GetGuid() == id);
            Assert.Equal(100m, card.GetProperty("used").GetDecimal());
            Assert.Equal(expected, card.GetProperty("results").GetProperty("strategy").GetDecimal());
        }
        finally
        {
            await CleanAsync(assetId);
        }
    }

    [NeonFact]
    public async Task A_live_signal_waits_for_the_user_on_the_page_and_the_dashboard_and_the_rules_have_their_own_result()
    {
        var (client, id, assetId) = await EvaluatedAsync(StrategyMode.Live);
        try
        {
            var page = await client.GetFromJsonAsync<JsonElement>($"/strategies/{id}");

            Assert.Equal(0m, page.GetProperty("budget").GetProperty("used").GetDecimal()); // Nothing confirmed yet.
            Assert.Equal(JsonValueKind.Null, page.GetProperty("results").ValueKind);
            var signals = page.GetProperty("signals");
            Assert.Equal((0, 0, 0), (signals.GetProperty("did").GetInt32(), signals.GetProperty("skipped").GetInt32(), signals.GetProperty("missed").GetInt32()));
            var stock = Assert.Single(page.GetProperty("stocks").EnumerateArray());
            Assert.Equal("waitingForYou", stock.GetProperty("status").GetString());
            Assert.Equal(1, stock.GetProperty("signalStep").GetInt32());
            Assert.Equal((0, 1), (stock.GetProperty("steps").GetInt32(), stock.GetProperty("rulesSteps").GetInt32()));
            Assert.Equal(JsonValueKind.Null, stock.GetProperty("result").ValueKind);

            var dashboard = await client.GetFromJsonAsync<JsonElement>("/dashboard");
            var waiting = Assert.Single(dashboard.GetProperty("waiting").EnumerateArray());
            Assert.Equal(("buySignal", id, "Test asset"),
                (waiting.GetProperty("kind").GetString(), waiting.GetProperty("strategyId").GetGuid(), waiting.GetProperty("stockName").GetString()));
            Assert.Equal(stock.GetProperty("signalId").GetGuid(), waiting.GetProperty("id").GetGuid());
        }
        finally
        {
            await CleanAsync(assetId);
        }
    }

    [NeonFact]
    public async Task Another_users_strategy_is_not_found()
    {
        var (_, id, assetId) = await EvaluatedAsync(StrategyMode.Simulation);
        try
        {
            var (other, _) = await factory.SignedUpAsync();
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/strategies/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/strategies/{Guid.NewGuid()}")).StatusCode);
        }
        finally
        {
            await CleanAsync(assetId);
        }
    }
}
