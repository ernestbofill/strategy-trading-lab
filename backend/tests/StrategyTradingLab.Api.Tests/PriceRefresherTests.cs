using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.MarketData;
using StrategyTradingLab.Api.Strategies;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Tests;

public class PriceRefresherTests
{
    static Candle Day(int day, decimal close) => new(new DateOnly(2026, 9, 1).AddDays(day), close, close + 1, close - 1, close, 1000);

    /// <summary>The recent month is days 20–29 of a 30-day history; the fake answers whichever was asked for.</summary>
    sealed class FakeSource(Func<int, decimal> close) : IMarketDataSource
    {
        public int FullRequests { get; private set; }
        /// <summary>The first day the last whole-history request asked for.</summary>
        public DateOnly? Since { get; private set; }
        public bool Fails { get; set; }

        public Task<IReadOnlyList<Candle>> DailyAsync(string symbol, DateOnly? since, bool adjusted, CancellationToken cancellationToken)
        {
            if (Fails) throw new MarketDataException("down");
            if (since is not null) (FullRequests, Since) = (FullRequests + 1, since);
            IReadOnlyList<Candle> days = [.. Enumerable.Range(since is not null ? 0 : 20, since is not null ? 30 : 10).Select(d => Day(d, close(d)))];
            return Task.FromResult(days);
        }

        public Task<IReadOnlyList<SymbolMatch>> SearchAsync(string query, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SymbolMatch>>([]);

        public Task<string> CurrencyAsync(string symbol, CancellationToken cancellationToken) => Task.FromResult("USD");
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

    [Fact]
    public void The_history_starts_two_years_before_today_or_the_earliest_strategy()
    {
        var today = new DateOnly(2026, 10, 31);

        Assert.Equal(new DateOnly(2024, 10, 31), PriceRefresher.HistoryStart(null, today));
        Assert.Equal(new DateOnly(2023, 3, 1), PriceRefresher.HistoryStart(new DateOnly(2025, 3, 1), today));
        Assert.Equal(new DateOnly(2024, 10, 31), PriceRefresher.HistoryStart(new DateOnly(2026, 11, 2), today)); // Starts later: today.
    }

    sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

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
            var failed = await db.Assets.AsNoTracking().SingleAsync(a => a.Id == asset.Id);
            Assert.Equal("down", failed.PricesProblem);
            Assert.NotNull(failed.PricesCheckedAt);

            source.Fails = false;
            await refresher.RefreshAssetAsync(asset, default);

            Assert.Null((await db.Assets.AsNoTracking().SingleAsync(a => a.Id == asset.Id)).PricesProblem);
        }
        finally
        {
            await db.Assets.Where(a => a.Id == asset.Id).ExecuteDeleteAsync();
        }
    }

