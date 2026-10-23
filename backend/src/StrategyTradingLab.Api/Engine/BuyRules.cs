namespace StrategyTradingLab.Api.Engine;

/// <summary>Where one asset stands under a strategy's rules on one day (the POC's status words, buys only).</summary>
public enum RuleStatus { NotEnoughData, NotCandidate, Watching, BuySignal, SignalEnded, Building, Holding }

/// <summary>What the runner tells the rules about the rules' own position and the day before.</summary>
/// <param name="Previous">Yesterday's status, null on the first day.</param>
/// <param name="StepsBought">Steps the rules have bought so far (each signal is acted on the next day).</param>
/// <param name="LastBuyDay">The day of the latest step; meaningless while nothing is bought.</param>
/// <param name="ActedNow">A step was bought today (at today's open).</param>
/// <param name="Cooling">The cooldown after the latest step is still running today.</param>
/// <param name="Full">Every step of the budget is bought.</param>
public readonly record struct DayContext(DateOnly Day, RuleStatus? Previous, int StepsBought, DateOnly LastBuyDay, bool ActedNow,
    bool Cooling, bool Full)
{
    public bool HasPosition => StepsBought > 0;
}

/// <summary>A day's verdict. <c>Facts</c> are the numbers (and words) behind it, for the signal's text, e.g. <c>belowPeakPct</c>.</summary>
public record RuleDecision(RuleStatus Status, string? Reason, IReadOnlyDictionary<string, object> Facts);

/// <summary>
/// A template's buy rules, prepared over one asset's whole history, for one run: some rules keep state between days, so
/// build new ones for each <see cref="RulesRunner.Run"/>. <see cref="Decide"/> only reads days up to <c>i</c>.
/// </summary>
public interface IBuyRules
{
    /// <summary>The days of history the rules need before they say anything.</summary>
    int MinHistory { get; }

    /// <summary>Calendar days after a step is bought before the next buy signal.</summary>
    int CooldownDays { get; }

    /// <summary>The number of days the rules were prepared over: <see cref="RulesRunner.Run"/> checks it against its bars.</summary>
    int Length { get; }

    RuleDecision Decide(int i, DayContext context);
}

/// <summary>How many steps the budget allows per asset (<c>budget.maxSteps</c>).</summary>
public record StepPlan(int MaxSteps);

/// <summary>One day's result. <c>StepNo</c> is the step a buy signal is for; <c>NewBuy</c> is true on the day a signal starts.</summary>
public record DayEvaluation(DateOnly Date, RuleStatus Status, int? StepNo, bool NewBuy, string? Reason,
    IReadOnlyDictionary<string, object> Facts);
