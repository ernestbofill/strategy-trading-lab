using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.MarketData;

namespace StrategyTradingLab.Api.Tests;

public class AssetCatalogTests
{
    sealed class FakeSource : IMarketDataSource
    {
        public Task<IReadOnlyList<Candle>> DailyAsync(string symbol, bool fullHistory, bool adjusted, CancellationToken cancellationToken)
        {
            var days = Enumerable.Range(fullHistory ? 0 : 20, fullHistory ? 30 : 10)
                .Select(day => new Candle(new DateOnly(2026, 9, 1).AddDays(day), 100m + day, 100m + day + 1, 100m + day - 1, 100m + day, 1000))
                .ToList();
            return Task.FromResult<IReadOnlyList<Candle>>(days);
        }

        public Task<IReadOnlyList<MarketQuote>> SearchAsync(string query, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<MarketQuote>>([
            new(query, "XNAS", "Example Corp", "USD", "EQUITY", "Technology", "Consumer Electronics")
        ]);
    }

    [NeonFact]
    public async Task Ensuring_a_new_asset_creates_the_row_and_loads_the_history()
    {
        using var api = new AccountsApi();
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var source = new FakeSource();
        var catalog = new AssetCatalog(db, source, new PriceRefresher(db, source, NullLogger<PriceRefresher>.Instance));
        var symbol = $"TEST{Guid.NewGuid():N}"[..10];

        var created = await catalog.EnsureAsync(symbol);

        Assert.True(created.Created);
        Assert.Equal(symbol.ToUpperInvariant(), created.Symbol);
        Assert.Equal("Technology · Consumer Electronics", created.What);
        Assert.Equal(AssetKind.Stock, created.Kind);
        Assert.True(await db.Assets.AnyAsync(a => a.Id == created.Id));
        Assert.True(await db.Prices.AnyAsync(p => p.AssetId == created.Id));
        Assert.Equal(30, await db.Prices.CountAsync(p => p.AssetId == created.Id));

        var again = await catalog.EnsureAsync(symbol);
        Assert.False(again.Created);
        Assert.Equal(created.Id, again.Id);
    }
}
