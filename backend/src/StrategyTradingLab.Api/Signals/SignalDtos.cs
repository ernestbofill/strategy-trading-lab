using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Signals;

// The DTOs of Confirm a signal (frontend/src/api/types.ts; camelCase JSON, enums as camelCase strings).

/// <summary>
/// What the form starts with: the next trading day after the signal, its opening price once it is stored (else null, and the
/// screen shows the signal day's close as a reference), and the step minus the tested fee (the step includes its fee).
/// </summary>
public record TradePrefill(DateOnly Date, decimal? Price, decimal? Amount);

/// <summary>The reference rate of a day: units of the asset's currency per one unit of the strategy's.</summary>
public record RateDay(DateOnly Date, decimal Rate);

/// <summary>How the user answered: the trade they saved, or the skip and its reason.</summary>
public record SignalAnswer(SignalStatus Status, SkipReason? SkipReason, DateOnly? Date, decimal? Price, decimal? Amount, decimal? Shares, decimal? Fee);

/// <summary>
/// Confirm a signal. <c>Reason</c> is what the rules found, in words ("8% below its recent peak, still above its 50-day
/// average"). <c>Rates</c> are the reference rates from a week before the signal to today, for the rate line to follow the date.
/// </summary>
public record SignalDetail(
    Guid Id,
    Guid StrategyId,
    string StrategyName,
    string TemplateName,
    string StrategyCurrency,
    string StockName,
    string AssetCurrency,
    int? StepNo,
    int? MaxSteps,
    decimal? StepAmount,
    DateOnly SignalDate,
    decimal ClosePrice,
    string Reason,
    SignalStatus Status,
    DateOnly? SnoozedUntil,
    DateOnly? EndedOn,
    DateOnly Today,
    TradePrefill Prefill,
    IReadOnlyList<RateDay> Rates,
    SignalAnswer? Answer);

/// <summary>
/// "I did it". <c>EnteredAs</c> says which of <c>Amount</c> and <c>Shares</c> the user typed; the other is worked out.
/// <c>FxRate</c> is only read when <c>FxSource</c> is user: a reference rate is looked up for the date.
/// </summary>
public record SignalTradeRequest(DateOnly? Date, decimal? Price, EnteredAs? EnteredAs, decimal? Amount, decimal? Shares, decimal? Fee,
    FxSource? FxSource, decimal? FxRate);

/// <summary>"I skipped it", with an optional reason.</summary>
public record SignalSkipRequest(SkipReason? Reason);

/// <summary>A snooze: the day the user is reminded.</summary>
public record SnoozeResult(DateOnly SnoozedUntil);
