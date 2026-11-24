using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Engine;

/// <summary>
/// The comeback (POC Confirmed turnaround, <c>turnaround</c>). A candidate was beaten down (at least <c>DrawdownPct</c>
/// below its 52-week high, and below the SMA 200) at some point in the last <c>CandidateDays</c> days. The first step is the
/// turn: the close at least <c>AboveSma50Pct</c> above the <c>LowDays</c>-day low on heavier volume, but not more than <c>MaxChasePct</c> further
/// above the low (Ernest, 2026-11-17; the POC: <c>MaxChasePct</c> above that line), on a close above the last one. A price back in
/// that window from above the chase limit needs no volume, only the up day (Ernest, 2026-11-22). Later steps come at
/// a pace: one every <c>StepEveryDays</c> trading days, while the close is above the last step's price (its open), so a step
/// is only added while the turn is working (Ernest, 2026-11-21; the POC earns them by milestones, removed in #268).
/// Keeps state between days (the turn's day, a signal's window): one instance per run.
/// </summary>
public class ComebackRules : BuyRulesBase
{
    const int HighDays = 252, VolumeShort = 5, VolumeLong = 50, SmaLong = 200;

    readonly ComebackParams p;
    readonly bool pocTurn;
    readonly IReadOnlyList<decimal> close, opens;
    readonly DateOnly[] dates;
    readonly decimal?[] low, sma200, high252, vol5, vol50;
    readonly bool[] candidate;
    readonly int?[] beatenAgo;
    readonly DateOnly?[] beatenOn, candidateFrom;
    readonly BuyWindow window = new();

    // State between days. The turn's day, for the stock page only (no verdict reads it).
    DateOnly? turnOn;
    // The price a bought step's signal window follows: it stays open while the close is above it (see Paced). A later step's
    // signal sets `pendingFrom`, which only becomes the window's price once its step is bought (the replay can drop a signal).
    decimal? signalFrom, pendingFrom;

    /// <param name="pocTurn">The POC's first step, for the golden tests only (an engine option, not a saved param): its chase limit
    /// <c>MaxChasePct</c> above the buy line, and the volume on any price, never an up day or a pullback from above.</param>
    public ComebackRules(IReadOnlyList<PriceBar> bars, ComebackParams parameters, bool pocTurn = false) : base(bars.Count)
    {
        p = parameters;
        this.pocTurn = pocTurn;
        close = [.. bars.Select(b => b.Close)];
        opens = [.. bars.Select(b => b.Open)];
        dates = [.. bars.Select(b => b.Date)];
        var volume = bars.Select(b => (decimal)b.Volume).ToList();
        low = Indicators.RollingLow(close, p.LowDays);
        sma200 = Indicators.Sma(close, SmaLong);
        high252 = Indicators.RollingHigh(close, HighDays);
        vol5 = Indicators.Sma(volume, VolumeShort);
        vol50 = Indicators.Sma(volume, VolumeLong);
        (candidate, beatenAgo, beatenOn, candidateFrom) = Candidates();
    }

    public override int MinHistory => Math.Max(HighDays, p.LowDays);

    public override int CooldownDays => p.CooldownDays;

    /// <summary>
    /// A candidate stays one for <c>CandidateDays</c> days after it was last beaten down (today included). Also how many
    /// trading days ago that was, and on which date, each day (null if never), and the first day of the candidate's unbroken
    /// run ("Candidate since"): being beaten down again every day moves the last date, never this one (feedback, 2026-11-21).
    /// </summary>
    (bool[] Candidate, int?[] BeatenAgo, DateOnly?[] BeatenOn, DateOnly?[] CandidateFrom) Candidates()
    {
        var result = new bool[close.Count];
        var ago = new int?[close.Count];
        var on = new DateOnly?[close.Count];
        var from = new DateOnly?[close.Count];
        var lastBeaten = int.MinValue;
        DateOnly? runFrom = null;
        for (var i = 0; i < close.Count; i++)
        {
            if (high252[i] is { } high && Indicators.PctFrom(close[i], high) <= -p.DrawdownPct
                && (!p.BelowSma200 || (sma200[i] is { } slow && close[i] < slow)))
                lastBeaten = i;
            result[i] = lastBeaten != int.MinValue && i - lastBeaten <= p.CandidateDays;
            ago[i] = lastBeaten == int.MinValue ? null : i - lastBeaten;
            on[i] = lastBeaten == int.MinValue ? null : dates[lastBeaten];
            runFrom = result[i] ? runFrom ?? dates[i] : null;
            from[i] = runFrom;
        }
        return (result, ago, on, from);
    }

    /// <summary>A % to one decimal, never "0%" for a small move: the verdict reads the unrounded figure.</summary>
    static decimal Shown(decimal pct) => Math.Round(pct, 1) is var shown && shown == 0 && pct != 0 ? 0.1m * Math.Sign(pct) : shown;

