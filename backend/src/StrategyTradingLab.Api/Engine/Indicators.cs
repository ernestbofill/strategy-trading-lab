namespace StrategyTradingLab.Api.Engine;

/// <summary>
/// Trailing indicators over a series: the value on day t only uses days up to t, so a whole history can be computed once
/// without leaking future prices into past decisions (the POC's rule). A value is null until its window is full.
/// </summary>
public static class Indicators
{
    /// <summary>The plain average of the last <paramref name="days"/> values.</summary>
    public static decimal?[] Sma(IReadOnlyList<decimal> values, int days)
    {
        var result = new decimal?[values.Count];
        decimal sum = 0;
        for (var i = 0; i < values.Count; i++)
        {
            sum += values[i];
            if (i >= days) sum -= values[i - days];
            if (i >= days - 1) result[i] = sum / days;
        }
        return result;
    }

    /// <summary>The highest value of the last <paramref name="days"/> days, today included.</summary>
    public static decimal?[] RollingHigh(IReadOnlyList<decimal> values, int days) => Rolling(values, days, (a, b) => a > b ? a : b);

    /// <summary>The lowest value of the last <paramref name="days"/> days, today included.</summary>
    public static decimal?[] RollingLow(IReadOnlyList<decimal> values, int days) => Rolling(values, days, (a, b) => a < b ? a : b);

    /// <summary>How far <paramref name="value"/> is above (+) or below (−) <paramref name="reference"/>, in percent.</summary>
    public static decimal PctFrom(decimal value, decimal reference) => (value / reference - 1) * 100;

    /// <summary>True when the value today is higher than <paramref name="days"/> days ago (a rising average). False without both.</summary>
    public static bool[] Rising(decimal?[] values, int days)
    {
        var result = new bool[values.Length];
        for (var i = days; i < values.Length; i++)
            result[i] = values[i] is { } today && values[i - days] is { } before && today > before;
        return result;
    }

    static decimal?[] Rolling(IReadOnlyList<decimal> values, int days, Func<decimal, decimal, decimal> pick)
    {
        var result = new decimal?[values.Count];
        for (var i = days - 1; i < values.Count; i++)
        {
            var best = values[i];
            for (var j = i - days + 1; j < i; j++) best = pick(best, values[j]);
            result[i] = best;
        }
        return result;
    }
}
