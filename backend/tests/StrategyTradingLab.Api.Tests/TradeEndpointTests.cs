using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Signals;
using StrategyTradingLab.Api.Strategies;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Tests;

/// <summary>
/// Your trades: see, fix and log them (Roadmap item 10).
/// Tests for logging manual trades, editing trades, deleting trades, and viewing signals/trades on the strategy page.
/// </summary>
public class TradeEndpointTests(AccountsApi factory) : IClassFixture<AccountsApi>
{
    static readonly DateOnly StrategyStartedOn = new(2026, 9, 1);
    static readonly DateOnly SignalDay = new(2026, 9, 22);

    async Task<(HttpClient Client, Guid StrategyId, Guid AssetId, Guid SignalId, Guid AnotherAssetId)> SeedAsync(StrategyMode mode = StrategyMode.Live)
    {
        var (client, email) = await factory.SignedUpAsync();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        user.EmailVerifiedAt = DateTimeOffset.UtcNow;

        var tag = Guid.NewGuid().ToString("N")[..10];
        var asset = new Asset { Symbol = $"T{tag}", Exchange = "TEST", Name = "Test Stock", Currency = "EUR" };
        var outsideAsset = new Asset { Symbol = $"O{tag}", Exchange = "TEST", Name = "Outside Stock", Currency = "EUR" };
        db.Assets.AddRange(asset, outsideAsset);
        await db.SaveChangesAsync();

        db.Prices.AddRange(
            new PriceDaily { AssetId = asset.Id, Date = StrategyStartedOn, Open = 100, High = 105, Low = 99, Close = 102, Volume = 1000 },
            new PriceDaily { AssetId = asset.Id, Date = SignalDay, Open = 185, High = 188, Low = 184, Close = 187.5m, Volume = 1000 },
            new PriceDaily { AssetId = asset.Id, Date = SignalDay.AddDays(1), Open = 190, High = 191, Low = 189, Close = 190.5m, Volume = 1000 });

        var strategy = new Strategy
        {
            UserId = user.Id, TemplateKey = "dip", Name = $"Trades {tag}", Mode = mode, Status = StrategyStatus.Running,
            StartedOn = StrategyStartedOn, Currency = "EUR", Budget = new Budget(3000, 100, 5), Params = TemplateCatalog.Dip.ParamsJson(),
            Assets = [new StrategyAsset { AssetId = asset.Id, Active = true, AddedAt = DateTimeOffset.UtcNow }],
        };
        db.Strategies.Add(strategy);
        await db.SaveChangesAsync();

        var signal = new Signal
        {
            StrategyId = strategy.Id, AssetId = asset.Id, Side = TradeSide.Buy, StepNo = 1, MaxSteps = 5, StepAmount = 100,
            SignalDate = SignalDay, ClosePrice = 187.5m, Facts = JsonDocument.Parse("""{"belowPeakPct":8.2}"""),
            Status = mode == StrategyMode.Live ? SignalStatus.Waiting : SignalStatus.Confirmed,
            AnsweredAt = mode == StrategyMode.Live ? null : DateTimeOffset.UtcNow,
        };
        db.Signals.Add(signal);
        await db.SaveChangesAsync();

        return (client, strategy.Id, asset.Id, signal.Id, outsideAsset.Id);
    }

    async Task CleanAsync(Guid assetId, Guid outsideAssetId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Strategies.Where(s => s.Assets.Any(a => a.AssetId == assetId)).ExecuteDeleteAsync();
        await db.Assets.Where(a => a.Id == assetId || a.Id == outsideAssetId).ExecuteDeleteAsync();
    }

