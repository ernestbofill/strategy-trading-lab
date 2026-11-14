using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Engine;

/// <summary>
/// The comeback (POC Confirmed turnaround, <c>turnaround</c>). A candidate was beaten down (at least <c>DrawdownPct</c>
/// below its 52-week high, and below the SMA 200) at some point in the last <c>CandidateDays</c> days. The first step is the
/// turn: the close at least <c>AboveSma50Pct</c> above the <c>LowDays</c>-day low on rising volume, but not more than <c>MaxChasePct</c> above
/// that line. Later steps are earned by milestones (back above the SMA 200, golden cross, near the old high, a breakout after
/// a pullback): each new one, reached after the last buy, earns one step, bought within <c>MaxChasePct</c> above its level.
/// Keeps state between days (the milestones used, the breakout tracker): one instance per run.
/// </summary>
public class ComebackRules : IBuyRules
{
    const int HighDays = 252, VolumeShort = 5, VolumeLong = 50, SmaShort = 50, SmaLong = 200;

    readonly ComebackParams p;
    readonly IReadOnlyList<decimal> close;
    readonly DateOnly[] dates;
    readonly decimal?[] low, sma50, sma200, high252, vol5, vol50;
    readonly bool[] candidate;
    readonly int?[] beatenAgo;
    readonly DateOnly?[] beatenOn;
    readonly BuyWindow window = new();

    // State between days, as the POC keeps in its `state`.
    readonly HashSet<string> used = [];
    DateOnly? usedAfterBuyOn;
    decimal? breakoutHigh;
    bool pulledBack;
    // For the stock page only (no verdict reads them): the first step's day, the milestone yesterday's signal was for, and
    // the milestones that bought a step, with its day. The others in `used` were already there at a buy.
    DateOnly? turnOn;
    string? signalled;
    readonly Dictionary<string, DateOnly> boughtOn = [];

    public ComebackRules(IReadOnlyList<PriceBar> bars, ComebackParams parameters)
    {
        p = parameters;
        close = [.. bars.Select(b => b.Close)];
        dates = [.. bars.Select(b => b.Date)];
        var volume = bars.Select(b => (decimal)b.Volume).ToList();
        low = Indicators.RollingLow(close, p.LowDays);
        sma50 = Indicators.Sma(close, SmaShort);
        sma200 = Indicators.Sma(close, SmaLong);
        high252 = Indicators.RollingHigh(close, HighDays);
        vol5 = Indicators.Sma(volume, VolumeShort);
        vol50 = Indicators.Sma(volume, VolumeLong);
        (candidate, beatenAgo, beatenOn) = Candidates();
    }

    public int Length => close.Count;

    public int MinHistory => Math.Max(HighDays, p.LowDays);

    public int CooldownDays => p.CooldownDays;

    /// <summary>
    /// A candidate stays one for <c>CandidateDays</c> days after it was last beaten down (today included). Also how many
    /// trading days ago that was, and on which date, each day (null if never).
    /// </summary>
    (bool[] Candidate, int?[] BeatenAgo, DateOnly?[] BeatenOn) Candidates()
    {
        var result = new bool[close.Count];
        var ago = new int?[close.Count];
        var on = new DateOnly?[close.Count];
        var lastBeaten = int.MinValue;
        for (var i = 0; i < close.Count; i++)
        {
            if (high252[i] is { } high && Indicators.PctFrom(close[i], high) <= -p.DrawdownPct
                && (!p.BelowSma200 || (sma200[i] is { } slow && close[i] < slow)))
                lastBeaten = i;
            result[i] = lastBeaten != int.MinValue && i - lastBeaten <= p.CandidateDays;
            ago[i] = lastBeaten == int.MinValue ? null : i - lastBeaten;
            on[i] = lastBeaten == int.MinValue ? null : dates[lastBeaten];
        }
        return (result, ago, on);
    }

    decimal Chase(decimal level) => level * (1 + p.MaxChasePct / 100);

    /// <summary>Whether the 5-day volume is at least <c>VolumeRatio</c> times the 50-day (no volume at all: no; the 50-day average is zero only when the 5-day one is, the POC's 0/0).</summary>
    bool VolumeOk(int i) => vol5[i] is { } short5 && vol50[i] is { } long50 && long50 > 0 && short5 / long50 >= p.VolumeRatio;

    int lastDecided = -1;

