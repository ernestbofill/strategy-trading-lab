using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.MarketData;

namespace StrategyTradingLab.Api.Tests;

public class PriceRefresherTests
{
    static Candle Day(int day, decimal close) => new(new DateOnly(2026, 9, 1).AddDays(day), close, close + 1, close - 1, close, 1000);

    /// <summary>The recent month is days 20–29 of a 30-day history; the fake answers whichever was asked for.</summary>
    sealed class FakeSource(Func<int, decimal> close) : IMarketDataSource
    {
        public int FullRequests { get; private set; }
        public bool Fails { get; set; }

        public Task<IReadOnlyList<Candle>> DailyAsync(string symbol, bool fullHistory, bool adjusted, CancellationToken cancellationToken)
        {
            if (Fails) throw new MarketDataException("down");
            if (fullHistory) FullRequests++;
            IReadOnlyList<Candle> days = [.. Enumerable.Range(fullHistory ? 0 : 20, fullHistory ? 30 : 10).Select(d => Day(d, close(d)))];
            return Task.FromResult(days);
        }

        public Task<IReadOnlyList<MarketQuote>> SearchAsync(string query, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MarketQuote>>([]);
    }

    static IReadOnlyList<Candle> Recent(params (int Day, decimal Close)[] days) => [.. days.Select(d => Day(d.Day, d.Close))];

    [Fact]
    public void A_first_run_with_nothing_stored_needs_the_full_history() =>
        Assert.True(PriceRefresher.NeedsFullHistory(Recent((1, 10), (2, 11)), new Dictionary<DateOnly, decimal>()));

    [Fact]
    public void Matching_closes_only_need_the_recent_days()
    {
        var stored = new Dictionary<DateOnly, decimal> { [Day(1, 0).Date] = 10.005m, [Day(2, 0).Date] = 50 };

        // Day 2 is the latest: a bar still in progress can differ, so it isn't compared.
        Assert.False(PriceRefresher.NeedsFullHistory(Recent((1, 10), (2, 11)), stored));
    }

    [Fact]
    public void A_re_adjusted_close_needs_the_full_history()
    {
        var stored = new Dictionary<DateOnly, decimal> { [Day(1, 0).Date] = 10.5m, [Day(2, 0).Date] = 11 };

        Assert.True(PriceRefresher.NeedsFullHistory(Recent((1, 10), (2, 11), (3, 12)), stored));
    }

    [Fact]
    public void A_gap_with_no_shared_day_needs_the_full_history() =>
        Assert.True(PriceRefresher.NeedsFullHistory(Recent((20, 10), (21, 11)), new Dictionary<DateOnly, decimal> { [Day(1, 0).Date] = 9 }));

    static async Task<(Asset Asset, AppDbContext Db, IServiceScope Scope)> NewAssetAsync(AccountsApi api)
    {
        var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var asset = new Asset { Symbol = $"T{Guid.NewGuid():N}"[..10], Exchange = "TEST", Name = "Test asset", Currency = "USD" };
        db.Assets.Add(asset);
        await db.SaveChangesAsync();
        return (asset, db, scope);
    }

    [NeonFact]
    public async Task The_first_run_stores_the_history_and_a_matching_second_run_only_the_recent_days()
    {
        using var api = new AccountsApi();
        var (asset, db, scope) = await NewAssetAsync(api);
        using var _ = scope;
        try
        {
            var source = new FakeSource(d => 100 + d);
            var refresher = new PriceRefresher(db, source, NullLogger<PriceRefresher>.Instance);

            var first = await refresher.RefreshAssetAsync(asset, default);
            var second = await refresher.RefreshAssetAsync(asset, default);

            Assert.Equal(RefreshOutcome.FullHistory, first.Outcome);
            Assert.Equal(RefreshOutcome.Updated, second.Outcome);
            Assert.Equal(1, source.FullRequests);
            Assert.Equal(30, await db.Prices.CountAsync(p => p.AssetId == asset.Id));
            Assert.Equal(129, (await db.Prices.SingleAsync(p => p.AssetId == asset.Id && p.Date == Day(29, 0).Date)).Close);
        }
        finally
        {
            await db.Assets.Where(a => a.Id == asset.Id).ExecuteDeleteAsync();
        }
    }

    [NeonFact]
    public async Task A_re_adjusted_history_replaces_what_was_stored()
    {
        using var api = new AccountsApi();
        var (asset, db, scope) = await NewAssetAsync(api);
        using var _ = scope;
        try
        {
            var refresher = new PriceRefresher(db, new FakeSource(d => 100 + d), NullLogger<PriceRefresher>.Instance);
            await refresher.RefreshAssetAsync(asset, default);

            var halved = new FakeSource(d => (100 + d) / 2m);
            var result = await new PriceRefresher(db, halved, NullLogger<PriceRefresher>.Instance).RefreshAssetAsync(asset, default);

            Assert.Equal(RefreshOutcome.FullHistory, result.Outcome);
            Assert.Equal(30, await db.Prices.CountAsync(p => p.AssetId == asset.Id));
            Assert.Equal(64.5m, (await db.Prices.SingleAsync(p => p.AssetId == asset.Id && p.Date == Day(29, 0).Date)).Close);
        }
        finally
        {
            await db.Assets.Where(a => a.Id == asset.Id).ExecuteDeleteAsync();
        }
    }

    [NeonFact]
    public async Task A_failing_source_keeps_what_was_stored_and_says_why()
    {
        using var api = new AccountsApi();
        var (asset, db, scope) = await NewAssetAsync(api);
        using var _ = scope;
        try
        {
            var source = new FakeSource(d => 100 + d);
            var refresher = new PriceRefresher(db, source, NullLogger<PriceRefresher>.Instance);
            await refresher.RefreshAssetAsync(asset, default);
            source.Fails = true;

            await Assert.ThrowsAsync<MarketDataException>(() => refresher.RefreshAssetAsync(asset, default));

            Assert.Equal(30, await db.Prices.CountAsync(p => p.AssetId == asset.Id));
        }
        finally
        {
            await db.Assets.Where(a => a.Id == asset.Id).ExecuteDeleteAsync();
        }
    }

    [NeonFact]
    public async Task Exchange_rates_are_stored_per_day_and_refreshed_in_place()
    {
        using var api = new AccountsApi();
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        try
        {
            var refresher = new PriceRefresher(db, new FakeSource(d => 1 + d / 100m), NullLogger<PriceRefresher>.Instance);

            var first = await refresher.RefreshRateAsync("TST", default);
            var second = await refresher.RefreshRateAsync("TST", default);

            Assert.Equal(RefreshOutcome.FullHistory, first.Outcome);
            Assert.Equal(RefreshOutcome.Updated, second.Outcome);
            Assert.Equal(30, await db.FxRates.CountAsync(r => r.Base == "EUR" && r.Quote == "TST"));
            Assert.Equal(1.29m, (await db.FxRates.SingleAsync(r => r.Quote == "TST" && r.Date == Day(29, 0).Date)).Rate);
        }
        finally
        {
            await db.FxRates.Where(r => r.Quote == "TST").ExecuteDeleteAsync();
        }
    }
}