    [NeonFact]
    public async Task A_re_adjusted_history_is_fetched_from_two_years_back_or_over_the_span_already_stored()
    {
        using var api = new AccountsApi();
        var (asset, db, scope) = await NewAssetAsync(api);
        using var _ = scope;
        try
        {
            var time = new FixedTime(new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero));
            var source = new FakeSource(d => 100 + d);
            await new PriceRefresher(db, source, NullLogger<PriceRefresher>.Instance) { Time = time }.RefreshAssetAsync(asset, default);
            Assert.Equal(new DateOnly(2024, 10, 31), source.Since); // No strategy lists it: two years before today.

            // A longer history downloaded by hand: a re-adjusting dividend fetches it all again, not only two years.
            db.Prices.Add(new PriceDaily { AssetId = asset.Id, Date = new DateOnly(2020, 1, 2), Open = 50, High = 51, Low = 49, Close = 50, Volume = 1 });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var halved = new FakeSource(d => (100 + d) / 2m);

            var result = await new PriceRefresher(db, halved, NullLogger<PriceRefresher>.Instance) { Time = time }.RefreshAssetAsync(asset, default);

            Assert.Equal(RefreshOutcome.FullHistory, result.Outcome);
            Assert.Equal(new DateOnly(2020, 1, 2), halved.Since);
        }
        finally
        {
            await db.Assets.Where(a => a.Id == asset.Id).ExecuteDeleteAsync();
        }
    }

    [NeonFact]
    public async Task A_trade_recorded_before_the_stored_days_fetches_two_years_before_it()
    {
        using var api = new AccountsApi();
        var (asset, db, scope) = await NewAssetAsync(api);
        using var _ = scope;
        var user = new User { Email = $"held-{Guid.NewGuid():N}@example.com", TimeZone = "UTC", Locale = "en" };
        try
        {
            var time = new FixedTime(new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero));
            var source = new FakeSource(d => 100 + d);
            var refresher = new PriceRefresher(db, source, NullLogger<PriceRefresher>.Instance) { Time = time };
            await refresher.RefreshAssetAsync(asset, default);
            // A holding bought long before the strategy and the stored days (2026-09-01 on).
            db.Users.Add(user);
            await db.SaveChangesAsync();
            var strategy = new Strategy
            {
                UserId = user.Id, TemplateKey = "dip", Name = "Held", Mode = StrategyMode.Live, Status = StrategyStatus.Running,
                StartedOn = new DateOnly(2026, 10, 1), Currency = "USD", Budget = new Budget(3000, 100, 5), Params = TemplateCatalog.Dip.ParamsJson(),
                Assets = [new StrategyAsset { AssetId = asset.Id, Active = true, AddedAt = DateTimeOffset.UtcNow }],
            };
            db.Strategies.Add(strategy);
            await db.SaveChangesAsync();
            db.UserTrades.Add(new UserTrade
            {
                StrategyId = strategy.Id, AssetId = asset.Id, TradeDate = new DateOnly(2025, 1, 15), Side = TradeSide.Buy, Price = 100,
                EnteredAs = EnteredAs.Amount, Amount = 99, Shares = 0.99m, Fee = 1, FxRate = 1, FxSource = FxSource.User, Source = TradeSource.Manual,
            });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var result = await refresher.RefreshAssetAsync(asset, default);

            Assert.Equal(RefreshOutcome.FullHistory, result.Outcome);
            Assert.Equal(new DateOnly(2023, 1, 15), source.Since);
        }
        finally
        {
            await db.Users.Where(u => u.Id == user.Id).ExecuteDeleteAsync();
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

    [NeonFact]
    public async Task Only_assets_a_running_strategy_lists_and_the_index_fund_are_refreshed_daily()
    {
        using var api = new AccountsApi();
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tag = Guid.NewGuid().ToString("N")[..9];
        Asset New(string prefix) => new() { Symbol = $"{prefix}{tag}", Exchange = "TEST", Name = "Test asset", Currency = "USD" };
        var (unticked, ended, unlisted) = (New("U"), New("E"), New("N"));
        var user = new User { Email = $"watch-{tag}@example.com", TimeZone = "UTC", Locale = "en" };
        db.AddRange(unticked, ended, unlisted, user);
        await db.SaveChangesAsync();
        Strategy Listing(Asset asset, StrategyStatus status) => new()
        {
            UserId = user.Id, TemplateKey = "dip", Name = $"Watch {asset.Symbol}", Mode = StrategyMode.Simulation, Status = status,
            StartedOn = new DateOnly(2026, 10, 1), EndedAt = status == StrategyStatus.Ended ? DateTimeOffset.UtcNow : null, Currency = "USD", Budget = new Budget(3000, 100, 5), Params = TemplateCatalog.Dip.ParamsJson(),
            Assets = [new StrategyAsset { AssetId = asset.Id, Active = false, AddedAt = DateTimeOffset.UtcNow }],
        };
        db.Strategies.AddRange(Listing(unticked, StrategyStatus.Running), Listing(ended, StrategyStatus.Ended));
        await db.SaveChangesAsync();
        try
        {
            var watched = await new PriceRefresher(db, new FakeSource(d => 100), NullLogger<PriceRefresher>.Instance).Watched()
                .Select(a => a.Id).ToListAsync();

            Assert.Contains(unticked.Id, watched); // Unticked, but still on a running strategy's list.
            Assert.Contains(StrategyBook.IndexFundId, watched);
            Assert.DoesNotContain(ended.Id, watched);
            Assert.DoesNotContain(unlisted.Id, watched);
        }
        finally
        {
            await db.Users.Where(u => u.Id == user.Id).ExecuteDeleteAsync();
            await db.Assets.Where(a => a.Symbol.EndsWith(tag)).ExecuteDeleteAsync();
        }
    }
}
