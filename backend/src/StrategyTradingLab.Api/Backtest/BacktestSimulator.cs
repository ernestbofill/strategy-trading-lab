using System.Text.Json;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Engine;
using StrategyTradingLab.Api.Evaluation;

namespace StrategyTradingLab.Api.Backtest;

/// <summary>One stock or fund in a test: its Yahoo symbol, currency and daily prices (adjusted), oldest first, from the warm-up on.</summary>
public record TestAsset(string Symbol, string Currency, IReadOnlyList<PriceBar> Bars);

/// <summary>
/// The rules a test runs: new engine rules for a stock's history (they keep state, so one per run), the most trades a stock may take
/// (<c>StepPlan.MaxSteps</c>) and the amount per trade, or null for all in (<i>Index trend</i>).
/// </summary>
public record TestRules(Func<IReadOnlyList<PriceBar>, IBuyRules> Create, int MaxSteps, decimal? StepAmount)
{
    /// <summary>A template's rules with a strategy's (or the template's) params, as the daily check builds them.</summary>
    public static TestRules For(string templateKey, JsonDocument parameters, Budget budget) =>
        new(bars => TemplateRules.Create(templateKey, bars, parameters), budget.MaxSteps ?? 1, budget.Step);
}

/// <summary>A period's pool: the stocks the test uses in it (the period's draft, the strategy's list, one stock or the index fund).</summary>
public record PeriodPool(BacktestPeriod Period, IReadOnlyList<TestAsset> Stocks);

/// <summary>
/// One period's results: the strategy and the baselines (docs/backtest.md, *Baselines*): 1 the index fund, monthly; 2 the same days and
/// money spread over the pool (null for a pool of one, where it would be the strategy itself); 3 the pool, monthly. <c>Unpaid</c> counts
/// the buy signals the money couldn't pay (the rules count them as bought, as the app does today); <c>SatOut</c> the stocks without
/// enough history at the start.
/// </summary>
public record BacktestPeriodResult(BacktestPeriod Period, int Stocks, LineResult Strategy, LineResult Index, LineResult? SameDays,
    LineResult SameStocks, IReadOnlyList<BacktestTrade> Trades, int Unpaid, IReadOnlyList<string> SatOut);

/// <summary>
/// <i>All</i>, the periods combined (Ernest, 2026-11-23): per line the average of the periods' yearly rates, the worst year of any
/// period, the average budget at work and the trades added up. Not one continuous test.
/// </summary>
public record AllLine(double? Rate, YearReturn? Worst, double BudgetAtWork, int Trades)
{
    public static AllLine? Of(IReadOnlyList<LineResult?> periods)
    {
        if (periods.Count == 0 || periods.Any(p => p is null)) return null;
        var lines = periods.Select(p => p!).ToList();
        var rates = lines.Where(l => l.Rate is not null).Select(l => l.Rate!.Value).ToList();
        return new(rates.Count == 0 ? null : rates.Average(), lines.Select(l => l.Worst).OfType<YearReturn>().MinBy(w => w.Return),
            lines.Average(l => l.BudgetAtWork), lines.Sum(l => l.Trades));
    }
}

public record BacktestResult(IReadOnlyList<BacktestPeriodResult> Periods, AllLine Strategy, AllLine Index, AllLine? SameDays, AllLine SameStocks);

/// <summary>
/// STL's own backtest (docs/backtest.md): the app's engine run over each period's pool, with the test's money. Each period is a fresh
/// test: the rules read the warm-up, nothing is bought before the start, each signal at the close is traded at the stock's next open,
/// and holdings are valued at the period's last close, nothing sold. The strategy and every baseline get the same money (the budget,
/// at most the monthly limit of new money in a calendar month with unspent limit carried over in full), the same fee and spread and
/// the same formulas. Pure: prices in, results out.
/// </summary>
public static class BacktestSimulator
{
    /// <summary>A stock whose prices start more than this after the warm-up's start sits the period out (the draw allows the same).</summary>
    public const int WarmUpSlackDays = 7;

