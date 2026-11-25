using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StrategyTradingLab.Api.Admin;
using StrategyTradingLab.Api.Backtest;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Engine;
using StrategyTradingLab.Api.Evaluation;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Tests;

/// <summary>The backtest's simulator (ROADMAP item 53): a hand-worked case, the POC's golden runs, and the app's own trades.</summary>
public class BacktestSimulatorTests(AccountsApi api) : IClassFixture<AccountsApi>
{
    /// <summary>Rules that signal a buy (or a sale) on the days given, by index, and otherwise watch or hold.</summary>
    sealed class Scripted(int length, IReadOnlySet<int> buys, IReadOnlySet<int>? sells = null) : BuyRulesBase(length)
    {
        public override int MinHistory => 1;
        public override int CooldownDays => 0;

        protected override RuleDecision DecideDay(int i, DayContext context) =>
            new(buys.Contains(i) ? RuleStatus.BuySignal : sells?.Contains(i) == true ? RuleStatus.SellSignal
                : context.HasPosition ? RuleStatus.Holding : RuleStatus.Watching, null, new Dictionary<string, object>());
    }

    static readonly DateOnly First = new(2023, 12, 25);

    /// <summary>Every day from 25 December 2023 to 31 March 2024 at 10, except the closes given.</summary>
    static TestAsset Flat(string symbol, string currency = "EUR", Dictionary<DateOnly, decimal>? closes = null, decimal endClose = 10)
    {
        var bars = new List<PriceBar>();
        for (var d = First; d <= new DateOnly(2024, 3, 31); d = d.AddDays(1))
        {
            var close = d == new DateOnly(2024, 3, 31) ? endClose : closes?.GetValueOrDefault(d, 10) ?? 10;
            bars.Add(new(d, 10, 10, 10, close, symbol[0])); // The volume names the stock for the scripted rules.
        }
        return new(symbol, currency, bars);
    }

    static int Day(DateOnly date) => date.DayNumber - First.DayNumber;

    static readonly BacktestPeriod Quarter = new(2024, 2024, new(2024, 1, 1), new(2024, 3, 31), First, Partial: false);

    static decimal Euro(string currency, DateOnly day) => 1;

