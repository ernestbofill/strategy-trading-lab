using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Admin;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Evaluation;

namespace StrategyTradingLab.Api.MarketData;

/// <summary>
/// Refreshes the prices once a day at the time set on the admin page, and when the admin page asks. It reads the
/// schedule from the database whenever it wakes (after a run, a change on the page, or an error).
/// </summary>
public class PriceRefreshWorker(PriceRefreshState state, DatabaseSwitch databases, IServiceScopeFactory scopes,
    IOptions<PriceRefreshOptions> options, TimeProvider time, ILogger<PriceRefreshWorker> logger) : BackgroundService
{
    static readonly TimeSpan RetryAfterError = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var firstWait = true;
        // When the next run is due. Kept while the worker wakes for a strategy's check, so a check neither restarts the start-up
        // wait nor, running past the daily time, pushes the run to the next day: a time already passed runs at once.
        DateTimeOffset? due = null;
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = Timeout.InfiniteTimeSpan;
            var failed = false;
            try
            {
                using var scope = scopes.CreateScope();
                var values = await scope.ServiceProvider.GetRequiredService<Admin.SettingsStore>().ValuesAsync(stoppingToken);
                var schedule = PriceSchedule.From(values);
                var now = time.GetUtcNow();
                if (!schedule.Enabled) due = null;
                else
                {
                    due ??= now + (firstWait ? options.Value.StartDelay : schedule.UntilNext(now));
                    delay = due.Value > now ? due.Value - now : TimeSpan.Zero;
                }
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                // Typically the database in use doesn't answer: try again later, and keep the process up.
                logger.LogWarning("The price schedule couldn't be read: {Reason}", error.Message);
                delay = RetryAfterError;
                failed = true;
            }
            state.Scheduled(delay == Timeout.InfiniteTimeSpan ? null : time.GetUtcNow() + delay);

            using var wait = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var timer = Task.Delay(delay, time, wait.Token);
            var asked = state.WaitForRequestAsync(wait.Token);
            var changed = state.WaitForChangeAsync(wait.Token);
            var check = state.WaitForCheckAsync(wait.Token);
            await Task.WhenAny(timer, asked, changed, check);
            await wait.CancelAsync();
            if (stoppingToken.IsCancellationRequested) return;
            // More than one signal can have completed at once, and WhenAny names only one: a request must never be lost
            // (its count is used up), so it runs whatever else fired. A changed schedule or the retry only reads it again.
            var run = asked.IsCompletedSuccessfully || (timer.IsCompletedSuccessfully && !failed);
            if (!run)
            {
                if (changed.IsCompletedSuccessfully) due = null; // Read the schedule afresh.
                // A strategy's own check; a run instead covers it (it fetches every watched asset and checks every strategy).
                if (check.IsCompletedSuccessfully) await CheckAsync(stoppingToken);
                continue;
            }
            firstWait = false; // The start-up wait is over once it ended in a run (a schedule save during it starts it again).
            due = null;

            state.TakeChecks();
            await RunAsync(stoppingToken);
        }
    }

    async Task RunAsync(CancellationToken stoppingToken)
    {
        // Prices belong to the database this host owns. While the admin page has the API on the other one, a run would
        // write there and leave the home database stale, so it waits for the next one.
        if (!databases.IsHome)
        {
            logger.LogWarning("The price refresh is skipped: the API is switched to the {Database} database.", databases.Active);
            state.Skipped();
            return;
        }
        state.Started(time.GetUtcNow());
        IReadOnlyList<RefreshResult> results = [];
        EvaluationSummary? evaluation = null;
        try
        {
            using var scope = scopes.CreateScope();
            results = await state.WithWriteLockAsync(() => scope.ServiceProvider.GetRequiredService<PriceRefresher>().RunAsync(stoppingToken), stoppingToken);
            logger.LogInformation("Prices refreshed: {Updated} updated, {Full} in full, {Failed} failed.",
                results.Count(r => r.Outcome == RefreshOutcome.Updated), results.Count(r => r.Outcome == RefreshOutcome.FullHistory),
                results.Count(r => r.Outcome == RefreshOutcome.Failed));
            await SyncPriceAlertsAsync(scope, results, stoppingToken);
            // The rules read the prices just stored.
            var evaluator = scope.ServiceProvider.GetRequiredService<StrategyEvaluator>();
            evaluation = await evaluator.RunAsync(stoppingToken);
            logger.LogInformation("Strategies evaluated: {Strategies}, {Signals} new signals, {Trades} new trades, {Missed} missed, {Failed} failed.",
                evaluation.Strategies, evaluation.NewSignals, evaluation.NewTrades, evaluation.Missed, evaluation.Failed);
            // AI strategies' signals come from their rounds; the daily check makes their trades and moves their rounds on.
            var ai = await scope.ServiceProvider.GetRequiredService<Ai.AiRounds>().RunDailyAsync(stoppingToken);
            logger.LogInformation("AI strategies checked: {Strategies}, {Trades} new trades, {Missed} missed, {Failed} failed.",
                ai.Strategies, ai.NewTrades, ai.Missed, ai.Failed);
            var alerts = scope.ServiceProvider.GetRequiredService<AdminAlerts>();
            await alerts.ClearAsync(DailyCheckKey, stoppingToken);
            await alerts.ForgetOldAsync(stoppingToken);
            var housekeeping = scope.ServiceProvider.GetRequiredService<Auth.HousekeepingService>();
            await housekeeping.RunAsync(stoppingToken);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError(error, "The price refresh or the evaluation failed.");
            await RaiseAsync(new NewAlert(DailyCheckKey, "The daily check failed", error.Message), stoppingToken);
        }
        finally
        {
            state.Finished(time.GetUtcNow(), results, evaluation);
        }
        await SendNotificationsAsync(stoppingToken);
    }

    /// <summary>
    /// Checks the strategies asked for (<see cref="PriceRefreshState.RequestCheck"/>): fetches the prices their stocks lack,
    /// then runs their rules up to the last finished session, as the daily run would. Best effort: a failure leaves it to that run.
    /// </summary>
    async Task CheckAsync(CancellationToken stoppingToken)
    {
        var ids = state.TakeChecks();
        if (ids.Count == 0 || !options.Value.FetchOnDemand) return;
        // As for the run: prices belong to the database this host owns.
        if (!databases.IsHome) return;
        var newSignals = 0;
        foreach (var id in ids)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var refresher = scope.ServiceProvider.GetRequiredService<PriceRefresher>();
                var fetched = await state.WithWriteLockAsync(() => refresher.CatchUpAsync(id, stoppingToken), stoppingToken);
                // While a US session is in progress the next open after the last finished session has passed: a signal found now
                // fills at the latest price, fetched before its stock's rules run and not stored (#220). A stock without one is left
                // to the daily run. One clock reading decides both, so the session and the last finished one always agree.
                var now = time.GetUtcNow();
                Func<Asset, CancellationToken, Task<LatestPrice?>>? latest = UsSession.InProgress(now)
                    ? (asset, cancellationToken) => refresher.TryLatestAsync(asset.Symbol, cancellationToken) : null;
                var (checkedNow, left) = await scope.ServiceProvider.GetRequiredService<StrategyEvaluator>().CheckAsync(id, latest, now, stoppingToken);
                state.LeftToDailyRun(id, left, now);
                newSignals += checkedNow.NewSignals;
                logger.LogInformation("Strategy {Strategy} checked at once: {Fetched} assets fetched ({Failed} failed), {Signals} new signals, {Trades} new trades, {Left} left to the daily run.",
                    id, fetched.Count, fetched.Count(r => r.Outcome == RefreshOutcome.Failed), checkedNow.NewSignals, checkedNow.NewTrades, left.Count);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                logger.LogWarning("Strategy {Strategy} couldn't be checked at once: {Reason}", id, error.Message);
            }
        }
        // A signal found here is pushed at once, as after the daily run (Ernest, 2026-11-12); its email keeps the user's hour.
        if (newSignals > 0) await PushChangesAsync(stoppingToken);
    }

    const string DailyCheckKey = "daily-check";

    /// <summary>
    /// The refresh's alerts: each asset or exchange rate that failed, and the index fund's prices more than one session behind
    /// (a holiday leaves them one behind).
    /// </summary>
    internal static async Task<List<NewAlert>> PriceAlertsAsync(AppDbContext db, IReadOnlyList<RefreshResult> results, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var found = results.Where(r => r.Outcome == RefreshOutcome.Failed)
            .Select(r => new NewAlert($"prices:{r.Name}", $"{r.Name}: prices weren't updated", r.Problem)).ToList();
        var lastSession = UsSession.LastComplete(now);
        var index = await db.Prices.Where(p => p.AssetId == Strategies.StrategyBook.IndexFundId).MaxAsync(p => (DateOnly?)p.Date, cancellationToken);
        if (index is null || UsSession.WeekdaysBetween(index.Value, lastSession) > 1)
            found.Add(new NewAlert("prices:behind", "Prices are behind",
                $"The index fund's newest price is from {(index is { } day ? day.ToString("yyyy-MM-dd") : "no day")}; the last US session was {lastSession:yyyy-MM-dd}."));
        return found;
    }

    /// <summary>The refresh's alerts, never failing the run: if the check itself fails, the alerts stay as they were.</summary>
    async Task SyncPriceAlertsAsync(IServiceScope scope, IReadOnlyList<RefreshResult> results, CancellationToken stoppingToken)
    {
        List<NewAlert> found;
        try
        {
            found = await PriceAlertsAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), results, time.GetUtcNow(), stoppingToken);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogWarning("The price alerts couldn't be checked: {Reason}", error.Message);
            return;
        }
        await scope.ServiceProvider.GetRequiredService<AdminAlerts>().SyncAsync(AlertSource.Prices, found, stoppingToken);
    }

    /// <summary>
    /// Runs <paramref name="work"/> in a scope of its own, best effort: a failure is logged as "<paramref name="failure"/>: reason"
    /// and never fails the caller.
    /// </summary>
    async Task TryInScopeAsync(string failure, Func<IServiceProvider, Task> work)
    {
        try
        {
            using var scope = scopes.CreateScope();
            await work(scope.ServiceProvider);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogWarning("{Failure}: {Reason}", failure, error.Message);
        }
    }

    /// <summary>Raises an alert in a scope of its own, for when the run's scope failed.</summary>
    Task RaiseAsync(NewAlert alert, CancellationToken stoppingToken) =>
        TryInScopeAsync("The admin alert couldn't be raised", services =>
            services.GetRequiredService<AdminAlerts>().RaiseAsync(AlertSource.DailyCheck, alert, stoppingToken));

    /// <summary>
    /// Right after the check, the pushes of what it changed (<see cref="PushChangesAsync"/>), then the alerts for the live signals
    /// it found that are still waiting, then the daily overview. After the run is recorded as finished, and apart from it: a push
    /// or email problem is not a refresh failure.
    /// </summary>
    async Task SendNotificationsAsync(CancellationToken stoppingToken)
    {
        // First, and apart from the emails: pushes go out as soon as the check has run, whatever the user's notification hour.
        await PushChangesAsync(stoppingToken);
        await TryInScopeAsync("The signal alerts couldn't be sent", async services =>
        {
            var notifications = services.GetRequiredService<Notifications.NotificationService>();
            if (!await notifications.EnabledAsync(stoppingToken)) return;
            var session = UsSession.LastComplete(time.GetUtcNow());
            logger.LogInformation("Sent {Count} signal alerts.", await notifications.SendSignalAlertsAsync(session, stoppingToken));
            // A buy signal that ended unanswered, after its alert (one this run just sent waits for its next day).
            logger.LogInformation("Sent {Count} ended signal alerts.", await notifications.SendSignalEndedAsync(session, stoppingToken));
        });
        // Apart from the alerts, so a failed alert doesn't hold the overviews back.
        await TryInScopeAsync("The daily overviews couldn't be sent", async services =>
        {
            var notifications = services.GetRequiredService<Notifications.NotificationService>();
            if (await notifications.EnabledAsync(stoppingToken))
                logger.LogInformation("Sent {Count} daily overviews.", await notifications.SendOverviewsAsync(time.GetUtcNow(), stoppingToken));
        });
    }

    /// <summary>
    /// The pushes of what a check changed: signals, their end, stocks becoming candidates or getting close. Never failing the
    /// check: what couldn't go out is retried by the hourly <see cref="Notifications.NotificationWorker"/>.
    /// </summary>
    Task PushChangesAsync(CancellationToken stoppingToken) =>
        TryInScopeAsync("The pushes couldn't be sent", async services =>
        {
            var notifications = services.GetRequiredService<Notifications.NotificationService>();
            if (!await notifications.EnabledAsync(stoppingToken)) return;
            var now = time.GetUtcNow();
            var session = UsSession.LastComplete(now);
            logger.LogInformation("Pushed {Count} signal alerts.", await notifications.PushSignalAlertsAsync(session, stoppingToken));
            // A buy signal that ended unanswered, after its alert (one this run just pushed is told on a later check).
            logger.LogInformation("Pushed {Count} ended signal alerts.", await notifications.PushSignalEndedAsync(session, stoppingToken));
            logger.LogInformation("Pushed {Count} stock changes.", await notifications.PushStockChangesAsync(now, stoppingToken));
        });
}