    /// <summary>The first step's chase limit: <c>MaxChasePct</c> more above the low than the buy line (POC: above the line).</summary>
    decimal TurnLimit(decimal lowest, decimal buyLine) =>
        pocTurn ? buyLine * (1 + p.MaxChasePct / 100) : lowest * (1 + (p.AboveSma50Pct + p.MaxChasePct) / 100);

    /// <summary>The day of the recent low: the latest of the last <c>LowDays</c> closes at it.</summary>
    DateOnly LowOn(int i, decimal lowest)
    {
        for (var k = i; k > i - p.LowDays && k >= 0; k--)
            if (close[k] == lowest) return dates[k];
        return dates[i];
    }

    /// <summary>The close's change since the last close, in % (null on the first day).</summary>
    decimal? DayPct(int i) => i >= 1 && close[i - 1] > 0 ? Indicators.PctFrom(close[i], close[i - 1]) : null;

    /// <summary>
    /// Whether the 5-day volume is at least <c>VolumeRatio</c> times the 50-day (no volume at all: no; the 50-day average is zero
    /// only when the 5-day one is, the POC's 0/0).
    /// </summary>
    bool VolumeOk(int i) => vol5[i] is { } short5 && vol50[i] is { } long50 && long50 > 0 && short5 / long50 >= p.VolumeRatio;

    /// <summary>
    /// The first step needs a close above the last one: heavy trading on a falling price is selling, not a turn (Ernest,
    /// 2026-11-16, then 2026-11-22 for the last close instead of the one 5 days before; not in the POC). A signal is bought the
    /// next day, so a down day can't end one that is open.
    /// </summary>
    bool UpOk(int i) => pocTurn || DayPct(i) > 0;

    /// <summary>
    /// Whether the price is back in the turn's window from above: the last close outside the window (since the recent low) was
    /// above the chase limit. Such a pullback needs no volume: trading is usually light on one, and the turn already ran. A
    /// stock with no volume data never gets it, as it never meets the volume either.
    /// </summary>
    bool FromAbove(int i, decimal lowest, decimal buyLine, decimal limit)
    {
        if (pocTurn || vol50[i] is not > 0 || close[i] < buyLine || close[i] > limit) return false;
        for (var k = i - 1; k > i - p.LowDays && k >= 0; k--)
        {
            if (close[k] > limit) return true;
            // Below the window: came from below. The low is too, even with a 0% buy line (the params allow it), where it is on the line.
            if (close[k] < buyLine || close[k] <= lowest) return false;
        }
        return false;
    }

    protected override RuleDecision DecideDay(int i, DayContext day)
    {
        if (low[i] is not { } lowest || high252[i] is not { } high || sma200[i] is null)
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
        if (candidateFrom[i] is { } sinceDate) facts["candidateSince"] = sinceDate.ToString("yyyy-MM-dd");

        if (!day.HasPosition)
        {
            // A new position starts afresh.
            turnOn = null;
            signalFrom = null;
            pendingFrom = null;
            var limit = TurnLimit(lowest, buyLine);
            var fromAbove = FromAbove(i, lowest, buyLine, limit);
            // Past the chase limit the window can't open anyway: the up day only opens it, so a down day up there keeps the
            // "too late to chase" reason.
            var entryOk = price >= buyLine && (fromAbove || VolumeOk(i)) && (price > limit || UpOk(i));
            var (status, reason) = window.Compose(day, candidate[i], entryOk, extended: price > limit,
                "Price or volume fell back below the buy conditions.",
                pocTurn
                    ? $"Price is more than {p.MaxChasePct:0.##}% above the buy line: too late to chase."
                    : $"Price is more than {p.AboveSma50Pct + p.MaxChasePct:0.##}% above the recent low: too late to chase.");
            if (fromAbove) facts["turn"] = "pullback"; // The alert's words: no volume to speak of.
            var turn = TurnChecks(i, price, high, lowest, buyLine, limit, fromAbove);
            // After a sale the cooldown holds the first buy back.
            if (RuleCheck.Cooldown(day, p.CooldownDays) is { } wait) turn.Add(wait);
            return new RuleDecision(status, reason, facts, window.Open) { Checks = turn };
        }

        return Paced(i, price, day, facts);
    }