    [Fact]
    public void A_hand_worked_quarter_pays_the_signals_the_money_allows_and_measures_every_line_alike()
    {
        // €300, €100 a trade, at most €100 a month, no costs. 2 Jan: A and B signal, January pays one. 20 Jan: C signals, nothing left
        // that month. February: nothing, its €100 carries over. 5 Mar: A's and C's second trades, €200 of room: both paid.
        var (jan2, jan20, mar5) = (Day(new(2024, 1, 2)), Day(new(2024, 1, 20)), Day(new(2024, 3, 5)));
        var signals = new Dictionary<string, HashSet<int>> { ["A"] = [jan2, mar5], ["B"] = [jan2], ["C"] = [jan20, mar5] };
        var stocks = new[] { Flat("A", endClose: 11), Flat("B", endClose: 11), Flat("C", endClose: 11) };
        // Baselines 1 and 3 buy €100 on each month's first day (the index's): a third of the budget in January, two in February, all in March.
        var rules = new TestRules(bars => new Scripted(bars.Count, signals[((char)bars[0].Volume).ToString()]), 2, 100);
        var money = new TestMoney(Budget: 300, MonthlyLimit: 100, FeePct: 0, SpreadPct: 0);

        var result = BacktestSimulator.Run(rules, money, [new(Quarter, stocks)], Flat("SPY", "USD", endClose: 11), Euro);
        var period = result.Periods.Single();

        var buys = period.Trades;
        Assert.Equal(3, buys.Count);
        var first = buys[0];
        Assert.Equal(new DateOnly(2024, 1, 3), first.Date); // The next open.
        Assert.Contains(first.Symbol, new[] { "A", "B" });
        Assert.Equal((1, 2), (first.StepNo, first.MaxSteps));
        Assert.Equal([("A", 2), ("C", 2)], buys.Skip(1).Select(t => (t.Symbol, t.StepNo!.Value)).Order());
        Assert.All(buys.Skip(1), t => Assert.Equal(new DateOnly(2024, 3, 6), t.Date));
        Assert.Equal(2, period.Unpaid); // A or B on 2 Jan, C on 20 Jan.
        Assert.All(buys, t => Assert.Equal(10m, t.Shares));

        // 30 shares at 11: €330 for €300, put in on 3 Jan (€100) and 6 Mar (€200).
        var expected = MoneyWeighted.AnnualRate([(new(2024, 1, 3), -100m), (new(2024, 3, 6), -200m), (new(2024, 3, 31), 330m)])!.Value;
        Assert.Equal(expected, period.Strategy.Rate!.Value, 9);
        Assert.Equal((300m, 330m, 3), (period.Strategy.Invested, period.Strategy.EndValue, period.Strategy.Trades));
        // The year's own return, from the first money in: (1 + rate) over 88 days of the year.
        Assert.Equal(Math.Pow(1 + expected, 88 / 365.0) - 1, period.Strategy.Worst!.Return, 9);
        // €100 from 3 Jan to 5 Mar (63 days at a third), €300 from 6 Mar (26 days), over the quarter's 91 days.
        Assert.Equal(47 / 91.0, period.Strategy.BudgetAtWork, 9);

        // Baselines 1 and 3 buy €100 on each month's first day (the index's), baseline 2 on the strategy's days.
        Assert.Equal((300m, 330m, 3), (period.Index.Invested, period.Index.EndValue, period.Index.Trades));
        Assert.Equal((300m, 330m, 9), (period.SameStocks.Invested, period.SameStocks.EndValue, period.SameStocks.Trades));
        Assert.Equal((300m, 330m, 9), (period.SameDays!.Invested, period.SameDays.EndValue, period.SameDays.Trades));
        Assert.Equal(expected, period.SameDays.Rate!.Value, 9);
        Assert.Equal((31 / 3.0 + 29 * 2 / 3.0 + 31) / 91, period.Index.BudgetAtWork, 9);
        Assert.True(period.Index.Rate < period.Strategy.Rate); // The same gain, held longer.

        // The same seed gives the same draw of who is paid on 2 Jan; All of one period is that period.
        Assert.Equal(first.Symbol, BacktestSimulator.Run(rules, money, [new(Quarter, stocks)], Flat("SPY", "USD", endClose: 11), Euro).Periods[0].Trades[0].Symbol);
        Assert.Equal((period.Strategy.Rate, 3), (result.Strategy.Rate, result.Strategy.Trades));
    }

    [Fact]
    public void A_trade_pays_its_fee_and_spread_in_the_stocks_currency_at_the_days_rate()
    {
        var money = new TestMoney(Budget: 1000, MonthlyLimit: 1000, FeePct: 0.1m, SpreadPct: 0.1m);
        var line = new BacktestLine(money, (currency, _) => currency == "USD" ? 1.1m : 1);
        var stock = Flat("A", "USD");

        var buy = line.Buy(stock, stock.Bars[10], 100);
        var sale = line.SellAll("A", stock.Bars[20])!;

        // €100 is $110: the fee comes out of the amount, the spread is added to the price.
        Assert.Equal(110m / 1.001m / (10m * 1.001m), buy.Shares);
        Assert.Equal(100m - buy.Shares * 10m / 1.1m, buy.Costs);
        Assert.Equal(buy.Shares * 10m * 0.999m * 0.999m / 1.1m, sale.Amount);
        Assert.Equal(sale.Amount, line.Idle);
        Assert.Equal(100m, line.NewMoney);
    }

    [Fact]
    public void Index_trend_takes_each_months_limit_while_it_holds_and_reinvests_its_sales_outside_the_limit()
    {
        // €300, at most €100 a month, all in. Buy signal 2 Jan; sale signal 14 Feb (sold at 15 Feb's open, 12); buy signal 4 Mar.
        var opens = new Dictionary<DateOnly, decimal> { [new(2024, 2, 15)] = 12 };
        var fund = Flat("SPY", "EUR") with { Bars = [.. Flat("SPY").Bars.Select(b => opens.TryGetValue(b.Date, out var o) ? b with { Open = o } : b)] };
        var rules = new TestRules(bars => new Scripted(bars.Count, new HashSet<int> { Day(new(2024, 1, 2)), Day(new(2024, 3, 4)) },
            new HashSet<int> { Day(new(2024, 2, 14)) }), 1, null);
        var money = new TestMoney(Budget: 300, MonthlyLimit: 100, FeePct: 0, SpreadPct: 0);

        var period = BacktestSimulator.Run(rules, money, [new(Quarter, [fund])], fund, Euro).Periods.Single();

        var t = period.Trades;
        Assert.Equal([(new DateOnly(2024, 1, 3), TradeSide.Buy, 100m), (new(2024, 2, 1), TradeSide.Buy, 100m),
            (new(2024, 2, 15), TradeSide.Sell, 240m), (new(2024, 3, 5), TradeSide.Buy, 340m)],
            t.Select(x => (x.Date, x.Side, Math.Round(x.Amount, 2))));
        Assert.Null(t[1].StepNo); // A top-up, not a signal's trade.
        Assert.Equal(300m, period.Strategy.Invested); // New money: the sale's €240 went back in outside the limit.
        Assert.Null(period.SameDays); // A pool of one: it would be the strategy itself.
    }

