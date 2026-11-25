namespace StrategyTradingLab.Api.Backtest;

/// <summary>
/// One period of a backtest: calendar years, an independent test with a fresh budget. The rules read prices from
/// <paramref name="WarmUpStart"/> on (the SMA 200, the 52-week high and the 168-day low need about 15 months), but nothing is bought
/// before <paramref name="Start"/>. The latest period ends at the last close, so it can be shorter (<paramref name="Partial"/>).
/// </summary>
public record BacktestPeriod(int FirstYear, int LastYear, DateOnly Start, DateOnly End, DateOnly WarmUpStart, bool Partial)
{
    /// <summary>"2011–14".</summary>
    public string Label => $"{FirstYear}–{LastYear % 100:D2}";
}

/// <summary>The periods, counted back from today in calendar years (docs/backtest.md, *Periods*).</summary>
public static class BacktestPeriods
{
    public const int DefaultYears = 16, DefaultLength = 4, WarmUpMonths = 15;

    /// <summary>A stock whose prices start more than this after a warm-up's start can't be tested in that period (nor drawn).</summary>
    public const int WarmUpSlackDays = 7;

    /// <summary>
    /// <paramref name="years"/> calendar years up to this one, in periods of <paramref name="length"/> years: with the defaults in 2026,
    /// 2011–14, 2015–18, 2019–22 and 2023–26 (to <paramref name="today"/>).
    /// </summary>
    public static IReadOnlyList<BacktestPeriod> For(DateOnly today, int years = DefaultYears, int length = DefaultLength)
    {
        if (length < 1 || years < length || years % length != 0)
            throw new ArgumentException($"{years} years can't be split into {length}-year periods.", nameof(years));
        var first = today.Year - years + 1;
        return [.. Enumerable.Range(0, years / length).Select(n =>
        {
            var from = first + n * length;
            var to = from + length - 1;
            var start = new DateOnly(from, 1, 1);
            var end = new DateOnly(to, 12, 31);
            return new BacktestPeriod(from, to, start, end < today ? end : today, start.AddMonths(-WarmUpMonths), end > today);
        })];
    }
}
