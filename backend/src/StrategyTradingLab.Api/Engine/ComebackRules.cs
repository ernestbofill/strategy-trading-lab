using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Engine;

/// <summary>
/// The comeback (POC Confirmed turnaround, <c>turnaround</c>). A candidate was beaten down (at least <c>DrawdownPct</c>
/// below its 52-week high, and below the SMA 200) at some point in the last <c>CandidateDays</c> days. The first step is the
/// turn: the close at least <c>AboveSma50Pct</c> above the SMA 50 on rising volume, but not more than <c>MaxChasePct</c> above
/// that line. Later steps are earned by milestones (back above the SMA 200, golden cross, near the old high, a breakout after
/// a pullback): each new one, reached after the last buy, earns one step, bought within <c>MaxChasePct</c> above its level.
/// Keeps state between days (the milestones used, the breakout tracker): one instance per run.
/// </summary>
public class ComebackRules : IBuyRules
{
    const int HighDays = 252, VolumeShort = 5, VolumeLong = 50, SmaShort = 50, SmaLong = 200;

    readonly ComebackParams p;
    readonly IReadOnlyList<decimal> close;
    readonly decimal?[] sma50, sma200, high252, vol5, vol50;
    readonly bool[] candidate;
    readonly BuyWindow window = new();

    // State between days, as the POC keeps in its `state`.
    readonly HashSet<string> used = [];
    DateOnly? usedAfterBuyOn;
    decimal? breakoutHigh;
    bool pulledBack;

    public ComebackRules(IReadOnlyList<PriceBar> bars, ComebackParams parameters)
    {
        p = parameters;
        close = [.. bars.Select(b => b.Close)];
        var volume = bars.Select(b => (decimal)b.Volume).ToList();
        sma50 = Indicators.Sma(close, SmaShort);
        sma200 = Indicators.Sma(close, SmaLong);
        high252 = Indicators.RollingHigh(close, HighDays);
        vol5 = Indicators.Sma(volume, VolumeShort);
        vol50 = Indicators.Sma(volume, VolumeLong);
        candidate = Candidates();
    }

    public int Length => close.Count;

    public int MinHistory => HighDays;

    public int CooldownDays => p.CooldownDays;

    /// <summary>A candidate stays one for <c>CandidateDays</c> days after it was last beaten down (today included).</summary>
    bool[] Candidates()
    {
        var result = new bool[close.Count];
        var lastBeaten = int.MinValue;
        for (var i = 0; i < close.Count; i++)
        {
            if (high252[i] is { } high && Indicators.PctFrom(close[i], high) <= -p.DrawdownPct
                && (!p.BelowSma200 || (sma200[i] is { } slow && close[i] < slow)))
                lastBeaten = i;
            result[i] = lastBeaten != int.MinValue && i - lastBeaten <= p.CandidateDays;
        }
        return result;
    }

    decimal Chase(decimal level) => level * (1 + p.MaxChasePct / 100);

    /// <summary>Whether the 5-day volume is at least <c>VolumeRatio</c> times the 50-day (no volume at all: no).</summary>
    bool VolumeOk(int i) => vol5[i] is { } short5 && vol50[i] is { } long50
        && (long50 > 0 ? short5 / long50 >= p.VolumeRatio : short5 > 0);

    public RuleDecision Decide(int i, DayContext day)
    {
        var price = close[i];
        var slow50 = sma50[i]!.Value;
        var buyLine = slow50 * (1 + p.AboveSma50Pct / 100);
        var facts = new Dictionary<string, object>
        {
            ["aboveBuyLinePct"] = Math.Round(Indicators.PctFrom(price, buyLine), 2),
            ["belowHighPct"] = Math.Round(-Indicators.PctFrom(price, high252[i]!.Value), 2),
            ["buyLine"] = Math.Round(buyLine, 4),
        };

        if (!day.HasPosition)
        {
            // A new position starts afresh.
            used.Clear();
            usedAfterBuyOn = null;
            breakoutHigh = null;
            pulledBack = false;
            var (status, reason) = window.Compose(day, candidate[i], entryOk: price >= buyLine && VolumeOk(i), extended: price > Chase(buyLine),
                "Price or volume fell back below the buy conditions.",
                $"Price is more than {p.MaxChasePct:0.##}% above the buy line: too late to chase.");
            return new RuleDecision(status, reason, facts);
        }

        var (milestones, bought) = Milestones(i, day);
        var (held, heldReason, active) = Adding(price, day, milestones, bought);
        if (held == RuleStatus.BuySignal && active is not null) facts["milestone"] = active.Term;
        return new RuleDecision(held, heldReason, facts);
    }

