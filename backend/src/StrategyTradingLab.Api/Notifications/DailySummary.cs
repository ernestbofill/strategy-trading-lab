using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Admin;
using StrategyTradingLab.Api.Common;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Evaluation;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Notifications;

/// <summary>
/// What a user's summary holds (spec: Summary): waiting signals, the session's answered ones and stock changes, the AI rounds
/// due and each strategy's signal count. The summary email and the /summary page read the same, and the stock change pushes
/// use its stock changes.
/// </summary>
public class DailySummary(AppDbContext db, TemplateStore templates, TimeProvider time)
{
    /// <summary>The summary's window of earlier daily checks: stock changes compare against them.</summary>
    const int RecentDays = 4;

    public record Data(
        IReadOnlyList<SignalLine> Waiting,
        IReadOnlyList<SignalLine> Changed,
        IReadOnlyList<StockChangeLine> StockChanges,
        IReadOnlyList<ScoreLine> Scoreboard,
        IReadOnlyList<(Guid StrategyId, string Name)> AiRoundsDue,
        bool HasChanges,
        bool Complete);

    public async Task<Data> LoadAsync(Guid userId, DateOnly today, DateOnly sessionDate, CancellationToken cancellationToken, IReadOnlyList<Strategy>? running = null)
    {
        var strategies = running ?? await db.Strategies.AsNoTracking().Where(s => s.UserId == userId && s.Status == StrategyStatus.Running).ToListAsync(cancellationToken);
        var ids = strategies.Select(s => s.Id).ToList();
        if (ids.Count == 0)
            return new Data([], [], [], [], [], false, true);

        var signals = await (from signal in db.Signals.AsNoTracking()
                             join asset in db.Assets.AsNoTracking() on signal.AssetId equals asset.Id
                             where ids.Contains(signal.StrategyId) && (signal.Status == SignalStatus.Waiting || signal.SignalDate == sessionDate)
                             select new { signal, asset }).ToListAsync(cancellationToken);
        var byId = strategies.ToDictionary(s => s.Id);
        SignalLine ToLine(Signal s, Asset a) => SignalLine.From(s, byId[s.StrategyId], a);
        var waiting = signals.Where(x => x.signal.Status == SignalStatus.Waiting).Select(x => ToLine(x.signal, x.asset)).OrderBy(l => l.Date).ToList();
        var changed = signals.Where(x => x.signal.SignalDate == sessionDate && x.signal.Status != SignalStatus.Waiting).Select(x => ToLine(x.signal, x.asset)).ToList();

        var (recent, stockChanges) = await StockChangesAsync(byId, sessionDate, cancellationToken);
        var hasChanges = stockChanges.Count > 0 || signals.Any(x => x.signal.SignalDate == sessionDate);
        var complete = hasChanges || strategies.Where(s => s.StartedOn <= sessionDate
                && (s.StartedOn >= sessionDate.AddDays(-RecentDays) || recent.Any(x => x.StrategyId == s.Id)))
            .All(s => recent.Any(x => x.StrategyId == s.Id && x.Date == sessionDate));

        var roundsDue = await db.AiRounds.AsNoTracking().Where(r => ids.Contains(r.StrategyId) && r.Status == AiRoundStatus.Due && r.DueOn <= today)
            .OrderBy(r => r.DueOn).Select(r => new { r.StrategyId, Name = byId[r.StrategyId].Name }).ToListAsync(cancellationToken);
        var counts = await db.Signals.AsNoTracking().Where(s => ids.Contains(s.StrategyId)).GroupBy(s => s.StrategyId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);
        var scoreboard = strategies.OrderBy(s => s.Name).Select(s => new ScoreLine(s.Name, s.Mode, s.StartedOn, counts.GetValueOrDefault(s.Id), s.Id)).ToList();

        return new Data(waiting, changed, stockChanges, scoreboard, [.. roundsDue.Select(r => (r.StrategyId, r.Name))], hasChanges, complete);
    }