    /// <param name="index">The index fund (SPY): baseline 1, and its days are the period's trading days and months.</param>
    /// <param name="fx">Units of a stock's currency for one unit of the test's, each day.</param>
    /// <param name="seed">Orders the signals of a day when money is short (<see cref="DraftDraw.DefaultSeed"/>).</param>
    public static BacktestResult Run(TestRules rules, TestMoney money, IReadOnlyList<PeriodPool> periods, TestAsset index, FxRate fx,
        ulong seed = DraftDraw.DefaultSeed)
    {
        var results = periods.Select(p => RunPeriod(rules, money, p, index, fx, seed)).ToList();
        return new(results, AllLine.Of([.. results.Select(r => r.Strategy)])!, AllLine.Of([.. results.Select(r => r.Index)])!,
            AllLine.Of([.. results.Select(r => r.SameDays)]), AllLine.Of([.. results.Select(r => r.SameStocks)])!);
    }

    /// <summary>A signal and the day it trades: the next bar after its day, within the period.</summary>
    sealed record Signal(TestAsset Stock, DateOnly Day, TradeSide Side, int? StepNo, PriceBar Fill);

    static BacktestPeriodResult RunPeriod(TestRules rules, TestMoney money, PeriodPool pool, TestAsset index, FxRate fx, ulong seed)
    {
        var period = pool.Period;
        var days = index.Bars.Where(b => b.Date >= period.Start && b.Date <= period.End).Select(b => b.Date).ToList();
        var tradingDays = days.ToHashSet();

        // Each stock's rules over its own history, as a simulation started on the period's first day: every signal is acted on by the
        // rules, paid or not (today's rule for a signal with no money, docs/backtest.md, *Money*).
        var stocks = new List<TestAsset>();
        var satOut = new List<string>();
        var signals = new List<Signal>();
        foreach (var stock in pool.Stocks)
        {
            var bars = stock.Bars.Where(b => b.Date <= period.End).ToList();
            if (bars.Count == 0 || bars[0].Date > period.WarmUpStart.AddDays(WarmUpSlackDays) || bars[^1].Date < period.Start)
            {
                satOut.Add(stock.Symbol);
                continue;
            }
            var cut = stock with { Bars = bars };
            stocks.Add(cut);
            var evaluated = RulesRunner.Run(rules.Create(bars), bars, new StepPlan(rules.MaxSteps), RulesRunner.StartIndex(bars, period.Start));
            var offset = bars.Count - evaluated.Count;
            for (var i = 0; i < evaluated.Count; i++)
            {
                var day = evaluated[i];
                // The last day's signal trades after the period: the rules still count it, but nothing is bought.
                if (offset + i + 1 >= bars.Count) continue;
                var fill = bars[offset + i + 1];
                if (day.NewBuy) signals.Add(new(cut, day.Date, TradeSide.Buy, day.StepNo, fill));
                if (day.NewSell) signals.Add(new(cut, day.Date, TradeSide.Sell, null, fill));
            }
        }

        var strategy = new BacktestLine(money, fx);
        var unpaid = 0;
        // The new money the limit allows by the end of a day's month: m × the limit by the end of month m, at most the budget.
        decimal Allowed(DateOnly day) =>
            Math.Min(money.Budget, ((day.Year - period.Start.Year) * 12 + day.Month - period.Start.Month + 1) * money.MonthlyLimit);
        const decimal Cent = 0.01m;

        // Buys decided on their signal's day, made at the next open; sales the same. Proceeds wait until a buy takes them.
        var pending = new List<(Signal Signal, decimal Amount, decimal FromIdle)>();
        // New money decided but not traded yet (bought at the next open).
        decimal Reserved() => pending.Sum(p => p.Amount - p.FromIdle);
        var bySignalDay = signals.GroupBy(s => s.Day).ToDictionary(g => g.Key, g => g.ToList());
        var timeline = signals.Select(s => s.Day).Concat(signals.Select(s => s.Fill.Date)).Concat(days).Distinct().Order().ToList();
        var lastTopUpMonth = (Year: 0, Month: 0);
        foreach (var today in timeline)
        {
            // 1) The trades of today's open: sales first, so their proceeds are known before today's close decides any buy.
            foreach (var (signal, amount, fromIdle) in pending.Where(p => p.Signal.Fill.Date == today).OrderBy(p => p.Signal.Side == TradeSide.Buy).ToList())
            {
                if (signal.Side == TradeSide.Sell) strategy.SellAll(signal.Stock.Symbol, signal.Fill, signal.Day);
                else strategy.Buy(signal.Stock, signal.Fill, amount, signal.StepNo, rules.StepAmount is null ? null : rules.MaxSteps, signal.Day, fromIdle);
            }
            pending.RemoveAll(p => p.Signal.Fill.Date == today);

            // 2) All in under a monthly limit (Index trend): a held fund takes each new month's limit at the month's first open, so a
            // limit below the budget still puts the budget to work. Not while a sale is pending.
            if (rules.StepAmount is null && (today.Year, today.Month) != lastTopUpMonth && tradingDays.Contains(today))
            {
                lastTopUpMonth = (today.Year, today.Month);
                foreach (var stock in stocks.Where(s => strategy.Holds(s.Symbol) && !pending.Any(p => p.Signal.Stock.Symbol == s.Symbol && p.Signal.Side == TradeSide.Sell)))
                {
                    var room = Allowed(today) - strategy.NewMoney - Reserved();
                    var at = Bars.FirstOnOrAfter(stock.Bars, today);
                    if (room >= Cent && at < stock.Bars.Count && stock.Bars[at].Date == today) strategy.Buy(stock, stock.Bars[at], room);
                }
            }

            // 3) Today's signals, paid in a seeded order (the same seed, the same result, no alphabetical bias).
            if (!bySignalDay.TryGetValue(today, out var todays)) continue;
            foreach (var signal in DraftDraw.Shuffle([.. todays.OrderBy(s => s.Stock.Symbol, StringComparer.Ordinal).ThenBy(s => s.Side)],
                seed, $"pay {today:yyyy-MM-dd}"))
            {
                if (signal.Side == TradeSide.Sell)
                {
                    pending.Add((signal, 0, 0));
                    continue;
                }
                var room = Allowed(today) - strategy.NewMoney - Reserved();
                if (rules.StepAmount is { } step)
                {
                    // A trade that doesn't fit is not made, and no part of it: the app makes no signal (DATA-MODEL: strategies).
                    if (step > room + Cent / 2) { unpaid++; continue; }
                    pending.Add((signal, step, 0));
                }
                else
                {
                    // All in: the room the limit leaves, plus any sale proceeds (reinvested outside the limit).
                    var idle = Math.Max(0, strategy.Idle - pending.Sum(p => p.FromIdle));
                    var amount = Math.Max(0, room) + idle;
                    if (amount < Cent) { unpaid++; continue; }
                    pending.Add((signal, amount, idle));
                }
            }
        }

        var trades = strategy.Trades;
        var indexLine = Monthly(money, fx, period, days, [index with { Bars = [.. index.Bars.Where(b => b.Date <= period.End)] }]);
        var sameStocks = Monthly(money, fx, period, days, stocks);
        BacktestLine? sameDays = null;
        if (stocks.Count > 1)
        {
            // Baseline 2: each of the strategy's buys, the same amount on the same day, split over the pool's stocks that trade that day.
            sameDays = new BacktestLine(money, fx);
            foreach (var buy in trades.Where(t => t.Side == TradeSide.Buy))
            {
                var priced = stocks.Select(s => (Stock: s, Bar: s.Bars.FirstOrDefault(b => b.Date == buy.Date))).Where(x => x.Bar is not null).ToList();
                foreach (var (stock, bar) in priced) sameDays.Buy(stock, bar!, buy.Amount / priced.Count);
            }
        }
        return new(period, stocks.Count, strategy.Result(period, days), indexLine.Result(period, days), sameDays?.Result(period, days),
            sameStocks.Result(period, days), trades, unpaid, satOut);
    }

    /// <summary>
    /// Baselines 1 and 3: on the first trading day of each month, the month's limit split over the stocks until the budget is used
    /// (<see cref="SameStocksMonthly.Plan"/>); a stock that doesn't trade that day buys at its next open in the month.
    /// </summary>
    static BacktestLine Monthly(TestMoney money, FxRate fx, BacktestPeriod period, IReadOnlyList<DateOnly> days, IReadOnlyList<TestAsset> stocks)
    {
        var line = new BacktestLine(money, fx);
        var buyDays = SameStocksMonthly.BuyDays(days, period, money);
        var prices = stocks.Select(s => PeriodPrices.From(s.Bars, buyDays, period.End, sameMonth: true)).ToList();
        foreach (var (day, i, amount) in SameStocksMonthly.Plan(prices, buyDays, money).OrderBy(p => p.Day))
        {
            var bars = stocks[i].Bars;
            line.Buy(stocks[i], bars[Bars.FirstOnOrAfter(bars, day)], amount);
        }
        return line;
    }
}