    public RuleDecision Decide(int i, DayContext day)
    {
        if (i <= lastDecided) throw new InvalidOperationException("These rules keep state from day to day and were already run: build new ones for each run.");
        lastDecided = i;
        if (low[i] is not { } lowest || sma50[i] is null || high252[i] is not { } high || sma200[i] is null)
        {
            var (none, noneReason) = window.Compose(day, candidate: false, entryOk: false, extended: false, "", "");
            return new RuleDecision(none, noneReason, new Dictionary<string, object>());
        }

        var price = close[i];
        var buyLine = lowest * (1 + p.AboveSma50Pct / 100);
        var facts = new Dictionary<string, object>
        {
            ["aboveBuyLinePct"] = Math.Round(Indicators.PctFrom(price, buyLine), 2),
            ["belowHighPct"] = Math.Round(-Indicators.PctFrom(price, high), 2),
            ["buyLine"] = Math.Round(buyLine, 4),
            // How far the price still has to rise to reach the buy line (0 at or above it): "close to the line".
            ["distancePct"] = Math.Round(Math.Max(0, Indicators.PctFrom(buyLine, price)), 2),
        };
        if (candidate[i] && beatenOn[i] is { } sinceDate) facts["candidateSince"] = sinceDate.ToString("yyyy-MM-dd");

        if (!day.HasPosition)
        {
            // A new position starts afresh.
            used.Clear();
            usedAfterBuyOn = null;
            breakoutHigh = null;
            pulledBack = false;
            turnOn = null;
            signalled = null;
            boughtOn.Clear();
            var (status, reason) = window.Compose(day, candidate[i], entryOk: price >= buyLine && VolumeOk(i), extended: price > Chase(buyLine),
                "Price or volume fell back below the buy conditions.",
                $"Price is more than {p.MaxChasePct:0.##}% above the buy line: too late to chase.");
            var turn = TurnChecks(i, price, high, lowest, buyLine);
            // After a sale the cooldown holds the first buy back.
            if (RuleCheck.Cooldown(day, p.CooldownDays) is { } wait) turn.Add(wait);
            return new RuleDecision(status, reason, facts, window.Open) { Checks = turn };
        }

        var (milestones, bought) = Milestones(i, day);
        var (held, heldReason, active) = Adding(price, day, milestones, bought);
        // The window stays open while any reached milestone's level is within the chase limit, the signalled one included
        // (a buy marks it used, which must not close the window the day after).
        var windowOpen = milestones.Any(m => InWindow(m, price));
        if (held == RuleStatus.BuySignal && active is not null) facts["milestone"] = active.Term;
        signalled = held == RuleStatus.BuySignal ? active?.Key : null;
        // The step already bought comes first: the turn, step 1.
        List<RuleCheck> checks = [new("turn", CheckGroup.Next, true, Used: true, Bought: true, Date: turnOn)];
        checks.AddRange(MilestoneChecks(i, price, milestones, preview: false));
        // Held, the cooldown holds the next step back: with the milestones that earn it.
        if (RuleCheck.Cooldown(day, p.CooldownDays, CheckGroup.Next) is { } cooldown) checks.Add(cooldown);
        return new RuleDecision(held, heldReason, facts, windowOpen) { Checks = checks };
    }

    /// <summary>The checks before the first step: beaten down lately, the turn, and the next-step milestones in preview.</summary>
    List<RuleCheck> TurnChecks(int i, decimal price, decimal high, decimal lowest, decimal buyLine)
    {
        var ratio = vol5[i] is { } short5 && vol50[i] is { } long50 && long50 > 0 ? short5 / long50 : 0;
        var dropPct = Math.Round(Indicators.PctFrom(price, high), 1);
        var slow200 = sma200[i];
        var vsSma200Pct = slow200 is { } s200 ? Math.Round(Indicators.PctFrom(price, s200), 1) : (decimal?)null;
        List<RuleCheck> checks =
        [
            new("beaten", CheckGroup.Candidate, candidate[i], beatenAgo[i], p.DrawdownPct, p.CandidateDays,
                Level: Math.Round(high, 2),
                EndLevel: slow200 is { } l200 ? Math.Round(l200, 2) : null,
                Date: beatenOn[i],
                SubValue: dropPct,
                EndValue: vsSma200Pct),
        ];
        if (!candidate[i])
        {
            var rawDrop = Math.Max(0, -Indicators.PctFrom(price, high));
            checks.Add(new("drop", CheckGroup.Candidate, rawDrop >= p.DrawdownPct, dropPct, p.DrawdownPct,
                Level: Math.Round(high, 2), Progress: RuleCheck.ProgressOf(rawDrop, p.DrawdownPct)));
            if (p.BelowSma200 && slow200 is { } slow)
                checks.Add(new("vsSma200", CheckGroup.Candidate, price < slow, vsSma200Pct, p.DrawdownPct, Level: Math.Round(slow, 2)));
        }
        var aboveLow = Math.Round(Indicators.PctFrom(price, lowest), 1);
        var chaseLimit = Chase(buyLine);
        var maxAboveLow = Math.Round(Indicators.PctFrom(chaseLimit, lowest), 1);
        var inBuyWindow = price >= buyLine && price <= chaseLimit;
        checks.Add(new("buyLine", CheckGroup.Buy, inBuyWindow, aboveLow, p.AboveSma50Pct, maxAboveLow,
            Math.Round(buyLine, 2), MidLevel: Math.Round(lowest, 2), EndLevel: Math.Round(chaseLimit, 2),
            SubValue: p.LowDays, EndValue: p.MaxChasePct));
        var volumeOk = VolumeOk(i);
        checks.Add(new("volume", CheckGroup.Buy, volumeOk, Math.Round(ratio, 2), p.VolumeRatio,
            Progress: RuleCheck.ProgressOf(ratio, p.VolumeRatio)));
        checks.AddRange(MilestoneChecks(i, price, BuildMilestones(i, price, pulled: false, highBeforePullback: null), preview: true));
        return checks;
    }

