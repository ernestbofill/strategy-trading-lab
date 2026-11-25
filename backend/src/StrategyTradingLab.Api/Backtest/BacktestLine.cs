using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Engine;

namespace StrategyTradingLab.Api.Backtest;

/// <summary>
/// One trade of a line, in the test's currency: <c>Amount</c> is the money that went in (a buy, fee and spread included) or came back
/// (a sale, net of them); <c>Costs</c> are the fee and the spread. <c>Price</c> is the open, in the stock's currency, before the spread.
/// <c>StepNo</c> of <c>MaxSteps</c> is the trade's number ("2 of 5"); a top-up (<i>Index trend</i> under a monthly limit) has none.
/// </summary>
public record BacktestTrade(DateOnly Date, string Symbol, TradeSide Side, int? StepNo, int? MaxSteps, decimal Price, decimal Shares,
    decimal Amount, decimal Costs, decimal FxRate, DateOnly? SignalDate);

/// <summary>A calendar year's money-weighted return (a fraction), over the part of the year the money was in.</summary>
public record YearReturn(int Year, double Return);

/// <summary>Where a line stood at a month's end: the money in its holdings (their cost) and their value.</summary>
public record ValuePoint(DateOnly Date, decimal Invested, decimal Value);

/// <summary>
/// One line's results in one period (docs/backtest.md, *Results*), all money-weighted: the yearly rate on the money invested (IRR), each
/// calendar year and the worst, the budget at work (0–1), the trades, the new money put in and what the line is worth at the end
/// (holdings at the last close, plus sale proceeds not reinvested).
/// </summary>
public record LineResult(double? Rate, IReadOnlyList<YearReturn> Years, double BudgetAtWork, int Trades, decimal Invested, decimal EndValue,
    IReadOnlyList<ValuePoint> Values)
{
    public YearReturn? Worst => Years.Count == 0 ? null : Years.MinBy(y => y.Return);
}

/// <summary>Converts the test's currency to a stock's: units of <paramref name="currency"/> for one unit of the test's on that day (1 when the same).</summary>
public delegate decimal FxRate(string currency, DateOnly day);

/// <summary>
/// The book of one line of a test (the strategy or a baseline): what it holds, its cash flows and its trades, kept in the test's
/// currency. The strategy and every baseline keep the same book, so they are measured with the same formulas.
/// </summary>
public sealed class BacktestLine(TestMoney money, FxRate fx)
{
    sealed class Holding(TestAsset asset)
    {
        public TestAsset Asset { get; } = asset;
        public decimal Shares, Cost;
    }

    readonly Dictionary<string, Holding> holdings = [];
    readonly Dictionary<string, TestAsset> traded = [];
    readonly List<(DateOnly Date, decimal Amount)> flows = [];
    readonly List<BacktestTrade> trades = [];

    public IReadOnlyList<BacktestTrade> Trades => trades;

    /// <summary>Sale proceeds not reinvested yet: money outside the holdings, which the rate leaves out until it goes back in.</summary>
    public decimal Idle { get; private set; }

    /// <summary>The budget's money put in so far (sale proceeds going back in aren't new money).</summary>
    public decimal NewMoney { get; private set; }

    public bool Holds(string symbol) => holdings.TryGetValue(symbol, out var h) && h.Shares > 0;

    /// <summary>
    /// Buys <paramref name="amount"/> of the stock at the open of <paramref name="bar"/>, fee and spread included. <paramref name="fromIdle"/>
    /// of it is sale proceeds going back in; the rest is new money from the budget.
    /// </summary>
    public BacktestTrade Buy(TestAsset asset, PriceBar bar, decimal amount, int? stepNo = null, int? maxSteps = null, DateOnly? signalDate = null,
        decimal fromIdle = 0)
    {
        var rate = fx(asset.Currency, bar.Date);
        var shares = money.SharesFor(amount * rate, bar.Open);
        if (!holdings.TryGetValue(asset.Symbol, out var holding)) holdings[asset.Symbol] = holding = new(asset);
        traded[asset.Symbol] = asset;
        holding.Shares += shares;
        holding.Cost += amount;
        Idle -= fromIdle;
        NewMoney += amount - fromIdle;
        flows.Add((bar.Date, -amount));
        var trade = new BacktestTrade(bar.Date, asset.Symbol, TradeSide.Buy, stepNo, maxSteps, bar.Open, shares, amount,
            amount - shares * bar.Open / rate, rate, signalDate);
        trades.Add(trade);
        return trade;
    }

