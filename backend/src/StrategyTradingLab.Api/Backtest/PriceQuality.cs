using StrategyTradingLab.Api.Engine;

namespace StrategyTradingLab.Api.Backtest;

public enum PriceWarningKind { Gap, Jump, Stops }

/// <summary>Something in a stock's prices to look at before a result counts, with its day and the numbers.</summary>
public record PriceWarning(string Symbol, PriceWarningKind Kind, DateOnly Date, string Detail);

/// <summary>The quality report's checks on one price history (docs/backtest.md, *Data*).</summary>
public static class PriceQuality
{
    /// <summary>More trading days than this without a price is a gap.</summary>
    public const int MaxGap = 5;

    /// <summary>A one-day move past this (40%) is usually a split Yahoo didn't adjust.</summary>
    public const decimal MaxMove = 0.40m;

    /// <summary>
    /// Gaps of more than 5 trading days, one-day moves over 40%, and prices that stop more than 5 trading days before the calendar's
    /// last day. <paramref name="calendar"/> is the US trading days (SPY's), oldest first; only the days from the stock's first
    /// price on are checked.
    /// </summary>
    public static IReadOnlyList<PriceWarning> Check(string symbol, IReadOnlyList<PriceBar> bars, IReadOnlyList<DateOnly> calendar)
    {
        var warnings = new List<PriceWarning>();
        if (bars.Count == 0 || calendar.Count == 0) return warnings;
        int TradingDaysBetween(DateOnly from, DateOnly to) => Index(calendar, to) - Index(calendar, from) - 1;
        for (var i = 1; i < bars.Count; i++)
        {
            var missing = TradingDaysBetween(bars[i - 1].Date, bars[i].Date);
            if (missing > MaxGap)
                warnings.Add(new(symbol, PriceWarningKind.Gap, bars[i].Date, $"{missing} trading days without a price before it"));
            var move = bars[i].Close / bars[i - 1].Close - 1;
            if (Math.Abs(move) > MaxMove)
                warnings.Add(new(symbol, PriceWarningKind.Jump, bars[i].Date, $"{move * 100:+0;−0}% in a day"));
        }
        var after = TradingDaysBetween(bars[^1].Date, calendar[^1]) + 1;
        if (bars[^1].Date < calendar[^1] && after > MaxGap)
            warnings.Add(new(symbol, PriceWarningKind.Stops, bars[^1].Date, $"no price in the last {after} trading days"));
        return warnings;
    }

    /// <summary>The number of calendar days on or before <paramref name="day"/>: its index when it is a trading day.</summary>
    static int Index(IReadOnlyList<DateOnly> calendar, DateOnly day)
    {
        int low = 0, high = calendar.Count;
        while (low < high)
        {
            var mid = (low + high) / 2;
            if (calendar[mid] <= day) low = mid + 1;
            else high = mid;
        }
        return low - 1;
    }
}
