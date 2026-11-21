using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Engine;

/// <summary>
/// Winners at a discount (POC Leader pullback, <c>stage2_pullback</c>). A candidate is a proven leader: it rose at least
/// <c>MinGainPct</c> from its low (the lowest close of the last <c>GainDays</c> up to the peak's day: a fall after the peak
/// is no rise, from <c>LowBeforePeakFrom</c> on; the POC takes the window's low whatever the order; Ernest, 2026-11-16) and its averages are stacked (SMA 50 &gt; SMA 200) with the SMA 200 rising.
/// The POC's stack has a middle average as well (SMA 50 &gt; SMA 150 &gt; SMA 200): STL dropped it to simplify (Ernest,
/// 2026-11-21, <c>docs/dip-trend-rule-study.md</c>), and keeps it as an engine option for the golden tests.
/// The buy window is the discount zone, <c>ZoneStartPct</c>–<c>ZoneEndPct</c> below the recent peak and above the SMA 50.
/// </summary>
public class DipRules : BuyRulesBase
{
    const int MiddleDays = 150, RisingDays = 20, MinimumHistory = 220;

    readonly DipParams p;
    readonly IReadOnlyList<decimal> close;
    readonly decimal?[] floor, trend, low, high;
    readonly decimal?[]? middle; // The POC's stack only.
    readonly bool[] trendRising;
    readonly DateOnly?[] candidateSince;
    readonly BuyWindow window = new();

    /// <param name="pocStack">The POC's stack of three averages (SMA 50 &gt; SMA 150 &gt; SMA 200), for the golden tests only: an
    /// engine option, not a saved param.</param>
    public DipRules(IReadOnlyList<PriceBar> bars, DipParams parameters, bool pocStack = false) : base(bars.Count)
    {
        p = parameters;
        close = [.. bars.Select(b => b.Close)];
        floor = Indicators.Sma(close, p.FloorDays);
        middle = pocStack ? Indicators.Sma(close, MiddleDaysFor(p)) : null;
        trend = Indicators.Sma(close, p.TrendDays);
        var windowLow = Indicators.RollingLow(close, p.GainDays);
        var lowBeforePeak = Indicators.LowBeforeHigh(close, p.GainDays, p.PeakDays);
        low = [.. bars.Select((b, i) => b.Date < p.LowBeforePeakFrom ? windowLow[i] : lowBeforePeak[i])];
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
            var isCandidate = floor[i] is { } sma50 && trend[i] is { } sma200
                && low[i] is { } lowest && high[i] is { } peak
                && Indicators.PctFrom(peak, lowest) >= p.MinGainPct
                && Stacked(i, sma50, sma200)
                && trendRising[i];
            since = isCandidate ? (since ?? bars[i].Date) : null;
            result[i] = since;
        }
        return result;
    }

    /// <summary>The floor average above the long one; with the POC's stack, the middle one between them.</summary>
    bool Stacked(int i, decimal sma50, decimal sma200) =>
        middle is null ? sma50 > sma200 : middle[i] is { } sma150 && sma50 > sma150 && sma150 > sma200;

    /// <summary>
    /// The middle average of the POC's stack (the SMA 150 between the SMA 50 and SMA 200), kept strictly between the floor and the
    /// trend average when either is changed. With no room between them (equal days) it is the floor's, so nothing stacks.
    /// </summary>
    static int MiddleDaysFor(DipParams p)
    {
        var lowest = p.FloorDays + 1;
        var highest = p.TrendDays - 1;
        return lowest <= highest ? Math.Clamp(MiddleDays, lowest, highest) : p.FloorDays;
    }

    /// <summary>The POC's 220 days, and more when a window needs more: the trend average and its rise, the low, the peak.</summary>
    public override int MinHistory => new[] { MinimumHistory, p.GainDays, p.TrendDays + RisingDays, p.PeakDays }.Max(); // The middle average's days are always fewer than the trend's.

    public override int CooldownDays => p.CooldownDays;

    protected override RuleDecision DecideDay(int i, DayContext context)
    {
        if (floor[i] is not { } sma50 || trend[i] is not { } sma200
            || low[i] is not { } lowest || high[i] is not { } peak)
        {
            var (none, noneReason) = window.Compose(context, candidate: false, entryOk: false, extended: false, "", "");
            return new RuleDecision(none, noneReason, new Dictionary<string, object>());
        }

        var price = close[i];
        var gain = Indicators.PctFrom(peak, lowest);
        var pullback = Indicators.PctFrom(price, peak);
        var aligned = Stacked(i, sma50, sma200);
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
            // The stock page draws a middle level only for the POC's stack.
            new("stacked", CheckGroup.Candidate, aligned, Math.Round(Indicators.PctFrom(sma50, sma200), 1),
                Level: Math.Round(sma50, 2), MidLevel: middle?[i] is { } sma150 ? Math.Round(sma150, 2) : null, EndLevel: Math.Round(sma200, 2)),
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
