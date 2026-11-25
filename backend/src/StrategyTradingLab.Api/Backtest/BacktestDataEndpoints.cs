using Microsoft.AspNetCore.Http.HttpResults;

namespace StrategyTradingLab.Api.Backtest;

/// <summary>The membership files the API holds: their hash, fja's last day, and whether they changed since the latest draw.</summary>
public record MembershipState(string Hash, DateOnly LastRow, bool ChangedSinceDraw);

/// <summary>
/// The owner page: the job, the latest draw's report and the latest snapshot's. <c>PeriodsChangedSinceDraw</c>: a new year moved the
/// periods (2027 starts 2027–30), so the draft should be drawn again.
/// </summary>
public record BacktestDataStatus(bool Storage, BacktestJobRun? Running, BacktestJobEnd? Last, MembershipState Membership,
    bool PeriodsChangedSinceDraw, DrawReport? Draw, SnapshotReport? Snapshot);

/// <summary>/admin/backtest-data, for the owner (the admin group's policy): the backtest's data and its quality report.</summary>
public static class BacktestDataEndpoints
{
    public static void MapBacktestData(RouteGroupBuilder admin)
    {
        var group = admin.MapGroup("/backtest-data");

        group.MapGet("/", async (BacktestDataJob job, BacktestStore store, TimeProvider time, CancellationToken cancellationToken) =>
        {
            var draw = await store.DrawAsync(cancellationToken);
            var snapshot = await store.SnapshotAsync(cancellationToken);
            var membership = Sp500Membership.Embedded;
            var (running, last) = job.Status;
            var periods = BacktestPeriods.For(DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime)).Select(p => p.Label);
            return new BacktestDataStatus(store.Configured, running, last,
                new(membership.Hash, membership.LastRow, draw is not null && draw.MembershipHash != membership.Hash),
                draw is not null && !periods.SequenceEqual(draw.Periods.Select(p => p.Label)), draw, snapshot);
        })
        .WithSummary("The backtest's data: the draw or snapshot running, the latest draw's report and the latest snapshot's.");

        group.MapPost("/draw", (BacktestDataJob job, BacktestStore store) => Start(job, store, BacktestJobKind.Draw))
            .WithSummary("Downloads every member, draws the test draft, runs its checks and takes a snapshot, in the background.");

        group.MapPost("/snapshot", async Task<Results<Accepted, Conflict<string>>> (BacktestDataJob job, BacktestStore store, CancellationToken cancellationToken) =>
            store.Configured && await store.DrawAsync(cancellationToken) is null
                ? TypedResults.Conflict("Draw the test draft first: a snapshot holds its stocks.")
                : Start(job, store, BacktestJobKind.Snapshot))
            .WithSummary("Takes this month's snapshot of the draft's stocks, SPY and the starter lists, in the background.");
    }

    static Results<Accepted, Conflict<string>> Start(BacktestDataJob job, BacktestStore store, BacktestJobKind kind) =>
        !store.Configured ? TypedResults.Conflict("Object storage isn't configured on this server.")
        : job.TryStart(kind) ? TypedResults.Accepted((string?)null)
        : TypedResults.Conflict("A draw or a snapshot is already running.");
}
