using System.Text.Json;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Engine;
using StrategyTradingLab.Api.Evaluation;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Strategies;

/// <summary>Where each stock of a strategy stands (spec: Strategy page), for the strategy page and the dashboard card's counts.</summary>
public static class StockStates
{
    /// <summary>A running strategy's followed stocks counted by pill, for its dashboard card.</summary>
    public static StockCounts CountsOf(Strategy strategy, Template template, StrategyBook book, IEnumerable<Signal> signals,
        IReadOnlyDictionary<(Guid, Guid), DailyEvaluation> latest, IReadOnlyDictionary<Guid, decimal>? aiScores)
    {
        var you = book.You(strategy);
        var own = signals.ToList();
        var states = strategy.Assets.Where(a => a.IsFollowed).Select(a => StockStateOf(strategy, a.AssetId, false,
            latest.GetValueOrDefault((strategy.Id, a.AssetId)), [.. own.Where(s => s.AssetId == a.AssetId)], [.. you.Where(b => b.AssetId == a.AssetId)], book, aiScores))
            .ToList();
        bool Close(StockState s) => IsClose(strategy.TemplateKey, template.ClosePct, s.Status, s.Distance);
        return new StockCounts(
            states.Count(s => s.Status is StockStatus.WaitingForYou or StockStatus.Signal),
            states.Count(Close),
            states.Count(s => s.Status is StockStatus.Watching or StockStatus.OutOfTheMarket or StockStatus.NoBudget && !Close(s)),
            states.Count(s => s.Status is StockStatus.Holding or StockStatus.Building or StockStatus.Done),
            states.Count(s => s.Status == StockStatus.NotCandidate));
    }

    /// <summary>
    /// Getting close: a candidate within <paramref name="closePct"/> of its buy rule. Bear markets' distance is above the low,
    /// not from a buy rule, so it is never close.
    /// </summary>
    public static bool IsClose(string templateKey, decimal closePct, StockStatus status, decimal? distance) =>
        status is StockStatus.Watching or StockStatus.OutOfTheMarket && templateKey != TemplateKeys.Bear
        && distance is > 0 && distance <= closePct;

    /// <summary>
    /// A buy rule the price went past (the comeback's chase limit, past the dip's zone): too late for this signal. A price
    /// window (a check with a level) unmet at its rounded bound is just past it: 18.04% is sent as 18% (the stock page's isPastMax).
    /// </summary>
    public static bool PastBuyRule(IReadOnlyList<RuleCheck> checks) =>
        checks.Any(c => c.Group == CheckGroup.Buy && c.Met == false && c.Value is { } value && c.Max is { } max
            && (value > max || (value == max && c.Level is not null)));

    /// <summary>Where a stock stands today: its pill, the steps and signal behind it, and how far the price is from the rules.</summary>
    public record StockState(StockStatus Status, int RulesSteps, int UserSteps, decimal? Distance, Signal? Signal, IReadOnlyList<RuleCheck> Checks);

    /// <summary>
    /// A stock's state from its latest evaluation, its signals and the trades the results count (spec: Strategy page). For an AI
    /// strategy, <paramref name="aiScores"/> holds the last round's scores (null for other templates).
    /// </summary>
    public static StockState StockStateOf(Strategy strategy, Guid assetId, bool removed, DailyEvaluation? evaluation, IReadOnlyList<Signal> own,
        IReadOnlyList<Buy> trades, StrategyBook book, IReadOnlyDictionary<Guid, decimal>? aiScores)
    {
        var live = strategy.Mode == StrategyMode.Live;
        var maxSteps = strategy.Budget.MaxSteps;
        var waiting = own.Where(s => s.Status == SignalStatus.Waiting).OrderBy(s => s.SignalDate).FirstOrDefault();
        // A simulation's signal of the latest checked day is a SIGNAL until its own trade is in: the next open's, or at once for an
        // AI round (spec §5, F-007). Of several that day, the one still waiting.
        var todays = evaluation is null ? null : own.FirstOrDefault(s => s.SignalDate == evaluation.Date && !book.HasTrade(s.Id));
        var (doneSteps, cycleStart) = strategy.TemplateKey == TemplateKeys.Bear ? StrategyPage.BearCycle(evaluation?.Facts.RootElement) : default;
        var buySignals = own.Count(s => s.Side == TradeSide.Buy && (cycleStart is null || s.SignalDate >= cycleStart));
        int rulesSteps;
        if (maxSteps is null)
        {
            var lastSignal = own.OrderBy(s => s.SignalDate).ThenBy(s => s.CreatedAt).LastOrDefault();
            rulesSteps = lastSignal?.Side == TradeSide.Buy ? 1 : 0;
        }
        else
        {
            rulesSteps = doneSteps is { } done ? Math.Min(done, buySignals) : buySignals;
        }
        var userSteps = maxSteps is null ? (Performance.Used(trades) > 0 ? 1 : 0)
            : strategy.TemplateKey == TemplateKeys.Bear && !live ? rulesSteps
            : doneSteps == 0 ? 0
            : trades.Count(b => b.Side == TradeSide.Buy && (cycleStart is null || b.Date >= cycleStart));

        // Before an AI strategy's daily check has a verdict: watching once a round scored the stock, else not scored yet.
        var latestStatus = evaluation?.Status
            ?? (aiScores is null ? null : aiScores.ContainsKey(assetId) ? EvaluationStatus.Watching : EvaluationStatus.NotCandidate);
        var checks = evaluation is null ? [] : ChecksOf(evaluation.Facts);
        var status = removed ? StockStatus.Removed : StrategyPage.WithCandidacy(
            StrategyPage.Status(strategy.Mode, latestStatus, waiting is not null, todays is not null, rulesSteps, maxSteps, strategy.TemplateKey, userSteps), checks);
        var signal = status switch { StockStatus.WaitingForYou => waiting, StockStatus.Signal => todays, _ => null };
        var distanceKey = strategy.TemplateKey == TemplateKeys.Bear && evaluation?.Status == EvaluationStatus.Watching ? "aboveLowPct" : "distancePct";
        var distance = evaluation?.Facts.RootElement.Number(distanceKey);
        return new StockState(status, rulesSteps, userSteps, distance, signal, checks);
    }

    /// <summary>
    /// The latest check's Manual buy signal is answered (done or skipped): the rules count its step as bought at the next open, so
    /// the page shows where they stand after it, its next steps and cooldown, without waiting for the next check. Null otherwise.
    /// </summary>
    public static IReadOnlyList<RuleCheck>? ChecksAfterBuy(DailyEvaluation? evaluation, IReadOnlyList<Signal> own) =>
        evaluation is not null && own.Any(s => s.Side == TradeSide.Buy && s.SignalDate == evaluation.Date
                && s.Status is SignalStatus.Confirmed or SignalStatus.Skipped)
            && ChecksOf(evaluation.Facts, StrategyEvaluator.ChecksAfterBuyKey) is { Count: > 0 } afterBuy
            ? afterBuy : null;

    /// <summary>The rule checks a daily evaluation stored under <paramref name="key"/> (none before they were stored).</summary>
    public static IReadOnlyList<RuleCheck> ChecksOf(JsonDocument facts, string key = StrategyEvaluator.ChecksKey) =>
        facts.RootElement.ValueKind == JsonValueKind.Object && facts.RootElement.TryGetProperty(key, out var checks)
            && checks.ValueKind == JsonValueKind.Array
            ? checks.Deserialize<List<RuleCheck>>(JsonSerializerOptions.Web) ?? []
            : [];
}