    record Golden(string Symbol, string[] Dates, decimal[] Close, long[]? Volume, string[] Status, int[] NewBuy, int[]? NewSell = null);

    static Golden LoadGolden(string name) =>
        JsonSerializer.Deserialize<Golden>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Golden", $"{name}.json")), JsonSerializerOptions.Web)!;

    [Theory]
    [InlineData("dip-ETN-a", "dip")]
    [InlineData("comeback-TSLA-0", "comeback")]
    [InlineData("bear-SPY-3800", "bear")]
    [InlineData("trend-SPY-0", "trend")]
    public void With_money_for_every_signal_it_trades_the_POCs_signals_at_the_next_open(string name, string template)
    {
        var golden = LoadGolden(name);
        var bars = golden.Dates.Select((d, i) => new PriceBar(DateOnly.Parse(d), golden.Close[i], golden.Close[i], golden.Close[i], golden.Close[i], golden.Volume?[i] ?? 0)).ToList();
        var asset = new TestAsset(golden.Symbol, "USD", bars);
        var (maxSteps, step) = template == "trend" ? (1, (decimal?)null) : (5, 100m);
        var rules = new TestRules(b => template switch
        {
            "dip" => new DipRules(b, new DipParams { CooldownDays = 10 }, pocStack: true),
            "comeback" => new ComebackRules(b, new ComebackParams(), pocTurn: true),
            "bear" => new BearRules(b, new BearParams()),
            _ => new TrendRules(b, new TrendParams { MaxChasePct = null }),
        }, maxSteps, step);
        var period = new BacktestPeriod(bars[0].Date.Year, bars[^1].Date.Year, bars[0].Date, bars[^1].Date, bars[0].Date, false);

        var result = BacktestSimulator.Run(rules, new TestMoney(Budget: 1_000_000, MonthlyLimit: 1_000_000), [new(period, [asset])], asset,
            (_, _) => 1).Periods.Single();

        // The engine's signals as the POC's (EngineGoldenTests), each traded at the next day's open; the comeback only up to its first
        // step held, as there (STL's later steps are a pace, the POC's milestones).
        var held = template == "comeback" ? Array.FindIndex(golden.Status, s => s is "building" or "holding") : -1;
        var upTo = held < 0 ? bars.Count : held;
        var signalled = golden.NewBuy.Select(i => (i, TradeSide.Buy)).Concat((golden.NewSell ?? []).Select(i => (i, TradeSide.Sell)))
            .Where(x => x.i < upTo && x.i + 1 < bars.Count).OrderBy(x => x.i).Select(x => (bars[x.i + 1].Date, x.Item2)).ToList();
        var traded = result.Trades.Where(t => t.SignalDate < bars[Math.Min(upTo, bars.Count - 1)].Date || upTo == bars.Count)
            .Select(t => (t.Date, t.Side)).ToList();
        Assert.NotEmpty(signalled);
        Assert.Equal(signalled, traded);
        Assert.Equal(0, result.Unpaid);
    }

    /// <summary>A random walk on weekdays, with a slow rise, so the dip rules find pullbacks.</summary>
    static List<PriceBar> Walk(int seed, DateOnly first, int count)
    {
        var random = new Random(seed);
        var bars = new List<PriceBar>();
        var price = 50m;
        for (var day = first; bars.Count < count; day = day.AddDays(1))
        {
            if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            var open = price;
            price = Math.Round(price * (decimal)(1 + 0.0012 + 0.02 * (random.NextDouble() * 2 - 1)), 4);
            bars.Add(new(day, open, Math.Max(open, price), Math.Min(open, price), price, 1000 + random.Next(2000)));
        }
        return bars;
    }

