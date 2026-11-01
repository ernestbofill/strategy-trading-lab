using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Engine;

/// <summary>
/// Winners at a discount (POC Leader pullback, <c>stage2_pullback</c>). A candidate is a proven leader: it rose at least
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
    readonly DateOnly?[] candidateSince;
    readonly BuyWindow window = new();

    public DipRules(IReadOnlyList<PriceBar> bars, DipParams parameters)
    {
        p = parameters;
        close = [.. bars.Select(b => b.Close)];
        floor = Indicators.Sma(close, p.FloorDays);
        middle = Indicators.Sma(close, MiddleDaysFor(p));
        trend = Indicators.Sma(close, p.TrendDays);
        low = Indicators.RollingLow(close, p.GainDays);
        high = Indicators.RollingHigh(close, p.PeakDays);
        trendRising = Indicators.Rising(trend, RisingDays);
        candidateSince = CandidateDates(bars);
    }

    DateOnly?[] CandidateDates(IReadOnlyList<PriceBar> bars)
    {
        var result = new DateOnly?[close.Count];
        DateOnly? since = null;
        for (var i = 0; i < close.Count; i++)
        {
            var isCandidate = floor[i] is { } sma50 && middle[i] is { } sma150 && trend[i] is { } sma200
                && low[i] is { } lowest && high[i] is { } peak
                && Indicators.PctFrom(peak, lowest) >= p.MinGainPct
                && sma50 > sma150 && sma150 > sma200
                && trendRising[i];
            since = isCandidate ? (since ?? bars[i].Date) : null;
            result[i] = since;
        }
        return result;
    }

    /// <summary>
    /// The middle average of the stack (SMA 50 &gt; SMA 150 &gt; SMA 200 in the POC), kept strictly between the floor and the
    /// trend average when either is changed. With no room between them (equal days) it is the floor's, so nothing stacks.
    /// </summary>
    static int MiddleDaysFor(DipParams p)
    {
        var lowest = p.FloorDays + 1;
        var highest = p.TrendDays - 1;
        return lowest <= highest ? Math.Clamp(MiddleDays, lowest, highest) : p.FloorDays;
    }

    public int Length => close.Count;

    /// <summary>The POC's 220 days, and more when a window needs more: the trend average and its rise, the low, the peak.</summary>
    public int MinHistory => new[] { MinimumHistory, p.GainDays, p.TrendDays + RisingDays, p.PeakDays, MiddleDaysFor(p) }.Max();

    public int CooldownDays => p.CooldownDays;

    int lastDecided = -1;

    public RuleDecision Decide(int i, DayContext context)
    {
        if (i <= lastDecided) throw new InvalidOperationException("These rules keep state from day to day and were already run: build new ones for each run.");
        lastDecided = i;
        if (floor[i] is not { } sma50 || middle[i] is not { } sma150 || trend[i] is not { } sma200
            || low[i] is not { } lowest || high[i] is not { } peak)
        {
            var (none, noneReason) = window.Compose(context, candidate: false, entryOk: false, extended: false, "", "");
            return new RuleDecision(none, noneReason, new Dictionary<string, object>());
        }

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
        var (status, reason) = window.Compose(context, candidate, inZone, extended: false, lost, "");
        var zoneBottom = peak * (1 - p.ZoneEndPct / 100);
        var sma200Ago = i >= RisingDays && trend[i - RisingDays] is { } prev ? prev : sma200;
        var risePct = Math.Round(Indicators.PctFrom(sma200, sma200Ago), 1);
        var facts = new Dictionary<string, object>
        {
            ["gainPct"] = Math.Round(gain, 2),
            ["belowPeakPct"] = Math.Round(-pullback, 2),
            ["peak"] = Math.Round(peak, 4),
            ["sma50"] = Math.Round(sma50, 4),
            ["zoneLow"] = Math.Round(zoneLow, 4),
            ["zoneHigh"] = Math.Round(zoneHigh, 4),
            // How far the price still has to fall to reach the zone (0 inside or below it): "close to the line".
            ["distancePct"] = Math.Round(Math.Max(0, Indicators.PctFrom(price, zoneHigh)), 2),
        };
        if (candidate && candidateSince[i] is { } sinceDate) facts["candidateSince"] = sinceDate.ToString("yyyy-MM-dd");
        var belowPeak = Math.Round(-pullback, 1);
        List<RuleCheck> checks =
        [
            new("gain", CheckGroup.Candidate, gain >= p.MinGainPct, Math.Round(gain, 1), p.MinGainPct,
                Level: Math.Round(lowest, 2), EndLevel: Math.Round(peak, 2), Progress: RuleCheck.ProgressOf(gain, p.MinGainPct),
                Date: candidate ? candidateSince[i] : null),
            new("stacked", CheckGroup.Candidate, aligned, Math.Round(Indicators.PctFrom(sma50, sma200), 1),
                Level: Math.Round(sma50, 2), MidLevel: Math.Round(sma150, 2), EndLevel: Math.Round(sma200, 2)),
            new("rising", CheckGroup.Candidate, trendRising[i], risePct, 0,
                Level: Math.Round(sma200, 2), EndLevel: Math.Round(sma200Ago, 2)),
            new("zone", CheckGroup.Buy, price <= zoneHigh && price >= zoneBottom, belowPeak, p.ZoneStartPct, p.ZoneEndPct,
                Math.Round(zoneHigh, 2), EndLevel: Math.Round(zoneBottom, 2)),
            new("floor", CheckGroup.Buy, price >= sma50, Math.Round(Indicators.PctFrom(price, sma50), 1), 0,
                Level: Math.Round(sma50, 2)),
        ];
        if (RuleCheck.Cooldown(context, p.CooldownDays) is { } cooldown) checks.Add(cooldown);
        return new RuleDecision(status, reason, facts, window.Open) { Checks = checks };
    }
}
