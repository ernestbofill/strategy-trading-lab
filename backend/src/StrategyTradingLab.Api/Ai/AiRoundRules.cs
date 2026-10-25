using System.Text.Json;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Ai;

/// <summary>A stock a round gives a step: the rules' next step for it.</summary>
public record AiPick(Guid AssetId, int StepNo);

/// <summary>Why a round gave fewer steps than its top N: the budget is used up, or every stock left has all its steps.</summary>
public enum AiNoStep { Budget, AllSteps }

/// <summary>A score as a round keeps it: the stock as it was named then, and the step it got, if any.</summary>
public record AiRoundScore(Guid AssetId, string Symbol, string Name, decimal Overall, decimal? News, decimal? Mood, decimal? Figures,
    string Reason, int? Step);

/// <summary>
/// The rules of <i>Your AI's top picks</i> (spec: The templates, AI round): when a round is due and when it is skipped, and which
/// stocks its scores give a step. Pure: no database.
/// </summary>
public static class AiRoundRules
{
    /// <summary>A round can be run on its due day and the 6 days after (spec: within a week of its due date), then it is skipped.</summary>
    public const int OpenDays = 7;

    /// <summary>The strategy's AI params, with the defaults for anything missing.</summary>
    public static AiParams Params(Strategy strategy) =>
        strategy.Params.Deserialize<AiParams>(JsonSerializerOptions.Web) ?? new AiParams();

    /// <summary>The round after one due on <paramref name="dueOn"/>: <c>EveryWeeks</c> after its due date, whenever it was run.</summary>
    public static DateOnly NextDue(DateOnly dueOn, AiParams parameters) => dueOn.AddDays(7 * parameters.EveryWeeks);

    /// <summary>The last day a round due on <paramref name="dueOn"/> can be run: a week, or less when the next is due sooner.</summary>
    public static DateOnly LastDay(DateOnly dueOn, AiParams parameters) =>
        dueOn.AddDays(Math.Min(OpenDays, 7 * parameters.EveryWeeks) - 1);

    /// <summary>
    /// The stocks a round gives a step: from the top overall score down, each stock with steps left gets its next one, until
    /// <c>TopN</c> have one or the next step wouldn't fit in the budget. A tie goes to the stock with fewer steps, then by name.
    /// <paramref name="steps"/> are the steps the rules hold per stock (every buy signal, answered or not), <paramref name="used"/>
    /// what the strategy's signals use of the budget, and <paramref name="unavailable"/> stocks that can't get a signal now
    /// (no price yet, or already one on that day).
    /// </summary>
    public static (IReadOnlyList<AiPick> Picks, AiNoStep? Short) Pick(IReadOnlyList<AiScore> scores, IReadOnlyDictionary<Guid, int> steps,
        IReadOnlyDictionary<Guid, string> names, Budget budget, decimal used, int topN, IReadOnlySet<Guid> unavailable)
    {
        var step = budget.Step ?? 0;
        var maxSteps = budget.MaxSteps ?? 1;
        var picks = new List<AiPick>();
        var ordered = scores
            .OrderByDescending(s => s.Overall)
            .ThenBy(s => steps.GetValueOrDefault(s.AssetId))
            .ThenBy(s => names.GetValueOrDefault(s.AssetId) ?? "", StringComparer.OrdinalIgnoreCase);
        foreach (var score in ordered)
        {
            if (picks.Count == topN) break;
            var held = steps.GetValueOrDefault(score.AssetId);
            if (held >= maxSteps || unavailable.Contains(score.AssetId)) continue;
            if (step <= 0 || used + step > budget.Amount) return (picks, AiNoStep.Budget);
            used += step;
            picks.Add(new AiPick(score.AssetId, held + 1));
        }
        return (picks, picks.Count < topN ? AiNoStep.AllSteps : null);
    }

    /// <summary>
    /// The day after which a round's signal trades: its open is the first one after the round was run, so a round run while the
    /// market is open waits for the next day's (no buying at an open that had already passed). New York's date.
    /// </summary>
    public static DateOnly TradeAfter(DateTimeOffset now)
    {
        var newYork = TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById("America/New_York"));
        var day = DateOnly.FromDateTime(newYork.DateTime);
        return TimeOnly.FromDateTime(newYork.DateTime) >= new TimeOnly(9, 30) ? day : day.AddDays(-1);
    }
}
