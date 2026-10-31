using System.Globalization;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Engine;

/// <summary>
/// Bear markets (POC Bear-market accumulation, <c>bear_accumulation</c>). Tracks its own cycle through four phases:
/// <c>normal</c> → <c>drawdown</c> (at least <c>DrawdownPct</c> below the recent <c>HighDays</c> high) → <c>accumulating</c>
/// (the first step once the close rebounds <c>ReboundPct</c> off the drop's low, then a step every <c>StepEveryDays</c>
/// trading days) → <c>done</c> once all steps are bought. Resets to <c>normal</c> when the price recovers the pre-drop high,
/// or, when <c>Rearm</c> is on and the drop is at least <c>HighDays</c> trading days old, rearms for a second leg down.
/// </summary>
public class BearRules : IBuyRules
{
    readonly BearParams p;
    readonly IReadOnlyList<decimal> close;
    readonly decimal?[] recentHigh;

    string phase = "normal";
    int age;
    int sinceLast;
    decimal peak;
    decimal low;
    int doneSteps;
    bool rearmed;

    public BearRules(IReadOnlyList<PriceBar> bars, BearParams parameters)
    {
        p = parameters;
        close = [.. bars.Select(b => b.Close)];
        recentHigh = Indicators.RollingHigh(close, p.HighDays);
    }

    public int Length => close.Count;

    public int MinHistory => p.HighDays;

    public int CooldownDays => 0;

    int lastDecided = -1;

    public RuleDecision Decide(int i, DayContext day)
    {
        if (i <= lastDecided) throw new InvalidOperationException("These rules keep state from day to day and were already run: build new ones for each run.");
        lastDecided = i;
        if (recentHigh[i] is not { } high)
            return new RuleDecision(RuleStatus.NotEnoughData, $"Waiting for {p.HighDays} trading days of history.", new Dictionary<string, object>());

        var price = close[i];
        var count = Math.Max(1, day.MaxSteps);
        if (phase != "normal") age++;
        if (phase is "accumulating" or "done") sinceLast++;

        string? reason = null;
        var rearmedToday = false;
        if (phase == "done")
        {
            if (price >= peak)
            {
                phase = "normal";
                reason = "Recovered its previous high: the cycle is complete.";
            }
            else if (p.Rearm && age >= p.HighDays)
            {
                // A long bear market that never recovered its old peak: the 6-month high has rolled
                // into post-crash prices, so watch for a fresh leg down from that lower high.
                phase = "normal";
                rearmed = true;
                rearmedToday = true;
                reason = string.Create(CultureInfo.InvariantCulture,
                    $"The drop began {age} trading days ago and this cycle's buys are done: watching for a second leg (a new {p.DrawdownPct:0.##}% fall from the recent high) without waiting for the old high.");
            }
        }
        else if (phase == "normal" && rearmed && price >= peak)
        {
            rearmed = false;
        }

        if (phase == "normal" && !rearmedToday && price <= high * (1 - p.DrawdownPct / 100))
        {
            phase = "drawdown";
            peak = high;
            low = price;
            age = 0;
            rearmed = false;
            reason = string.Create(CultureInfo.InvariantCulture,
                $"Fell {p.DrawdownPct:0.##}% or more from its recent high ({high:N2}).");
        }
        else if (phase == "drawdown")
        {
            low = Math.Min(low, price);
            if (price >= low * (1 + p.ReboundPct / 100))
            {
                phase = "accumulating";
                doneSteps = 0;
                sinceLast = 0;
            }
        }

        RuleStatus status;
        int? stepNo = null;
        if (phase == "normal")
        {
            status = RuleStatus.NotCandidate;
        }
        else if (phase == "drawdown")
        {
            status = RuleStatus.Watching;
        }
        else if (phase == "accumulating")
        {
            var due = doneSteps == 0 || sinceLast >= p.StepEveryDays;
            if (due)
            {
                doneSteps++;
                sinceLast = 0;
                stepNo = doneSteps <= count ? doneSteps : null;
                if (doneSteps >= count) phase = "done";
                status = RuleStatus.BuySignal;
            }
            else
            {
                status = RuleStatus.Building;
            }
        }
        else
        {
            status = RuleStatus.Holding;
        }

        var bounceLine = low > 0 ? low * (1 + p.ReboundPct / 100) : price;
        var belowHighPct = Math.Round(Math.Max(0, -Indicators.PctFrom(price, high)), 2);
        var belowDropPeakPct = peak > 0 ? Math.Round(Math.Max(0, -Indicators.PctFrom(price, peak)), 2) : belowHighPct;
        var aboveLowPct = low > 0 ? Math.Round(Math.Max(0, Indicators.PctFrom(price, low)), 2) : 0m;
        var facts = new Dictionary<string, object>
        {
            ["belowHighPct"] = belowHighPct,
            ["belowDropPeakPct"] = belowDropPeakPct,
            ["aboveLowPct"] = aboveLowPct,
            ["doneSteps"] = phase is "accumulating" or "done" || status == RuleStatus.BuySignal ? doneSteps : 0,
            // In a drawdown, how far the price still has to bounce to reach the first step ("close to the line").
            ["distancePct"] = phase == "drawdown"
                ? Math.Round(Math.Max(0, Indicators.PctFrom(bounceLine, price)), 2)
                : belowHighPct,
        };
        var windowOpen = status == RuleStatus.BuySignal
            || (phase is "accumulating" or "done" && price >= bounceLine);
        // In normal markets only the drop counts; in a drop, the bounce off its low; while buying, the pace of the steps.
        var inDrop = phase != "normal";
        List<RuleCheck> checks =
        [
            inDrop
                ? new("drop", CheckGroup.Candidate, true, belowDropPeakPct, p.DrawdownPct, Level: Math.Round(peak, 2))
                : new("drop", CheckGroup.Candidate, false, belowHighPct, p.DrawdownPct, Level: Math.Round(high, 2),
                    Progress: RuleCheck.ProgressOf(belowHighPct, p.DrawdownPct)),
        ];
        if (inDrop)
        {
            var bounced = phase != "drawdown";
            checks.Add(new("bounce", CheckGroup.Buy, bounced, aboveLowPct, p.ReboundPct, Level: Math.Round(bounceLine, 2),
                Progress: bounced ? null : RuleCheck.ProgressOf(aboveLowPct, p.ReboundPct)));
        }
        if (phase == "accumulating" || (phase == "done" && status == RuleStatus.BuySignal))
            checks.Add(new("pace", CheckGroup.Next, status == RuleStatus.BuySignal, sinceLast, p.StepEveryDays,
                Progress: status == RuleStatus.BuySignal ? null : RuleCheck.ProgressOf(sinceLast, p.StepEveryDays)));
        return new RuleDecision(status, reason, facts, windowOpen, stepNo) { Checks = checks };
    }
}