    /// <summary>The checks before the first step: beaten down lately, the turn, and the next steps' pace in preview.</summary>
    List<RuleCheck> TurnChecks(int i, decimal price, decimal high, decimal lowest, decimal buyLine, decimal chaseLimit, bool fromAbove)
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
        var maxAboveLow = Math.Round(Indicators.PctFrom(chaseLimit, lowest), 1);
        var inBuyWindow = price >= buyLine && price <= chaseLimit;
        // Date: the recent low's day, which the chart marks.
        checks.Add(new("buyLine", CheckGroup.Buy, inBuyWindow, aboveLow, p.AboveSma50Pct, maxAboveLow,
            Math.Round(buyLine, 2), Date: LowOn(i, lowest), MidLevel: Math.Round(lowest, 2), EndLevel: Math.Round(chaseLimit, 2),
            SubValue: p.LowDays, EndValue: p.MaxChasePct));
        var upOk = UpOk(i);
        var move = !pocTurn && DayPct(i) is { } dayPct ? Shown(dayPct) : (decimal?)null;
        if (fromAbove)
        {
            // Back from above the chase limit: no volume needed, only the up day.
            checks.Add(new("pullback", CheckGroup.Buy, upOk, Level: Math.Round(chaseLimit, 2), SubValue: move));
        }
        else
        {
            var volumeOk = VolumeOk(i) && upOk;
            checks.Add(new("volume", CheckGroup.Buy, volumeOk, Math.Round(ratio, 2), p.VolumeRatio,
                // Enough volume on a price that isn't rising is not met: the bar stops short of its target, as the verdict says.
                Progress: RuleCheck.ProgressOf(ratio, p.VolumeRatio) is { } done && !volumeOk ? Math.Min(done, 0.99m) : RuleCheck.ProgressOf(ratio, p.VolumeRatio),
                SubValue: move));
        }
        checks.Add(new("pace", CheckGroup.Next, false, null, p.StepEveryDays, Preview: true));
        return checks;
    }

    /// <summary>
    /// The latest step, by the runner's <c>LastBuyDay</c>: trading days since its signal (the bar before its buy, as <i>Bear
    /// markets</i> counts, so steps come exactly <c>StepEveryDays</c> apart) and its price, the open it was bought at. A step
    /// bought after the last bar (a Manual buy answered before tomorrow's open) was signalled today, at today's close.
    /// </summary>
    (int Since, decimal Price) LastStep(int i, DayContext day)
    {
        var k = Array.BinarySearch(dates, day.LastBuyDay);
        if (k < 0) k = ~k; // No bar on that day (a revised history): the next one.
        return k > i ? (0, close[i]) : (i - k + 1, opens[k]);
    }

    /// <summary>A held position's next steps: one every <c>StepEveryDays</c> trading days, while the close is above the last step's price.</summary>
    RuleDecision Paced(int i, decimal price, DayContext day, Dictionary<string, object> facts)
    {
        turnOn ??= day.LastBuyDay; // The first day held: the turn's step was bought on it.
        // A bought step's signal window stays open while the close is above the price it came from: the rules' own buy at the
        // next open restarts the pace, which must not close it (a Manual signal would read missed the next day). A paced step
        // follows the step price its signal came from; the turn, its own buy price.
        if (day.ActedNow) signalFrom = pendingFrom ?? LastStep(i, day).Price;
        pendingFrom = null;
        var windowClosed = signalFrom is { } from && price <= from;
        if (windowClosed) signalFrom = null;
        var closedReason = windowClosed ? "The price fell back, so the signal window closed." : null;
        List<RuleCheck> checks = [new("turn", CheckGroup.Next, true, Used: true, Date: turnOn)];
        // Every step bought: the pace has nothing left to earn, so it has no row (a used row would read as a step it bought).
        if (day.Full) return new RuleDecision(RuleStatus.Holding, closedReason, facts, WindowOpen: signalFrom is not null) { Checks = checks };

        var (since, lastPrice) = LastStep(i, day);
        var due = since >= p.StepEveryDays;
        var abovePct = Indicators.PctFrom(price, lastPrice);
        var above = price > lastPrice;
        facts["sinceLastDays"] = since;
        facts["aboveLastStepPct"] = Math.Round(abovePct, 2);
        var status = !day.ActedNow && !day.Cooling && due && above ? RuleStatus.BuySignal : RuleStatus.Building;
        // A signal's window closed: the price fell back to the step price it came from.
        var reason = status == RuleStatus.BuySignal ? null : closedReason;
        // Once due, the pace reads due (not "45 of 10"): the last-step row says what it waits for.
        var shownDays = Math.Min(since, p.StepEveryDays);
        checks.Add(new("pace", CheckGroup.Next, due, shownDays, p.StepEveryDays, Progress: RuleCheck.ProgressOf(shownDays, p.StepEveryDays)));
        checks.Add(new("lastStep", CheckGroup.Next, above, Shown(abovePct), 0, Level: Math.Round(lastPrice, 2)));
        if (RuleCheck.Cooldown(day, p.CooldownDays, CheckGroup.Next) is { } cooldown) checks.Add(cooldown);
        if (status == RuleStatus.BuySignal) pendingFrom = lastPrice;
        return new RuleDecision(status, reason, facts, WindowOpen: (due && above) || signalFrom is not null) { Checks = checks };
    }
}