    List<RuleCheck> MilestoneChecks(int i, decimal price, IReadOnlyList<Milestone> milestones, bool preview)
    {
        var s50 = sma50[i];
        var s200 = sma200[i];
        var goldenSpread = s50 is { } shortAvg && s200 is { } longAvg
            ? (shortAvg > longAvg
                ? Math.Max(0.1m, Math.Round(Indicators.PctFrom(shortAvg, longAvg), 1))
                : Math.Min(0m, Math.Round(Indicators.PctFrom(shortAvg, longAvg), 1)))
            : (decimal?)null;
        // Held, before a pullback: the highest close since the last buy, which a fall of the pullback % starts to watch.
        var tracked = !preview && !pulledBack ? breakoutHigh : null;
        return [.. milestones.Select(m =>
        {
            var isUsed = !preview && used.Contains(m.Key);
            var met = isUsed || InWindow(m, price);
            // No golden cross yet: the price vs the SMA 50 says nothing until it comes, only how far the SMA 50 must rise.
            var notCrossed = m.Key == "golden" && !m.Reached;
            var val = m.Level is { } level && !notCrossed ? Math.Round(Indicators.PctFrom(price, level), 1) : (decimal?)null;
            var min = m.Key switch { "near_high" => p.NearHighPct, "breakout" => p.BreakoutPullbackPct, _ => (decimal?)0 };
            var line = m.Level is { } l ? Math.Round(l, 2) : (decimal?)null;
            var midLine = m.Key == "golden" && s200 is { } slow ? Math.Round(slow, 2)
                : m.Key == "near_high" && high252[i] is { } h ? Math.Round(h, 2)
                : m.Key == "breakout" && tracked is { } top ? Math.Round(top, 2)
                : (decimal?)null;
            var endLine = m.Level is { } chaseBase ? Math.Round(Chase(chaseBase), 2)
                : m.Key == "breakout" && tracked is { } watchFrom ? Math.Round(watchFrom * (1 - p.BreakoutPullbackPct / 100), 2)
                : (decimal?)null;
            var pullback = m.Key == "breakout" && tracked is { } peak
                ? RuleCheck.ProgressOf(Math.Max(0, -Indicators.PctFrom(price, peak)), p.BreakoutPullbackPct)
                : null;
            var riseToCross = notCrossed && s50 is { } short50 && s200 is { } long200 && short50 > 0
                ? Math.Max(0.1m, Math.Round(Indicators.PctFrom(long200, short50), 1)) // Never "rise 0%" before the cross.
                : (decimal?)null;
            var bought = isUsed && boughtOn.TryGetValue(m.Key, out var on) ? on : (DateOnly?)null;
            return new RuleCheck(m.Key, CheckGroup.Next, met, val, min, p.MaxChasePct, line,
                Progress: pullback,
                Used: isUsed,
                // Used without buying is false, not null: checks stored before this field read as before ("Step bought").
                Bought: isUsed ? bought is not null : null,
                Date: bought,
                MidLevel: midLine,
                EndLevel: endLine,
                SubValue: m.Key == "golden" ? goldenSpread : null,
                EndValue: riseToCross,
                Preview: preview ? true : null);
        })];
    }

    sealed record Milestone(string Key, string Term, bool Reached, decimal? Level);

    List<Milestone> BuildMilestones(int i, decimal price, bool pulled, decimal? highBeforePullback)
    {
        var nearHighLevel = high252[i]!.Value * (1 - p.NearHighPct / 100); // Non-null: Decide checked it.
        return
        [
            new("sma200", "Reclaims the SMA 200", price > sma200[i], sma200[i]),
            new("golden", "Golden cross (SMA 50 above SMA 200)", sma50[i] > sma200[i], sma50[i]),
            new("near_high", $"Within {p.NearHighPct:0.##}% of the 52-week high", price >= nearHighLevel, nearHighLevel),
            new("breakout", $"Higher high after pulling back {p.BreakoutPullbackPct:0.##}% (breakout)", pulled && price > highBeforePullback,
                pulled ? highBeforePullback : null),
        ];
    }

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
        var milestones = BuildMilestones(i, price, pulledBack, breakoutHigh);
        if (bought)
        {
            // The first buy is the turn; a later one is for the milestone of the signal before it. A step bought on the signal's
            // own day (RulesRunner's boughtBeforeLast) has no signal before it: the open milestone in its window, as the signal's.
            if (turnOn is null) turnOn = day.LastBuyDay;
            else if ((signalled ?? milestones.FirstOrDefault(m => !used.Contains(m.Key) && InWindow(m, price))?.Key) is { } key)
                boughtOn[key] = day.LastBuyDay;
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
