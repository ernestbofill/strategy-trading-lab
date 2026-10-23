using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Engine;

/// <summary>
/// Dip in winners (POC Leader pullback, <c>stage2_pullback</c>). A candidate is a proven leader: it rose at least
/// <c>MinGainPct</c> from its low and its averages are stacked (SMA 50 &gt; SMA 150 &gt; SMA 200) with the SMA 200 rising.
/// The buy window is the discount zone, <c>ZoneStartPct</c>–<c>ZoneEndPct</c> below the recent peak and above the SMA 50.
/// </summary>
public class DipRules : IBuyRules
{
    const int MiddleDays = 150, RisingDays = 20, MinimumHistory = 220;

    readonly DipParams p;
    readonly IReadOnlyList<decimal> close;
    readonly decimal?[] floor, middle, trend, low, high;
    readonly bool[] trendRising;

    public DipRules(IReadOnlyList<PriceBar> bars, DipParams parameters)
    {
        p = parameters;
        close = [.. bars.Select(b => b.Close)];
        floor = Indicators.Sma(close, p.FloorDays);
        middle = Indicators.Sma(close, MiddleDays);
        trend = Indicators.Sma(close, p.TrendDays);
        low = Indicators.RollingLow(close, p.GainDays);
        high = Indicators.RollingHigh(close, p.PeakDays);
        trendRising = Indicators.Rising(trend, RisingDays);
    }

    public int MinHistory => Math.Max(MinimumHistory, p.GainDays);

    public int CooldownDays => p.CooldownDays;

    public RuleDay At(int i)
    {
        if (floor[i] is not { } sma50 || middle[i] is not { } sma150 || trend[i] is not { } sma200
            || low[i] is not { } lowest || high[i] is not { } peak)
            return new RuleDay(false, false, false, "", "", new Dictionary<string, decimal>());

        var price = close[i];
        var gain = Indicators.PctFrom(peak, lowest);
        var pullback = Indicators.PctFrom(price, peak);
        var aligned = sma50 > sma150 && sma150 > sma200;
        var candidate = gain >= p.MinGainPct && aligned && trendRising[i];
        var zoneHigh = peak * (1 - p.ZoneStartPct / 100);
        var zoneLow = Math.Max(peak * (1 - p.ZoneEndPct / 100), sma50);
        var inZone = zoneLow <= price && price <= zoneHigh && zoneLow < zoneHigh;

        var lost = price > zoneHigh
            ? "Price bounced back toward the recent high: the dip is over."
            : "Price fell through the buy zone (below the zone or the SMA 50).";
        var facts = new Dictionary<string, decimal>
        {
            ["gainPct"] = Math.Round(gain, 2),
            ["belowPeakPct"] = Math.Round(-pullback, 2),
            ["peak"] = Math.Round(peak, 4),
            ["sma50"] = Math.Round(sma50, 4),
            ["zoneLow"] = Math.Round(zoneLow, 4),
            ["zoneHigh"] = Math.Round(zoneHigh, 4),
        };
        return new RuleDay(candidate, inZone, Extended: false, lost, "", facts);
    }
}
