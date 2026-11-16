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
    /// Locks the strategy's row until the transaction ends, and returns the stocks it follows while it runs (none once it ended).
    /// Read under the lock, so an Edit list save or an archive made since the strategy was loaded waits for the caller or wins,
    /// and gets no new signal.
    /// </summary>
    public static Task<List<Guid>> LockFollowedAsync(this AppDbContext db, Guid strategyId, CancellationToken cancellationToken) =>
        db.Database.SqlQuery<Guid>($"""
            SELECT a.asset_id AS "Value" FROM strategies s JOIN strategy_assets a ON a.strategy_id = s.id
            WHERE s.id = {strategyId} AND s.status = 'running' AND a.active AND a.removed_at IS NULL
            FOR NO KEY UPDATE OF s
            """).ToListAsync(cancellationToken);

    /// <summary>Stores where a stock stands on a day, replacing the row of that day if there is one. Saved with the caller's next save.</summary>
    public static async Task SaveDayAsync(this AppDbContext db, DailyEvaluation day, CancellationToken cancellationToken)
    {
        var row = await db.DailyEvaluations.FindAsync([day.StrategyId, day.AssetId, day.Date], cancellationToken);
        if (row is null)
        {
            db.DailyEvaluations.Add(day);
            return;
        }
        row.Status = day.Status;
        row.StepNo = day.StepNo;
        row.Facts = day.Facts;
    }
}
