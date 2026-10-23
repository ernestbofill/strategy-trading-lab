namespace StrategyTradingLab.Api.Engine;

/// <summary>
/// Runs a template's buy rules over an asset's history, day by day, as the POC's backtest does: a signal on day D is
/// acted on at day D+1's open, whatever the user does ("the rules carry on as if the step had been bought"), and the
/// cooldown counts from that day. Port of <c>Strategy.step</c> and <c>compose_status</c> without sells (the products' buy-only
/// templates have none). Pure: bars in, evaluations out.
/// </summary>
public static class RulesRunner
{
    static readonly IReadOnlyDictionary<string, decimal> NoFacts = new Dictionary<string, decimal>();

    public static IReadOnlyList<DayEvaluation> Run(IBuyRules rules, IReadOnlyList<PriceBar> bars, StepPlan plan)
    {
        if (rules.Length != bars.Count) throw new ArgumentException($"The rules cover {rules.Length} days but {bars.Count} bars were given.", nameof(bars));
        var results = new List<DayEvaluation>(bars.Count);
        var machine = new Machine(rules, plan);
        for (var i = 0; i < bars.Count; i++) results.Add(machine.Step(i, bars[i].Date));
        return results;
    }

    sealed class Machine(IBuyRules rules, StepPlan plan)
    {
        RuleStatus? previous;
        int? previousStep;
        bool windowOpen;
        DateOnly? windowStart, cooldownUntil;
        int stepsBought;
        DateOnly lastBuyDay;
        bool pendingBuy;

        public DayEvaluation Step(int i, DateOnly day)
        {
            // 1) Yesterday's signal is acted on at today's open: a step is bought, and the cooldown starts today.
            var actedNow = pendingBuy;
            if (pendingBuy)
            {
                stepsBought++;
                lastBuyDay = day;
                cooldownUntil = rules.CooldownDays > 0 ? day.AddDays(rules.CooldownDays) : null;
                pendingBuy = false;
            }

            // 2) Today's evaluation, at the close.
            if (i + 1 < rules.MinHistory)
            {
                previous = RuleStatus.NotEnoughData;
                previousStep = null;
                return new DayEvaluation(day, RuleStatus.NotEnoughData, null, false, null, NoFacts);
            }

            var rule = rules.At(i);
            var (status, reason) = Compose(day, rule, actedNow);
            int? step = status == RuleStatus.BuySignal ? Math.Clamp(stepsBought + 1, 1, plan.MaxSteps) : null;
            var newBuy = step is not null && (previous != RuleStatus.BuySignal || previousStep != step);
            previous = status;
            previousStep = step;
            if (newBuy) pendingBuy = true;
            return new DayEvaluation(day, status, step, newBuy, reason, rule.Facts);
        }

        bool Full => stepsBought >= plan.MaxSteps;

        (RuleStatus, string?) Compose(DateOnly day, RuleDay rule, bool answered)
        {
            var cooling = cooldownUntil is { } until && day < until;
            var open = rule.Candidate && rule.EntryOk && !rule.Extended;
            if (open && !windowOpen) windowStart = day;
            windowOpen = open;
            const string notCandidate = "It no longer meets the candidate criteria.";

            if (stepsBought > 0)
            {
                if (Full) return (RuleStatus.Holding, null);
                if (cooling || answered) return (RuleStatus.Building, null);
                var boughtInWindow = lastBuyDay >= (windowStart ?? day);
                if (open && !boughtInWindow) return (RuleStatus.BuySignal, null);
                if (previous == RuleStatus.BuySignal && !open && !boughtInWindow)
                {
                    // The window for the next step closed without buying it.
                    var closed = !rule.Candidate ? notCandidate : rule.EntryOk && rule.Extended ? rule.ExtendedReason : rule.LostReason;
                    return (RuleStatus.Building, closed);
                }
                return (RuleStatus.Building, null);
            }
            if (!rule.Candidate) return previous == RuleStatus.BuySignal && !answered ? (RuleStatus.SignalEnded, notCandidate) : (RuleStatus.NotCandidate, null);
            if (cooling || answered) return (RuleStatus.Watching, null);
            if (rule.EntryOk && rule.Extended)
            {
                // Only a window that actually opened can close. A jump straight past the buy point stays "watching".
                if (previous is RuleStatus.BuySignal or RuleStatus.SignalEnded) return (RuleStatus.SignalEnded, rule.ExtendedReason);
                return (RuleStatus.Watching, $"Jumped past the buy window without a buy signal. {rule.ExtendedReason}");
            }
            if (rule.EntryOk) return (RuleStatus.BuySignal, null);
            return previous == RuleStatus.BuySignal ? (RuleStatus.SignalEnded, rule.LostReason) : (RuleStatus.Watching, null);
        }
    }
}