    /// <summary>
    /// Summary data for the dedicated daily summary page, up to date from the most recent price check session.
    /// </summary>
    public async Task<DailySummaryDto> ForPageAsync(Guid userId, string timeZone, string locale, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var lastSession = UsSession.LastComplete(now);
        var runningStrategies = await db.Strategies.AsNoTracking().Where(s => s.UserId == userId && s.Status == StrategyStatus.Running).ToListAsync(cancellationToken);
        if (runningStrategies.Count == 0)
            return new DailySummaryDto(lastSession, false, [], [], [], [], []);

        var ids = runningStrategies.Select(s => s.Id).ToList();
        var latestEval = await db.DailyEvaluations.AsNoTracking().Where(e => ids.Contains(e.StrategyId)).MaxAsync(e => (DateOnly?)e.Date, cancellationToken);
        var sessionDate = latestEval ?? lastSession;

        var today = UserClock.Today(timeZone, now);

        var data = await LoadAsync(userId, today, sessionDate, cancellationToken, runningStrategies);

        return new DailySummaryDto(
            sessionDate,
            data.HasChanges,
            [.. data.Waiting.Select(w => new SummaryWaitingSignal(w.SignalId, w.StrategyId, w.StrategyName, w.StockName, w.Side, w.Date, w.StepNo, w.MaxSteps, w.ClosePrice))],
            [.. data.AiRoundsDue.Select(r => new SummaryWaitingAiRound(r.StrategyId, r.Name))],
            [.. data.Changed.Select(c => new SummaryChangedSignal(c.SignalId, c.StrategyId, c.StrategyName, c.Mode, c.StockName, c.Side, c.Date, c.ClosePrice, c.StepNo, c.MaxSteps, EmailTexts.SignalText(c, locale).Replace("\n", " ")))],
            [.. data.StockChanges.Select(c => new SummaryStockChange(c.StrategyId, c.AssetId, c.StrategyName, c.Mode, c.StockName, c.Kind, c.DistancePct, EmailTexts.StockChangeText(c, locale)))],
            [.. data.Scoreboard.Select(s => new SummaryStrategyScore(s.StrategyId, s.Name, s.Mode, s.StartedOn, s.Signals))]);
    }

    /// <summary>
    /// The stock changes of <paramref name="strategies"/> on <paramref name="session"/>, for the summary and the stock change
    /// pushes alike, with the evaluations of the recent days they were found in.
    /// </summary>
    public async Task<(List<DailyEvaluation> Recent, List<StockChangeLine> Changes)> StockChangesAsync(Dictionary<Guid, Strategy> strategies,
        DateOnly session, CancellationToken cancellationToken)
    {
        var ids = strategies.Keys.ToList();
        var recent = await (from eval in db.DailyEvaluations.AsNoTracking()
                            join asset in db.Assets.AsNoTracking() on eval.AssetId equals asset.Id
                            where ids.Contains(eval.StrategyId) && eval.Date >= session.AddDays(-RecentDays) && eval.Date <= session
                            select new { eval, asset.Name }).ToListAsync(cancellationToken);
        var closePct = (await templates.AllAsync(cancellationToken)).ToDictionary(t => t.Key, t => t.ClosePct);
        return ([.. recent.Select(x => x.eval)], StockChangeLines(recent.Select(x => (x.eval, x.Name)), session, strategies, closePct));
    }

    /// <summary>
    /// The stock changes of every running strategy: each stock evaluated on the last session against its latest
    /// earlier evaluation of the recent days (see <see cref="StockChanges.Latest"/>). A stock without an earlier day (just added) has nothing to compare.
    /// A template the store no longer has uses the default getting-close %.
    /// </summary>
    static List<StockChangeLine> StockChangeLines(IEnumerable<(DailyEvaluation Eval, string Name)> recent, DateOnly lastSession, Dictionary<Guid, Strategy> strategies,
        Dictionary<string, decimal> closePct) =>
        recent.GroupBy(x => (x.Eval.StrategyId, x.Eval.AssetId))
            .Select(g => g.OrderByDescending(x => x.Eval.Date).ToList())
            .Where(days => days[0].Eval.Date == lastSession)
            .Select(days => (days, change: StockChanges.Latest([.. days.Select(d => (d.Eval.Status, d.Eval.Facts.RootElement))],
                closePct.GetValueOrDefault(strategies[days[0].Eval.StrategyId].TemplateKey, Template.DefaultClosePct))))
            .Where(x => x.change is not null)
            .Select(x =>
            {
                var strategy = strategies[x.days[0].Eval.StrategyId];
                return new StockChangeLine(strategy.Id, x.days[0].Eval.AssetId, strategy.Name, strategy.Mode, strategy.TemplateKey, x.days[0].Name, x.change!.Value.Kind, x.change.Value.DistancePct);
            })
            .OrderBy(c => c.StrategyName).ThenBy(c => c.StockName).ToList();
}
