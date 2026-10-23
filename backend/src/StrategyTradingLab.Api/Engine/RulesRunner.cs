namespace StrategyTradingLab.Api.Engine;

/// <summary>
/// Runs a template's buy rules over an asset's history, day by day, as the POC's backtest does: a signal on day D is
/// acted on at day D+1's open, whatever the user does ("the rules carry on as if the step had been bought"), and the
/// cooldown counts from that day. The rules decide each day's status (<see cref="IBuyRules"/>); this keeps the rules' own
/// position, the cooldown and the step numbers, as the POC's <c>Strategy.step</c> does. Pure: bars in, evaluations out.
/// </summary>
public static class RulesRunner
{
    static readonly IReadOnlyDictionary<string, object> NoFacts = new Dictionary<string, object>();

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
        DateOnly? cooldownUntil;
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

            var context = new DayContext(day, previous, stepsBought, lastBuyDay, actedNow,
                Cooling: cooldownUntil is { } until && day < until, Full: stepsBought >= plan.MaxSteps);
            var decision = rules.Decide(i, context);
            int? step = decision.Status == RuleStatus.BuySignal ? Math.Clamp(stepsBought + 1, 1, plan.MaxSteps) : null;
            var newBuy = step is not null && (previous != RuleStatus.BuySignal || previousStep != step);
            previous = decision.Status;
            previousStep = step;
            if (newBuy) pendingBuy = true;
            return new DayEvaluation(day, decision.Status, step, newBuy, decision.Reason, decision.Facts);
        }
    }
}
