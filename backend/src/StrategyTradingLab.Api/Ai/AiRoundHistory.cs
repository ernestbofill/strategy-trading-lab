using System.Text.Json.Nodes;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Auth;
using StrategyTradingLab.Api.Common;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Evaluation;
using StrategyTradingLab.Api.MarketData;
using StrategyTradingLab.Api.Signals;
using StrategyTradingLab.Api.Strategies;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Ai;

/// <summary>The AI rounds' read side: the history, the strategy page's summary, and the last scores the stock page shows.</summary>
public partial class AiRounds
{
    /// <summary>Each stock's overall score in the strategy's latest done round that scored it.</summary>
    public async Task<Dictionary<Guid, decimal>> LastScoresAsync(Guid strategyId, CancellationToken cancellationToken) =>
        LastScores(await db.AiRounds.AsNoTracking().Where(r => r.StrategyId == strategyId && r.Status == AiRoundStatus.Done).ToListAsync(cancellationToken));

    static Dictionary<Guid, decimal> LastScores(IEnumerable<AiRound> rounds)
    {
        var last = new Dictionary<Guid, decimal>();
        foreach (var round in rounds.Where(r => r.Status == AiRoundStatus.Done).OrderByDescending(r => r.DoneAt))
            foreach (var score in Read(round.Scores))
                last.TryAdd(score.AssetId, score.Overall);
        return last;
    }

    public static IReadOnlyList<AiRoundScore> Read(JsonDocument? scores) =>
        scores?.Deserialize<List<AiRoundScore>>(JsonSerializerOptions.Web) ?? [];

    /// <summary>
    /// The history: every round, newest first, and each done round's picks against the rest: the average change of their
    /// prices from the round's session close to the latest close, in each stock's own currency.
    /// </summary>
    public async Task<(List<AiRoundItem> Rounds, PicksVsRest? Total)> HistoryAsync(Strategy strategy, CancellationToken cancellationToken)
    {
        var rounds = await db.AiRounds.AsNoTracking().Where(r => r.StrategyId == strategy.Id).OrderByDescending(r => r.DueOn).ToListAsync(cancellationToken);
        var changes = await ChangesAsync(rounds, cancellationToken);
        var items = rounds.Select(r =>
        {
            var scores = Read(r.Scores);
            var (picks, rest) = changes.GetValueOrDefault(r.Id);
            return new AiRoundItem(r.Id, r.DueOn, r.Extra, r.Status, r.DoneAt, r.SessionDate, r.AiModel, r.PromptVersion, r.PromptEdited, r.PromptText, r.Answer,
                [.. scores.Select(s => new AiScoreDto(s.AssetId, s.Symbol, s.Name, s.Overall, s.News, s.Mood, s.Figures, s.Reason, s.Step))], picks, rest);
        }).ToList();
        return (items, Total(changes.Values));
    }

    /// <summary>The strategy page's line (the next round and the picks against the rest), and each stock's last score.</summary>
    public async Task<(AiRoundsSummary Summary, Dictionary<Guid, decimal> LastScores)> SummaryAsync(Strategy strategy, DateOnly today,
        CancellationToken cancellationToken)
    {
        if (strategy.Status == StrategyStatus.Running) await SweepAsync([new(strategy, today)], cancellationToken);
        var rounds = await db.AiRounds.AsNoTracking().Where(r => r.StrategyId == strategy.Id).ToListAsync(cancellationToken);
        // A round of an ended strategy is never due.
        var current = strategy.Status == StrategyStatus.Running ? rounds.FirstOrDefault(r => r.Status == AiRoundStatus.Due) : null;
        var changes = await ChangesAsync(rounds, cancellationToken);
        var summary = new AiRoundsSummary(current?.DueOn, current is not null && current.DueOn <= today, rounds.Count(r => r.Status == AiRoundStatus.Done),
            rounds.Count(r => r.Status == AiRoundStatus.Skipped), Total(changes.Values));
        return (summary, LastScores(rounds));
    }

    static PicksVsRest? Total(IEnumerable<(decimal? Picks, decimal? Others)> changes)
    {
        var both = changes.Where(c => c.Picks is not null && c.Others is not null).ToList();
        return both.Count == 0 ? null
            : new PicksVsRest(Math.Round(both.Average(c => c.Picks!.Value), 1), Math.Round(both.Average(c => c.Others!.Value), 1), both.Count);
    }

    async Task<Dictionary<Guid, (decimal? Picks, decimal? Others)>> ChangesAsync(IReadOnlyList<AiRound> rounds, CancellationToken cancellationToken)
    {
        var done = rounds.Where(r => r.Status == AiRoundStatus.Done && r.SessionDate is not null).Select(r => (r.Id, Session: r.SessionDate!.Value, Scores: Read(r.Scores))).ToList();
        if (done.Count == 0) return [];
        // Two closes per stock and round: its last close on or before the round's session, and its latest.
        var ids = done.SelectMany(r => r.Scores.Select(s => s.AssetId)).Distinct().ToList();
        var latest = await LastClosesAsync(ids, null, cancellationToken);
        var atSession = new Dictionary<DateOnly, Dictionary<Guid, (DateOnly Date, decimal Close)>>();
        foreach (var session in done.Select(r => r.Session).Distinct())
            atSession[session] = await LastClosesAsync([.. done.Where(r => r.Session == session).SelectMany(r => r.Scores.Select(s => s.AssetId)).Distinct()],
                session, cancellationToken);

        decimal? Change(Guid assetId, DateOnly session)
        {
            if (!atSession[session].TryGetValue(assetId, out var then) || !latest.TryGetValue(assetId, out var now)) return null;
            if (now.Date <= then.Date) return null; // No close since the round: nothing to compare yet.
            return (now.Close / then.Close - 1) * 100;
        }
        decimal? Average(IEnumerable<decimal?> values) => values.Where(v => v is not null).Select(v => v!.Value).ToList() is { Count: > 0 } list
            ? Math.Round(list.Average(), 1) : null;

        return done.ToDictionary(r => r.Id, r => (
            Average(r.Scores.Where(s => s.Step is not null).Select(s => Change(s.AssetId, r.Session))),
            Average(r.Scores.Where(s => s.Step is null).Select(s => Change(s.AssetId, r.Session)))));
    }

    /// <summary>Each asset's last close on or before <paramref name="on"/> (the latest when null), within the look-back.</summary>
    async Task<Dictionary<Guid, (DateOnly Date, decimal Close)>> LastClosesAsync(List<Guid> ids, DateOnly? on, CancellationToken cancellationToken)
    {
        var rows = await db.Prices.AsNoTracking().Where(p => ids.Contains(p.AssetId) && (on == null || p.Date <= on))
            .GroupBy(p => p.AssetId)
            .Select(g => g.OrderByDescending(p => p.Date).Select(p => new { p.AssetId, p.Date, p.Close }).First())
            .ToListAsync(cancellationToken);
        return rows.Where(r => on is null || r.Date >= on.Value.AddDays(-ExchangeRates.LookBackDays)).ToDictionary(r => r.AssetId, r => (r.Date, r.Close));
    }
}
