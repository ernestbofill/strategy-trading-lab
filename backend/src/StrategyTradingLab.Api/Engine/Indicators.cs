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
    public static decimal?[] RollingHigh(IReadOnlyList<decimal> values, int days) => Rolling(values, days, (kept, today) => kept > today);

    /// <summary>The lowest value of the last <paramref name="days"/> days, today included.</summary>
    public static decimal?[] RollingLow(IReadOnlyList<decimal> values, int days) => Rolling(values, days, (kept, today) => kept < today);

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

    /// <summary>The best value of each window, in one pass: a deque keeps the candidates, the best first (O(n)).</summary>
    static decimal?[] Rolling(IReadOnlyList<decimal> values, int days, Func<decimal, decimal, bool> better)
    {
        var result = new decimal?[values.Count];
        var candidates = new LinkedList<int>();
        for (var i = 0; i < values.Count; i++)
        {
            while (candidates.Last is { } last && !better(values[last.Value], values[i])) candidates.RemoveLast();
            candidates.AddLast(i);
            if (candidates.First!.Value <= i - days) candidates.RemoveFirst();
            if (i >= days - 1) result[i] = values[candidates.First.Value];
        }
        return result;
    }
}
