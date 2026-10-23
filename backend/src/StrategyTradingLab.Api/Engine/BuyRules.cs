namespace StrategyTradingLab.Api.Engine;

/// <summary>Where one asset stands under a strategy's rules on one day (the POC's status words, buys only).</summary>
public enum RuleStatus { NotEnoughData, NotCandidate, Watching, BuySignal, SignalEnded, Building, Holding }

/// <summary>What a template's rules say on one day; the runner turns it into a status, steps and signals.</summary>
/// <param name="Candidate">Meets the candidate criteria today.</param>
/// <param name="EntryOk">The entry conditions hold (the buy window is open if it is also a candidate and not extended).</param>
/// <param name="Extended">The price ran too far past the buy point to chase.</param>
/// <param name="LostReason">Why the window closed because a condition was lost.</param>
/// <param name="ExtendedReason">Why it closed because the price ran away.</param>
/// <param name="Facts">The numbers behind the day, for the signal's text, e.g. <c>belowPeakPct</c>.</param>
public record RuleDay(bool Candidate, bool EntryOk, bool Extended, string LostReason, string ExtendedReason,
    IReadOnlyDictionary<string, decimal> Facts);

/// <summary>A template's buy rules, prepared over one asset's whole history. <see cref="At"/> only reads days up to <c>i</c>.</summary>
public interface IBuyRules
{
    /// <summary>The days of history the rules need before they say anything.</summary>
    int MinHistory { get; }

    /// <summary>Calendar days after a step is bought before the next buy signal.</summary>
    int CooldownDays { get; }

    RuleDay At(int i);
}

/// <summary>How many steps the budget allows per asset (<c>budget.maxSteps</c>).</summary>
public record StepPlan(int MaxSteps);

/// <summary>One day's result. <c>StepNo</c> is the step a buy signal is for; <c>NewBuy</c> is true on the day a signal starts.</summary>
public record DayEvaluation(DateOnly Date, RuleStatus Status, int? StepNo, bool NewBuy, string? Reason,
    IReadOnlyDictionary<string, decimal> Facts);