    sealed record Milestone(string Key, string Term, bool Reached, decimal? Level);

    /// <summary>Today's milestones. A buy uses up every sign already there: only new ones earn a step.</summary>
    (List<Milestone> Milestones, bool Bought) Milestones(int i, DayContext day)
    {
        var price = close[i];
        var bought = usedAfterBuyOn != day.LastBuyDay || day.ActedNow;
        if (breakoutHigh is null)
        {
            breakoutHigh = price; // A new position: the tracker starts at its first close.
            pulledBack = false;
        }
        else if (!pulledBack)
        {
            breakoutHigh = Math.Max(breakoutHigh.Value, price);
            if (price <= breakoutHigh * (1 - p.BreakoutPullbackPct / 100)) pulledBack = true; // The high before the pullback is the breakout level.
        }
        var nearHighLevel = high252[i]!.Value * (1 - p.NearHighPct / 100);
        List<Milestone> milestones =
        [
            new("sma200", "Reclaims the SMA 200", price > sma200[i], sma200[i]),
            new("golden", "Golden cross (SMA 50 above SMA 200)", sma50[i] > sma200[i], sma50[i]),
            new("near_high", $"Within {p.NearHighPct:0.##}% of the 52-week high", price >= nearHighLevel, nearHighLevel),
            new("breakout", $"Higher high after pulling back {p.BreakoutPullbackPct:0.##}% (breakout)", pulledBack && price > breakoutHigh,
                pulledBack ? breakoutHigh : null),
        ];
        if (bought)
        {
            foreach (var m in milestones.Where(m => m.Reached)) used.Add(m.Key);
            usedAfterBuyOn = day.LastBuyDay;
            breakoutHigh = price; // The next breakout counts from here.
            pulledBack = false;
        }
        return (milestones, bought);
    }

    bool InWindow(Milestone m, decimal price) => m.Reached && m.Level is { } level && level <= price && price <= Chase(level);

    /// <summary>The status of a position the rules hold: milestones earn steps (the candidate test no longer applies).</summary>
    (RuleStatus, string?, Milestone?) Adding(decimal price, DayContext day, List<Milestone> milestones, bool bought)
    {
        var open = milestones.Where(m => !used.Contains(m.Key)).ToList();
        var inWindow = open.Where(m => InWindow(m, price)).ToList();
        var waiting = open.Where(m => m.Reached && m.Level is not null && !InWindow(m, price)).ToList();
        var active = inWindow.FirstOrDefault() ?? waiting.FirstOrDefault();
        if (day.Full) return (RuleStatus.Holding, null, active);
        if (open.Count == 0) return (RuleStatus.Holding, null, null); // Every milestone used: no more steps for this position.
        if (bought || day.ActedNow || day.Cooling) return (RuleStatus.Building, null, active);
        if (inWindow.Count > 0) return (RuleStatus.BuySignal, null, active);
        if (day.Previous == RuleStatus.BuySignal)
        {
            // The milestone's window closed without buying.
            var reason = waiting.Any(m => m.Level is { } level && price > level)
                ? $"Price is more than {p.MaxChasePct:0.##}% above the milestone's level: too late to chase."
                : "The price left the milestone's buy window before buying.";
            return (RuleStatus.Building, reason, active);
        }
        return (RuleStatus.Building, null, active);
    }
}