    /// <summary>Sells all of the stock at the open of <paramref name="bar"/>; the proceeds wait in <see cref="Idle"/>. Null when nothing is held.</summary>
    public BacktestTrade? SellAll(string symbol, PriceBar bar, DateOnly? signalDate = null)
    {
        if (!holdings.TryGetValue(symbol, out var holding) || holding.Shares <= 0) return null;
        var rate = fx(holding.Asset.Currency, bar.Date);
        var proceeds = money.ProceedsOf(holding.Shares, bar.Open) / rate;
        var trade = new BacktestTrade(bar.Date, symbol, TradeSide.Sell, null, null, bar.Open, holding.Shares, proceeds,
            holding.Shares * bar.Open / rate - proceeds, rate, signalDate);
        holdings.Remove(symbol);
        Idle += proceeds;
        flows.Add((bar.Date, proceeds));
        trades.Add(trade);
        return trade;
    }

    /// <summary>The holdings' value at the last close on or before <paramref name="day"/>, in the test's currency.</summary>
    decimal ValueOn(DateOnly day) => holdings.Values.Sum(h => Bars.LastOnOrBefore(h.Asset.Bars, day) is { } bar ? h.Shares * bar.Close / fx(h.Asset.Currency, bar.Date) : 0);

    decimal Cost => holdings.Values.Sum(h => h.Cost);

    /// <summary>
    /// The line's results over the period, nothing sold at its end. <paramref name="days"/> are the period's trading days (the index's),
    /// over which the budget at work is averaged. Call once the period's trades are in.
    /// </summary>
    public LineResult Result(BacktestPeriod period, IReadOnlyList<DateOnly> days)
    {
        // The book as it stood on each day: its trades replayed by date into a fresh one (a baseline's stock that didn't trade on the
        // month's buy day fills later in the month, after another stock's buy of a later day may be in).
        var ordered = trades.OrderBy(t => t.Date).ToList();
        var replay = new BacktestLine(money, fx);
        var next = 0;
        void ReplayTo(DateOnly day)
        {
            for (; next < ordered.Count && ordered[next].Date <= day; next++)
            {
                var t = ordered[next];
                if (t.Side == TradeSide.Sell) { replay.holdings.Remove(t.Symbol); continue; }
                if (!replay.holdings.TryGetValue(t.Symbol, out var h)) replay.holdings[t.Symbol] = h = new(traded[t.Symbol]);
                (h.Shares, h.Cost) = (h.Shares + t.Shares, h.Cost + t.Amount);
            }
        }

        // The budget at work: (1 / N) × Σ min(1, invested on day t / budget), invested being the holdings' cost (docs/backtest.md).
        var atWork = 0.0;
        foreach (var day in days)
        {
            ReplayTo(day);
            atWork += Math.Min(1, (double)(replay.Cost / money.Budget));
        }
        atWork = days.Count == 0 ? 0 : atWork / days.Count;

        replay = new BacktestLine(money, fx);
        next = 0;
        var years = new List<YearReturn>();
        var values = new List<ValuePoint>();
        for (var year = period.Start.Year; year <= period.End.Year; year++)
        {
            var from = new DateOnly(year, 1, 1) < period.Start ? period.Start : new DateOnly(year, 1, 1);
            var to = new DateOnly(year, 12, 31) > period.End ? period.End : new DateOnly(year, 12, 31);
            ReplayTo(from.AddDays(-1));
            // Holdings carried into the year count as put in on its first day, at their value then.
            var carried = replay.ValueOn(from.AddDays(-1));
            // The year's clock runs only while the line holds something (after each day's trades at the open): a fund sold in February
            // and not bought again had its year from January to February, and money put in during December earns a few days.
            var heldBefore = new Dictionary<DateOnly, int>();
            var held = 0;
            for (var day = from; day <= to; day = day.AddDays(1))
            {
                heldBefore[day] = held;
                ReplayTo(day);
                if (replay.holdings.Count > 0) held++;
                if (day.Day == DateTime.DaysInMonth(day.Year, day.Month) || day == to)
                    values.Add(new(day, Math.Round(replay.Cost, 2), Math.Round(replay.ValueOn(day), 2)));
            }
            if (held == 0) continue;
            var points = new List<(double Time, decimal Amount)>();
            if (carried > 0) points.Add((0, -carried));
            points.AddRange(flows.Where(f => f.Date >= from && f.Date <= to).Select(f => ((double)heldBefore[f.Date] / held, f.Amount)));
            points.Add((1, replay.ValueOn(to)));
            // The rate per unit of time held is the year's own return, not a yearly rate.
            if (MoneyWeighted.Rate(points) is { } yearReturn) years.Add(new(year, yearReturn));
        }

        var endValue = ValueOn(period.End);
        return new(MoneyWeighted.AnnualRate([.. flows, (period.End, endValue)]), years, atWork, trades.Count, Math.Round(NewMoney, 2),
            Math.Round(endValue + Idle, 2), values);
    }
}