    [NeonFact]
    public async Task Log_manual_trade_validates_asset_date_and_mode()
    {
        var (client, strategyId, assetId, _, outsideAssetId) = await SeedAsync();
        var (_, simStrategyId, _, _, _) = await SeedAsync(StrategyMode.Simulation);
        try
        {
            // Simulation strategy refused
            var simRes = await client.PostAsJsonAsync($"/strategies/{simStrategyId}/trades", new
            {
                assetId,
                side = "buy",
                date = SignalDay,
                price = 180m,
                enteredAs = "amount",
                amount = 98.80m,
                fee = 1.20m,
            });
            Assert.Equal(HttpStatusCode.Conflict, simRes.StatusCode);

            // Asset not in strategy refused
            var wrongAssetRes = await client.PostAsJsonAsync($"/strategies/{strategyId}/trades", new
            {
                assetId = outsideAssetId,
                side = "buy",
                date = SignalDay,
                price = 180m,
                enteredAs = "amount",
                amount = 98.80m,
                fee = 1.20m,
            });
            Assert.Equal(HttpStatusCode.BadRequest, wrongAssetRes.StatusCode);
            var problem = await wrongAssetRes.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(problem.GetProperty("errors").TryGetProperty("assetId", out _));

            // Date before strategy started refused
            var earlyRes = await client.PostAsJsonAsync($"/strategies/{strategyId}/trades", new
            {
                assetId,
                side = "buy",
                date = StrategyStartedOn.AddDays(-1),
                price = 180m,
                enteredAs = "amount",
                amount = 98.80m,
                fee = 1.20m,
            });
            Assert.Equal(HttpStatusCode.BadRequest, earlyRes.StatusCode);

            // Valid manual trade succeeds
            var goodRes = await client.PostAsJsonAsync($"/strategies/{strategyId}/trades", new
            {
                assetId,
                side = "buy",
                date = SignalDay,
                price = 185m,
                enteredAs = "amount",
                amount = 98.80m,
                fee = 1.20m,
            });
            Assert.Equal(HttpStatusCode.Created, goodRes.StatusCode);
            var item = await goodRes.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("manual", item.GetProperty("source").GetString());
            Assert.Equal("buy", item.GetProperty("side").GetString());
            Assert.Equal(185m, item.GetProperty("price").GetDecimal());

            // Check database has manual trade with signal_id == null
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var trade = await db.UserTrades.SingleAsync(t => t.StrategyId == strategyId && t.Source == TradeSource.Manual);
            Assert.Null(trade.SignalId);
            Assert.Equal(TradeSource.Manual, trade.Source);
            Assert.Equal(185m, trade.Price);
            Assert.Null(trade.DeletedAt);
        }
        finally
        {
            await CleanAsync(assetId, outsideAssetId);
        }
    }

    [NeonFact]
    public async Task Each_live_trade_can_be_edited_and_deleted()
    {
        var (client, strategyId, assetId, signalId, outsideAssetId) = await SeedAsync();
        try
        {
            // First confirm a signal trade
            var tradeRes = await client.PostAsJsonAsync($"/signals/{signalId}/trade", new
            {
                date = SignalDay.AddDays(1),
                price = 190m,
                enteredAs = "amount",
                amount = 98.80m,
                fee = 1.20m,
                fxSource = "reference",
            });
            Assert.Equal(HttpStatusCode.NoContent, tradeRes.StatusCode);

            // Check trade appears on StrategyDetail
            var detail = await client.GetFromJsonAsync<JsonElement>($"/strategies/{strategyId}");
            var trades = detail.GetProperty("trades");
            Assert.Equal(1, trades.GetArrayLength());
            var tradeId = trades[0].GetProperty("id").GetGuid();
            Assert.Equal("signal", trades[0].GetProperty("source").GetString());

            // Edit the trade: date, price, amount, fee
            var editRes = await client.PutAsJsonAsync($"/trades/{tradeId}", new
            {
                date = SignalDay.AddDays(2),
                price = 192m,
                enteredAs = "shares",
                shares = 0.52m,
                fee = 1.50m,
                fxSource = "reference",
            });
            Assert.Equal(HttpStatusCode.NoContent, editRes.StatusCode);

            // Verify edit in DB and on strategy detail
            detail = await client.GetFromJsonAsync<JsonElement>($"/strategies/{strategyId}");
            var updatedTrade = detail.GetProperty("trades")[0];
            Assert.Equal(192m, updatedTrade.GetProperty("price").GetDecimal());
            Assert.Equal(0.52m, updatedTrade.GetProperty("shares").GetDecimal());
            Assert.Equal(1.50m, updatedTrade.GetProperty("fee").GetDecimal());

            // Delete the trade
            var deleteRes = await client.DeleteAsync($"/trades/{tradeId}");
            Assert.Equal(HttpStatusCode.NoContent, deleteRes.StatusCode);

            // StrategyDetail now has 0 trades
            detail = await client.GetFromJsonAsync<JsonElement>($"/strategies/{strategyId}");
            Assert.Equal(0, detail.GetProperty("trades").GetArrayLength());

            // But signalHistory still has the confirmed signal! (Rules never change)
            var history = detail.GetProperty("signalHistory");
            Assert.True(history.GetArrayLength() >= 1);
            Assert.Equal("confirmed", history[0].GetProperty("status").GetString());

            // In DB, trade is soft-deleted
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var softDeleted = await db.UserTrades.SingleAsync(t => t.Id == tradeId);
            Assert.NotNull(softDeleted.DeletedAt);
        }
        finally
        {
            await CleanAsync(assetId, outsideAssetId);
        }
    }
}
