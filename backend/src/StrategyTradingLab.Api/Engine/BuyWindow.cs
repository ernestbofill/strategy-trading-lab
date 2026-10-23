namespace StrategyTradingLab.Api.Engine;

/// <summary>
/// The buy window: open while every buy condition holds, closed when one is lost or the price ran too far past the buy
/// point to chase. Port of the POC's <c>compose_status</c> (shared status logic for entry/exit strategies), without sells.
/// Scaling in: a position below its target is Building and gets one step per window, which is why the window's first day
/// is kept. Keeps state between days, so one instance per run.
/// </summary>
public sealed class BuyWindow
{
    const string NotCandidate = "It no longer meets the candidate criteria.";

    bool open;
    DateOnly? start;

    /// <summary>Whether the window was open at the last <see cref="Compose"/>.</summary>
    public bool Open => open;

    public (RuleStatus Status, string? Reason) Compose(DayContext day, bool candidate, bool entryOk, bool extended,
        string lostReason, string extendedReason)
    {
        var windowOpen = candidate && entryOk && !extended;
        if (windowOpen && !open) start = day.Day;
        open = windowOpen;

        if (day.HasPosition)
        {
            if (day.Full) return (RuleStatus.Holding, null);
            if (day.Cooling || day.ActedNow) return (RuleStatus.Building, null);
            var boughtInWindow = day.LastBuyDay >= (start ?? day.Day);
            if (windowOpen && !boughtInWindow) return (RuleStatus.BuySignal, null);
            if (day.Previous == RuleStatus.BuySignal && !windowOpen && !boughtInWindow)
            {
                // The window for the next step closed without buying it.
                var closed = !candidate ? NotCandidate : entryOk && extended ? extendedReason : lostReason;
                return (RuleStatus.Building, closed);
            }
            return (RuleStatus.Building, null);
        }
        if (!candidate)
            return day.Previous == RuleStatus.BuySignal && !day.ActedNow ? (RuleStatus.SignalEnded, NotCandidate) : (RuleStatus.NotCandidate, null);
        if (day.Cooling || day.ActedNow) return (RuleStatus.Watching, null);
        if (entryOk && extended)
        {
            // Only a window that actually opened can close. A jump straight past the buy point stays "watching".
            if (day.Previous is RuleStatus.BuySignal or RuleStatus.SignalEnded) return (RuleStatus.SignalEnded, extendedReason);
            return (RuleStatus.Watching, $"Jumped past the buy window without a buy signal. {extendedReason}");
        }
        if (entryOk) return (RuleStatus.BuySignal, null);
        return day.Previous == RuleStatus.BuySignal ? (RuleStatus.SignalEnded, lostReason) : (RuleStatus.Watching, null);
    }
}
