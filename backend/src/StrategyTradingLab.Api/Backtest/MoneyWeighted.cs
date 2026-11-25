using StrategyTradingLab.Api.Engine;

namespace StrategyTradingLab.Api.Backtest;

/// <summary>The test's money (docs/backtest.md, *What is tested*): the budget, the monthly limit and the costs of every trade.</summary>
public record TestMoney(decimal Budget = 6000, decimal MonthlyLimit = 400, decimal FeePct = 0.1m, decimal SpreadPct = 0.1m)
{
    /// <summary>The months a line that spends the whole limit each month takes to use the budget: 15 with the defaults.</summary>
    public int Months => (int)Math.Ceiling(Budget / MonthlyLimit);

    /// <summary>The shares an amount buys at a price: the fee comes out of the amount, the spread is added to the price.</summary>
    public decimal SharesFor(decimal amount, decimal price) => amount / (1 + FeePct / 100) / (price * (1 + SpreadPct / 100));
}

/// <summary>Money-weighted returns (IRR): each euro counts for as long as it was invested.</summary>
public static class MoneyWeighted
{
    /// <summary>
    /// The yearly rate that brings the cash flows to zero (XIRR, a 365-day year): money put in is negative, the value at the end
    /// positive. Null without both, or when no rate between −99.99% and +1,000% a year fits.
    /// </summary>
    public static double? AnnualRate(IReadOnlyList<(DateOnly Date, decimal Amount)> flows)
    {
        if (!flows.Any(f => f.Amount < 0) || !flows.Any(f => f.Amount > 0)) return null;
        var start = flows.Min(f => f.Date);
        var points = flows.Select(f => (Years: (f.Date.DayNumber - start.DayNumber) / 365.0, Amount: (double)f.Amount)).ToList();
        double Value(double rate) => points.Sum(p => p.Amount / Math.Pow(1 + rate, p.Years));
        double low = -0.9999, high = 10;
        var atLow = Value(low);
        if (Math.Sign(atLow) == Math.Sign(Value(high))) return null;
        for (var i = 0; i < 200; i++)
        {
            var mid = (low + high) / 2;
            var atMid = Value(mid);
            if (Math.Sign(atMid) == Math.Sign(atLow)) (low, atLow) = (mid, atMid);
            else high = mid;
        }
        return (low + high) / 2;
    }
}

/// <summary>
/// What a backtest needs of one stock's prices in a period, for a line that buys on known days and never sells: the opens on those
/// days and the last close on or before the period's end. Small, so the draw can hold it for every member.
/// </summary>
public sealed class PeriodPrices(IReadOnlyDictionary<DateOnly, decimal> opens, decimal? endClose)
{
    public decimal? OpenOn(DateOnly day) => opens.TryGetValue(day, out var open) ? open : null;
    public decimal? EndClose => endClose;

    public static PeriodPrices From(IReadOnlyList<PriceBar> bars, IEnumerable<DateOnly> buyDays, DateOnly end)
    {
        var days = buyDays.ToHashSet();
        var opens = bars.Where(b => days.Contains(b.Date)).ToDictionary(b => b.Date, b => b.Open);
        var last = bars.LastOrDefault(b => b.Date <= end);
        return new(opens, last?.Close);
    }
}

/// <summary>
/// Baseline 3, <i>same stocks, monthly</i> (docs/backtest.md, *Baselines*): on the first trading day of each month it buys the whole pool,
/// split evenly, with the month's limit, until the budget is used; never sells; valued at the period's last close. For the draw's
/// representative check it runs in the stocks' currency (dollars): both pools buy on the same days, so the exchange rate moves them
/// alike. The simulator (ROADMAP item 53) runs it in the strategy's currency.
/// </summary>
public static class SameStocksMonthly
{
    /// <summary>The first trading day of each month from the period's start, as many months as the budget lasts.</summary>
    public static IReadOnlyList<DateOnly> BuyDays(IReadOnlyList<DateOnly> calendar, BacktestPeriod period, TestMoney money) =>
        [.. calendar.Where(d => d >= period.Start && d <= period.End).GroupBy(d => (d.Year, d.Month)).Select(g => g.Min()).Take(money.Months)];

    /// <summary>
    /// The money-weighted yearly rate. A month's money is split among the stocks with a price that day; a stock whose prices stop
    /// before the end is valued at its last close. Null when nothing could be bought.
    /// </summary>
    public static double? Rate(IReadOnlyList<PeriodPrices> pool, IReadOnlyList<DateOnly> buyDays, BacktestPeriod period, TestMoney money)
    {
        var shares = new decimal[pool.Count];
        var flows = new List<(DateOnly, decimal)>();
        var spent = 0m;
        foreach (var day in buyDays)
        {
            var amount = Math.Min(money.MonthlyLimit, money.Budget - spent);
            var priced = Enumerable.Range(0, pool.Count).Where(i => pool[i].OpenOn(day) is not null && pool[i].EndClose is not null).ToList();
            if (amount <= 0 || priced.Count == 0) continue;
            foreach (var i in priced) shares[i] += money.SharesFor(amount / priced.Count, pool[i].OpenOn(day)!.Value);
            flows.Add((day, -amount));
            spent += amount;
        }
        var value = Enumerable.Range(0, pool.Count).Sum(i => shares[i] * (pool[i].EndClose ?? 0));
        flows.Add((period.End, value));
        return MoneyWeighted.AnnualRate(flows);
    }
}
