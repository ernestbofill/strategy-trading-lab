using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Evaluation;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Tests;

public class StrategyEvaluatorTests
{
    sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public void The_stored_status_has_every_engine_status_by_the_same_name() =>
        Assert.Equal(Enum.GetNames<StrategyTradingLab.Api.Engine.RuleStatus>(), Enum.GetNames<EvaluationStatus>());

    [Fact]
    public void The_last_complete_session_is_the_latest_weekday_after_the_close()
    {
        // 2026-10-23 is a Friday; New York is UTC-4 then, so 16:30 there is 20:30 UTC.
        Assert.Equal(new DateOnly(2026, 10, 23), UsSession.LastComplete(new DateTimeOffset(2026, 10, 23, 20, 30, 0, TimeSpan.Zero)));
        Assert.Equal(new DateOnly(2026, 10, 22), UsSession.LastComplete(new DateTimeOffset(2026, 10, 23, 20, 29, 0, TimeSpan.Zero)));
        Assert.Equal(new DateOnly(2026, 10, 23), UsSession.LastComplete(new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero))); // Sunday.
        Assert.Equal(new DateOnly(2026, 10, 23), UsSession.LastComplete(new DateTimeOffset(2026, 10, 26, 13, 0, 0, TimeSpan.Zero))); // Monday, before the open.
    }

    [Fact]
    public void A_rate_between_two_currencies_goes_through_the_euro_and_uses_the_latest_day_on_or_before()
    {
        var rates = new ExchangeRates(new()
        {
            ["USD"] = [(new DateOnly(2026, 10, 1), 1.10m), (new DateOnly(2026, 10, 10), 1.20m)],
            ["GBP"] = [(new DateOnly(2026, 10, 1), 0.85m)],
        });

        Assert.Equal(1m, rates.Between("EUR", "EUR", new DateOnly(2026, 10, 5)));
        Assert.Equal(1.10m, rates.Between("EUR", "USD", new DateOnly(2026, 10, 5)));
        Assert.Equal(1.20m, rates.Between("EUR", "USD", new DateOnly(2026, 10, 12)));
        Assert.Equal(1.10m / 0.85m, rates.Between("GBP", "USD", new DateOnly(2026, 10, 5)));
        Assert.Null(rates.Between("EUR", "USD", new DateOnly(2026, 9, 1)));
        Assert.Null(rates.Between("EUR", "HKD", new DateOnly(2026, 10, 5)));
    }

    /// <summary>
    /// 300 days of 0.3% growth, then a dip to 6% under the peak (day 300, a buy signal for step 1), then a day at the
    /// same price (301, where the signal's trade is made), and optionally a day back up to the peak (302, the window closes).
    /// </summary>
    static (List<PriceDaily> Prices, DateOnly Start) DipSeries(Guid assetId, bool windowCloses)
    {
        var first = new DateOnly(2024, 1, 1);
        var closes = Enumerable.Range(0, 300).Select(i => Math.Round(100m * (decimal)Math.Pow(1.003, i), 4)).ToList();
        var peak = closes.TakeLast(60).Max();
        closes.Add(Math.Round(peak * 0.94m, 4));
        closes.Add(closes[^1]);
        if (windowCloses) closes.Add(peak);
        var prices = closes.Select((close, i) => new PriceDaily
            { AssetId = assetId, Date = first.AddDays(i), Open = close, High = close, Low = close, Close = close, Volume = 1000 }).ToList();
        return (prices, first.AddDays(299));
    }

    [Fact]
    public void The_dip_series_gives_one_signal_on_day_300_and_its_window_closes_on_day_302()
    {
        var (prices, start) = DipSeries(Guid.NewGuid(), windowCloses: true);
        var bars = prices.Select(p => new StrategyTradingLab.Api.Engine.PriceBar(p.Date, p.Open, p.High, p.Low, p.Close, p.Volume)).ToList();
        var rules = new StrategyTradingLab.Api.Engine.DipRules(bars, new DipParams());

        var days = StrategyTradingLab.Api.Engine.RulesRunner.Run(rules, bars, new StrategyTradingLab.Api.Engine.StepPlan(5), from: 299);

        Assert.Equal(start, days[0].Date);
        Assert.Equal([new DateOnly(2024, 1, 1).AddDays(300)], days.Where(d => d.NewBuy).Select(d => d.Date));
        Assert.True(days[2].WindowOpen); // Day 301: still in the zone.
        Assert.False(days[3].WindowOpen); // Day 302: back at the peak.
    }

    static async Task<(Guid UserId, Guid StrategyId, Guid AssetId)> SeedAsync(AppDbContext db, StrategyMode mode, bool windowCloses)
    {
        var tag = Guid.NewGuid().ToString("N")[..10];
        var asset = new Asset { Symbol = $"E{tag}", Exchange = "TEST", Name = "Test asset", Currency = "USD" };
        var user = new User { Email = $"eval-{tag}@example.com", TimeZone = "UTC", Locale = "en" };
        db.Assets.Add(asset);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var (prices, start) = DipSeries(asset.Id, windowCloses);
        db.Prices.AddRange(prices);
        var strategy = new Strategy
        {
            UserId = user.Id, TemplateKey = "dip", Name = $"Eval {tag}", Mode = mode, Status = StrategyStatus.Running, StartedOn = start,
            Currency = "USD", Budget = new Budget(3000, 100, 5), Params = TemplateCatalog.Dip.ParamsJson(),
            Assets = [new StrategyAsset { AssetId = asset.Id, Active = true, AddedAt = new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) }],
        };
        db.Strategies.Add(strategy);
        await db.SaveChangesAsync();
        return (user.Id, strategy.Id, asset.Id);
    }

    static async Task CleanAsync(AppDbContext db, Guid userId, Guid assetId)
    {
        await db.Users.Where(u => u.Id == userId).ExecuteDeleteAsync();
        await db.Assets.Where(a => a.Id == assetId).ExecuteDeleteAsync();
    }

    static Task<int> SignalCount(AppDbContext db, Guid strategyId) => db.Signals.CountAsync(s => s.StrategyId == strategyId);

    static Task<int> TradeCount(AppDbContext db, Guid strategyId) => db.SignalTrades.CountAsync(t => t.StrategyId == strategyId);

    static StrategyEvaluator Evaluator(AppDbContext db) =>
        new(db, new Clock(new DateTimeOffset(2024, 12, 2, 12, 0, 0, TimeSpan.Zero)), NullLogger<StrategyEvaluator>.Instance);

    [NeonFact]
    public async Task A_simulation_strategy_gets_a_confirmed_signal_and_its_trade_at_the_next_open_once()
    {
        using var api = new AccountsApi();
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (userId, strategyId, assetId) = await SeedAsync(db, StrategyMode.Simulation, windowCloses: false);
        try
        {
            // Counted on this strategy's own rows: the run evaluates every running strategy, and other test classes add theirs in parallel.
            await Evaluator(db).RunAsync(default);
            Assert.Equal((1, 1), (await SignalCount(db, strategyId), await TradeCount(db, strategyId)));
            await Evaluator(db).RunAsync(default);
            Assert.Equal((1, 1), (await SignalCount(db, strategyId), await TradeCount(db, strategyId)));
            var signal = await db.Signals.SingleAsync(s => s.StrategyId == strategyId);
            Assert.Equal(new DateOnly(2024, 1, 1).AddDays(300), signal.SignalDate);
            Assert.Equal((1, 5, 100m), (signal.StepNo, signal.MaxSteps, signal.StepAmount));
            Assert.Equal(SignalStatus.Confirmed, signal.Status);
            Assert.NotNull(signal.AnsweredAt);
            Assert.Null(signal.EndedOn); // The price is still in the zone on the last day.
            Assert.True(signal.Facts.RootElement.GetProperty("belowPeakPct").GetDecimal() is > 5 and < 7);
            var trade = await db.SignalTrades.SingleAsync(t => t.SignalId == signal.Id);
            Assert.Equal(new DateOnly(2024, 1, 1).AddDays(301), trade.TradeDate);
            Assert.Equal(1m, trade.FxRate);
            Assert.Equal(98.80m, trade.Amount); // The step includes its fee: 98.80 + 0.2% of it + 1 = 100.
            Assert.Equal(1.20m, trade.Fee);
            Assert.Equal(Math.Round(98.80m / trade.Price, 6), trade.Shares);
            var today = await db.DailyEvaluations.SingleAsync(e => e.StrategyId == strategyId);
            Assert.Equal(new DateOnly(2024, 1, 1).AddDays(301), today.Date);
            Assert.Equal(EvaluationStatus.Building, today.Status);
        }
        finally
        {
            await CleanAsync(db, userId, assetId);
        }
    }

    [NeonFact]
    public async Task A_live_signal_waits_for_the_user_and_is_missed_when_its_window_closes()
    {
        using var api = new AccountsApi();
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (userId, strategyId, assetId) = await SeedAsync(db, StrategyMode.Live, windowCloses: true);
        try
        {
            await Evaluator(db).RunAsync(default);

            var signal = await db.Signals.SingleAsync(s => s.StrategyId == strategyId);
            Assert.Equal(SignalStatus.Missed, signal.Status); // Back up to the peak: out of the zone.
            Assert.Equal(new DateOnly(2024, 1, 1).AddDays(302), signal.EndedOn);
            Assert.Null(signal.AnsweredAt);
            Assert.Equal(1, await db.SignalTrades.CountAsync(t => t.SignalId == signal.Id)); // The rules carry on as if it was bought.
        }
        finally
        {
            await CleanAsync(db, userId, assetId);
        }
    }

    [NeonFact]
    public async Task A_strategy_that_started_after_the_last_price_is_left_alone()
    {
        using var api = new AccountsApi();
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (userId, strategyId, assetId) = await SeedAsync(db, StrategyMode.Simulation, windowCloses: false);
        try
        {
            await db.Strategies.Where(s => s.Id == strategyId).ExecuteUpdateAsync(s => s.SetProperty(x => x.StartedOn, new DateOnly(2030, 1, 1)));

            await Evaluator(db).RunAsync(default);

            Assert.Equal(0, await SignalCount(db, strategyId));
            Assert.Empty(await db.DailyEvaluations.Where(e => e.StrategyId == strategyId).ToListAsync());
        }
        finally
        {
            await CleanAsync(db, userId, assetId);
        }
    }

    [NeonFact]
    public async Task No_signal_is_made_when_its_step_would_not_fit_in_the_budget()
    {
        using var api = new AccountsApi();
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (userId, strategyId, assetId) = await SeedAsync(db, StrategyMode.Simulation, windowCloses: false);
        try
        {
            // A budget of one step, already used by an earlier signal.
            await db.Strategies.Where(s => s.Id == strategyId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Budget, new Budget(100, 100, 5)));
            db.Signals.Add(new Signal
            {
                StrategyId = strategyId, AssetId = assetId, Side = TradeSide.Buy, StepNo = 1, MaxSteps = 5, StepAmount = 100, SignalDate = new DateOnly(2020, 1, 1),
                ClosePrice = 1, Facts = JsonDocument.Parse("{}"), Status = SignalStatus.Confirmed, AnsweredAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();

            await Evaluator(db).RunAsync(default);

            Assert.Equal(1, await SignalCount(db, strategyId)); // Only the earlier one.
        }
        finally
        {
            await CleanAsync(db, userId, assetId);
        }
    }

    [NeonFact]
    public async Task A_trend_strategy_buys_all_in_sells_all_out_and_reinvests_the_proceeds_on_the_next_buy()
    {
        using var api = new AccountsApi();
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tag = Guid.NewGuid().ToString("N")[..10];
        var asset = new Asset { Symbol = $"T{tag}", Exchange = "TEST", Name = "Trend asset", Currency = "USD", Kind = AssetKind.Index };
        var user = new User { Email = $"trend-{tag}@example.com", TimeZone = "UTC", Locale = "en" };
        db.Assets.Add(asset);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var first = new DateOnly(2024, 1, 1);
        var prices = new List<PriceDaily>();
        for (var i = 0; i < 200; i++)
            prices.Add(new PriceDaily { AssetId = asset.Id, Date = first.AddDays(i), Open = 100, High = 100, Low = 100, Close = 100, Volume = 1000 });
        for (var i = 200; i < 206; i++)
            prices.Add(new PriceDaily { AssetId = asset.Id, Date = first.AddDays(i), Open = 105, High = 105, Low = 105, Close = 105, Volume = 1000 });
        prices.Add(new PriceDaily { AssetId = asset.Id, Date = first.AddDays(206), Open = 90, High = 90, Low = 90, Close = 90, Volume = 1000 });
        for (var i = 207; i < 212; i++)
            prices.Add(new PriceDaily { AssetId = asset.Id, Date = first.AddDays(i), Open = 120, High = 120, Low = 100, Close = 100, Volume = 1000 });
        prices.Add(new PriceDaily { AssetId = asset.Id, Date = first.AddDays(212), Open = 120, High = 120, Low = 120, Close = 120, Volume = 1000 });
        prices.Add(new PriceDaily { AssetId = asset.Id, Date = first.AddDays(213), Open = 120, High = 120, Low = 120, Close = 120, Volume = 1000 });
        db.Prices.AddRange(prices);
        var start = first.AddDays(199);
        var strategy = new Strategy
        {
            UserId = user.Id, TemplateKey = "trend", Name = $"Trend {tag}", Mode = StrategyMode.Simulation, Status = StrategyStatus.Running, StartedOn = start,
            Currency = "USD", Budget = new Budget(1000, null, null), Params = TemplateCatalog.Trend.ParamsJson(),
            Assets = [new StrategyAsset { AssetId = asset.Id, Active = true, AddedAt = new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) }],
        };
        db.Strategies.Add(strategy);
        await db.SaveChangesAsync();
        try
        {
            await Evaluator(db).RunAsync(default);

            var signals = await db.Signals.Where(s => s.StrategyId == strategy.Id).OrderBy(s => s.SignalDate).ToListAsync();
            Assert.Equal(3, signals.Count);
            Assert.Equal((TradeSide.Buy, TradeSide.Sell, TradeSide.Buy), (signals[0].Side, signals[1].Side, signals[2].Side));

            var trades = await db.SignalTrades.Where(t => t.StrategyId == strategy.Id).OrderBy(t => t.TradeDate).ToListAsync();
            Assert.Equal(3, trades.Count);
            Assert.Equal((TradeSide.Buy, TradeSide.Sell, TradeSide.Buy), (trades[0].Side, trades[1].Side, trades[2].Side));
            Assert.Equal(1000m, trades[0].Amount + trades[0].Fee);
            Assert.Equal(trades[0].Shares, trades[1].Shares); // All out.
            Assert.True(trades[2].Amount + trades[2].Fee > 1000m); // Reinvested the higher sale proceeds from day 207.
            Assert.Equal(trades[1].Amount - trades[1].Fee, trades[2].Amount + trades[2].Fee);
        }
        finally
        {
            await CleanAsync(db, user.Id, asset.Id);
        }
    }
}
