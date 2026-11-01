using System.Text.Json;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Engine;
using StrategyTradingLab.Api.Evaluation;

namespace StrategyTradingLab.Api.Notifications;

/// <summary>How a stock's place in the rules changed from one daily check to the next (spec: Notifications, Stock changes).</summary>
public enum StockChangeKind { BecameCandidate, NoLongerCandidate, CameClose }

/// <summary>A stock change as the overview words it.</summary>
public record StockChangeLine(Guid StrategyId, Guid AssetId, string StrategyName, StrategyMode Mode, string TemplateKey, string StockName, StockChangeKind Kind, decimal? DistancePct = null);

/// <summary>
/// Compares a stock's evaluation on the last session with the one before it, for the strategies whose owner asked for these
/// lines. Pure: two evaluations in, at most one change out.
/// </summary>
public static class StockChanges
{
    /// <summary>"Close": the stock is a candidate and within this many percent of its signal's line.</summary>
    public const decimal ClosePct = 3m;

    /// <summary>Whether the rules watch the stock: true or false, or null when it says nothing about that (a signal, steps held, too little history).</summary>
    public static bool? Candidate(EvaluationStatus status, JsonElement facts)
    {
        var candidateFailed = facts.ValueKind == JsonValueKind.Object && facts.TryGetProperty(StrategyEvaluator.ChecksKey, out var checks)
            && checks.ValueKind == JsonValueKind.Array
            && checks.EnumerateArray().Any(c => c.TryGetProperty("group", out var g) && g.GetString() == CheckGroup.Candidate
                && c.TryGetProperty("met", out var met) && met.ValueKind == JsonValueKind.False);
        return status switch
        {
            EvaluationStatus.NotCandidate => false,
            EvaluationStatus.Watching => !candidateFailed,
            // A signal that ended because the stock stopped being a candidate (the stock page reads it the same way).
            EvaluationStatus.SignalEnded when candidateFailed => false,
            _ => null,
        };
    }

    /// <summary>The distance to the signal's line, when the evaluation has one above zero.</summary>
    public static decimal? Distance(JsonElement facts) =>
        facts.ValueKind == JsonValueKind.Object && facts.TryGetProperty("distancePct", out var d) && d.ValueKind == JsonValueKind.Number && d.GetDecimal() is > 0 and var value
            ? value : null;

    /// <summary>
    /// What changed between <paramref name="before"/> and <paramref name="now"/>, or null. Becoming a candidate wins over coming
    /// close, and a stock already close the day before isn't reported again.
    /// </summary>
    public static (StockChangeKind Kind, decimal? DistancePct)? Between((EvaluationStatus Status, JsonElement Facts) before, (EvaluationStatus Status, JsonElement Facts) now)
    {
        var was = Candidate(before.Status, before.Facts);
        var isNow = Candidate(now.Status, now.Facts);
        if (was is null || isNow is null) return null;
        if (was == false && isNow == true) return (StockChangeKind.BecameCandidate, null);
        if (was == true && isNow == false) return (StockChangeKind.NoLongerCandidate, null);
        if (isNow == false) return null;
        var distance = Distance(now.Facts);
        if (distance is not { } close || close > ClosePct) return null;
        return Distance(before.Facts) is { } earlier && earlier <= ClosePct ? null : (StockChangeKind.CameClose, close);
    }
}
