using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Engine;

namespace StrategyTradingLab.Api.Evaluation;

/// <summary>The reads and writes the daily check, the AI round and the stock page's chart share.</summary>
public static class EvaluationData
{
    /// <summary>
    /// A stock's stored prices up to <paramref name="lastSession"/>, oldest first: finished sessions only, as the daily check reads
    /// them (a bar refreshed during the session isn't final).
    /// </summary>
    public static Task<List<PriceBar>> FinishedBarsAsync(this AppDbContext db, Guid assetId, DateOnly lastSession, CancellationToken cancellationToken) =>
        db.Prices.AsNoTracking().Where(p => p.AssetId == assetId && p.Date <= lastSession).OrderBy(p => p.Date)
            .Select(p => new PriceBar(p.Date, p.Open, p.High, p.Low, p.Close, p.Volume)).ToListAsync(cancellationToken);

    /// <summary>
    /// Locks the strategy's row until the transaction ends, and returns the stocks it follows while it runs (none once it ended),
    /// or with <paramref name="assetId"/> only that one if it still follows it. Read under the lock, so an Edit list save or an
    /// archive made since the strategy was loaded waits for the caller or wins, and gets no new signal.
    /// </summary>
    public static Task<List<Guid>> LockFollowedAsync(this AppDbContext db, Guid strategyId, CancellationToken cancellationToken, Guid? assetId = null) =>
        db.Database.SqlQuery<Guid>($"""
            SELECT a.asset_id AS "Value" FROM strategies s JOIN strategy_assets a ON a.strategy_id = s.id
            WHERE s.id = {strategyId} AND s.status = 'running' AND a.active AND a.removed_at IS NULL
                AND ({assetId}::uuid IS NULL OR a.asset_id = {assetId})
            FOR NO KEY UPDATE OF s
            """).ToListAsync(cancellationToken);

    /// <summary>Stores where a stock stands on a day, replacing the row of that day if there is one. Saved with the caller's next save.</summary>
    public static async Task SaveDayAsync(this AppDbContext db, Guid strategyId, Guid assetId, DateOnly date, EvaluationStatus status, int? stepNo,
        JsonDocument facts, CancellationToken cancellationToken)
    {
        var row = await db.DailyEvaluations.FindAsync([strategyId, assetId, date], cancellationToken);
        if (row is null)
        {
            db.DailyEvaluations.Add(new DailyEvaluation { StrategyId = strategyId, AssetId = assetId, Date = date, Status = status, StepNo = stepNo, Facts = facts });
            return;
        }
        row.Status = status;
        row.StepNo = stepNo;
        row.Facts = facts;
    }
}
