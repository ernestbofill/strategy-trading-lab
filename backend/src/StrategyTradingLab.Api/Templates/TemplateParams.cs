using System.ComponentModel.DataAnnotations;

namespace StrategyTradingLab.Api.Templates;

// What a strategy stores in strategies.params, one record per template (camelCase JSON).
// Defaults and ranges are the POC's (trading-strategies-bot, strategies/*.py). Left out on
// purpose: the stop-loss and trailing stop (not in the product), the "getting close" alert
// threshold and the backtest's baseline. Day counts are trading days unless they say otherwise.

/// <summary>Dip in winners (POC Leader pullback, <c>stage2_pullback</c>).</summary>
public record DipParams : IValidatableObject
{
    /// <summary>Proven growth: rose at least this % from its low.</summary>
    [Range(10, 500)] public decimal MinGainPct { get; init; } = 50;

    /// <summary>The low is measured over this many days (~8 months).</summary>
    [Range(60, 252)] public int GainDays { get; init; } = 168;

    /// <summary>Recent peak: the highest close of this many days.</summary>
    [Range(10, 120)] public int PeakDays { get; init; } = 60;

    /// <summary>The discount zone starts this % below the recent peak.</summary>
    [Range(0.5, 30)] public decimal ZoneStartPct { get; init; } = 5;

    /// <summary>The discount zone ends this % below the recent peak.</summary>
    [Range(2, 40)] public decimal ZoneEndPct { get; init; } = 15;

    /// <summary>The long trend: SMA of this many days, which must be rising.</summary>
    [Range(100, 300)] public int TrendDays { get; init; } = 200;

    /// <summary>The floor a healthy dip stays above: SMA of this many days.</summary>
    [Range(20, 100)] public int FloorDays { get; init; } = 50;

    /// <summary>Calendar days after a step before the next buy signal.</summary>
    [Range(0, 120)] public int CooldownDays { get; init; } = 20;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ZoneStartPct >= ZoneEndPct)
        {
            yield return new ValidationResult("The discount zone must start before it ends.",
                [nameof(ZoneStartPct), nameof(ZoneEndPct)]);
        }
    }
}

/// <summary>The comeback (POC Confirmed turnaround, <c>turnaround</c>).</summary>
public record ComebackParams
{
    /// <summary>Beaten down: at least this % below the 52-week high.</summary>
    [Range(5, 90)] public decimal DrawdownPct { get; init; } = 30;

    /// <summary>Beaten down also needs the price below its SMA 200.</summary>
    public bool BelowSma200 { get; init; } = true;

    /// <summary>Stays a candidate for this many days after it was last beaten down.</summary>
    [Range(0, 250)] public int CandidateDays { get; init; } = 60;

    /// <summary>The signal line: this % above the SMA 50.</summary>
    [Range(0, 50)] public decimal AboveSma50Pct { get; init; } = 15;

    /// <summary>The 5-day average volume is at least this multiple of the 50-day.</summary>
    [Range(1, 5)] public decimal VolumeRatio { get; init; } = 1.2m;

    /// <summary>A step only up to this % above the signal line or a milestone's level.</summary>
    [Range(0, 30)] public decimal MaxChasePct { get; init; } = 8;

    /// <summary>Milestone "near the old high": within this % of the 52-week high.</summary>
    [Range(1, 50)] public decimal NearHighPct { get; init; } = 15;

    /// <summary>Milestone "breakout": a pullback of at least this %, then a close above the high before it.</summary>
    [Range(2, 30)] public decimal BreakoutPullbackPct { get; init; } = 8;

    /// <summary>Calendar days after a step before the next buy signal.</summary>
    [Range(0, 120)] public int CooldownDays { get; init; } = 5;
}

/// <summary>The big market drop (POC Bear-market accumulation, <c>bear_accumulation</c>).</summary>
public record BearParams
{
    /// <summary>A bear market: this % below the recent high.</summary>
    [Range(5, 60)] public decimal DrawdownPct { get; init; } = 20;

    /// <summary>Recent high: the highest close of this many days (~6 months).</summary>
    [Range(20, 252)] public int HighDays { get; init; } = 126;

    /// <summary>The first step: this % above the lowest close since the drop.</summary>
    [Range(1, 30)] public decimal ReboundPct { get; init; } = 5;

    /// <summary>Days between steps (10 ≈ 2 weeks).</summary>
    [Range(1, 60)] public int StepEveryDays { get; init; } = 10;

    /// <summary>
    /// Once the cycle's steps are done and the drop is older than the recent-high period, a new
    /// fall from the recent high starts a new cycle without waiting for the old high.
    /// </summary>
    public bool Rearm { get; init; } = true;
}

/// <summary>Ride the long trend (POC Index trend, <c>index_trend</c>). All in, all out.</summary>
public record TrendParams
{
    /// <summary>The trend line: SMA of this many days.</summary>
    [Range(50, 300)] public int SmaDays { get; init; } = 200;

    /// <summary>Buy when the close is more than this % above the trend line, sell when this % below.</summary>
    [Range(0, 10)] public decimal BandPct { get; init; } = 3;

    /// <summary>Calendar days after a signal before the next one.</summary>
    [Range(0, 120)] public int CooldownDays { get; init; } = 5;
}

/// <summary>Your AI's top picks (new, no POC rules).</summary>
public record AiParams
{
    /// <summary>An AI round every this many weeks.</summary>
    [Range(1, 8)] public int EveryWeeks { get; init; } = 2;

    /// <summary>How many top-scored stocks get a step per round.</summary>
    [Range(1, 5)] public int TopN { get; init; } = 1;
}