    [NeonFact]
    public async Task A_backtest_of_a_running_simulation_from_its_start_gives_the_trades_the_app_recorded()
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var first = new DateOnly(2022, 1, 3);
        // Three stocks, 7 buy signals on different days; a €500 budget at €100 a trade pays the first 5.
        var series = new[] { 4, 1, 6 }.Select(seed => (Seed: seed, Bars: Walk(seed, first, 700))).ToList();
        var assets = series.Select(s => new Asset { Symbol = $"B{s.Seed}{tag}", Exchange = "TEST", Name = "Backtest check", Currency = "USD" }).ToList();
        var user = new User { Email = $"backtest-{tag}@example.com", TimeZone = "UTC", Locale = "en" };
        db.Assets.AddRange(assets);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        for (var i = 0; i < assets.Count; i++)
            db.Prices.AddRange(series[i].Bars.Select(b => new PriceDaily
                { AssetId = assets[i].Id, Date = b.Date, Open = b.Open, High = b.High, Low = b.Low, Close = b.Close, Volume = b.Volume }));
        var start = series[0].Bars[300].Date;
        var budget = new Budget(500, 100, 5);
        var strategy = new Strategy
        {
            UserId = user.Id, TemplateKey = "dip", Name = $"Backtest {tag}", Mode = StrategyMode.Simulation, Status = StrategyStatus.Running,
            StartedOn = start, Currency = "USD", Budget = budget, Params = TemplateCatalog.Dip.ParamsJson(),
            Assets = [.. assets.Select(a => new StrategyAsset { AssetId = a.Id, Active = true, AddedAt = new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) })],
        };
        db.Strategies.Add(strategy);
        await db.SaveChangesAsync();
        foreach (var asset in assets) await StrategyEvaluatorTests.CheckedBeforeAsync(db, strategy.Id, asset.Id, start);
        try
        {
            var last = series[0].Bars[^1].Date;
            // Checked each evening, as the daily run does: one run over many days would pay them stock by stock, not by date.
            var evaluator = new StrategyEvaluator(db, new AdminAlerts(db, TimeProvider.System, NullLogger<AdminAlerts>.Instance), TimeProvider.System,
                NullLogger<StrategyEvaluator>.Instance);
            foreach (var day in series[0].Bars.Where(b => b.Date >= start).Select(b => b.Date))
                await evaluator.CheckAsync(strategy.Id, null, new DateTimeOffset(day.ToDateTime(new TimeOnly(22, 0)), TimeSpan.Zero), default);
            var symbols = assets.ToDictionary(a => a.Id, a => a.Symbol);
            var recorded = (await db.SignalTrades.Where(t => t.StrategyId == strategy.Id).Join(db.Signals, t => t.SignalId, s => s.Id,
                    (t, s) => new { t.AssetId, t.TradeDate, t.Side, t.StepNo, s.SignalDate }).ToListAsync())
                .Select(t => (t.TradeDate, symbols[t.AssetId], t.Side, t.StepNo, t.SignalDate)).Order().ToList();

            var period = new BacktestPeriod(start.Year, last.Year, start, last, first, Partial: false);
            var pool = series.Select((s, i) => new TestAsset(assets[i].Symbol, "USD", s.Bars)).ToList();
            var result = BacktestSimulator.Run(TestRules.For("dip", strategy.Params, budget), new TestMoney(500, 500, 0.2m, 0), [new(period, pool)],
                pool[0], (_, _) => 1).Periods.Single();
            var simulated = result.Trades.Select(t => (t.Date, t.Symbol, t.Side, t.StepNo, t.SignalDate!.Value)).Order().ToList();

            Assert.Equal(5, recorded.Count);
            Assert.Equal(recorded, simulated);
            Assert.Equal(2, result.Unpaid); // The app made no signal for them: the budget was used.
            Assert.Equal(5, await db.Signals.CountAsync(s => s.StrategyId == strategy.Id));
        }
        finally
        {
            await db.Users.Where(u => u.Id == user.Id).ExecuteDeleteAsync();
            var ids = assets.Select(a => a.Id).ToList();
            await db.Assets.Where(a => ids.Contains(a.Id)).ExecuteDeleteAsync();
        }
    }
}
