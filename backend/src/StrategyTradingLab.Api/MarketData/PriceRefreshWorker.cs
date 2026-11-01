using Microsoft.Extensions.Options;
using StrategyTradingLab.Api.Admin;
using StrategyTradingLab.Api.Evaluation;

namespace StrategyTradingLab.Api.MarketData;

public class PriceRefreshOptions
{
    /// <summary>The first run after the API starts, so a release doesn't leave the prices a day old.</summary>
    public TimeSpan StartDelay { get; set; } = TimeSpan.FromMinutes(1);
}

/// <summary>What the last refresh did and when the next one is, for the admin page. In memory: a restart forgets it.</summary>
public class PriceRefreshState
{
    readonly SemaphoreSlim requested = new(0, 1);
    readonly SemaphoreSlim changed = new(0, 1);
    int pending;
    volatile bool running;
    volatile PriceRun last = new(null, null, [], null, null);

    public bool Running => running || pending == 1;

    /// <summary>
    /// Held while prices are written in bulk: the daily run's fetch, or one of the admin's downloads or deletes, so two of
    /// them never delete and insert the same rows at once. The run waits for it; the admin's are refused.
    /// </summary>
    public SemaphoreSlim Writing { get; } = new(1, 1);
    public DateTimeOffset? LastStarted => last.Started;
    public DateTimeOffset? LastFinished => last.Finished;
    public IReadOnlyList<RefreshResult> LastResults => last.Results;
    /// <summary>What the evaluation after the last refresh did; null before the first run or when it failed.</summary>
    public EvaluationSummary? LastEvaluation => last.Evaluation;
    /// <summary>When the next daily run starts; null while the schedule is off.</summary>
    public DateTimeOffset? NextRun => last.Next;

    /// <summary>Asks the worker to run now. False if a run is on, or already asked for.</summary>
    public bool Request()
    {
        if (running || Interlocked.Exchange(ref pending, 1) == 1) return false;
        Release(requested);
        return true;
    }

    /// <summary>The schedule settings changed: the worker reads them again and recomputes the next run.</summary>
    public void ScheduleChanged() => Release(changed);

    static void Release(SemaphoreSlim semaphore)
    {
        try
        {
            semaphore.Release();
        }
        catch (SemaphoreFullException)
        {
            // Already signalled and not yet seen: this signal is the same one.
        }
    }

    public Task WaitForRequestAsync(CancellationToken cancellationToken) => requested.WaitAsync(cancellationToken);

    public Task WaitForChangeAsync(CancellationToken cancellationToken) => changed.WaitAsync(cancellationToken);

    /// <summary>The run was asked for but not made (see the worker).</summary>
    public void Skipped() => Interlocked.Exchange(ref pending, 0);

    public void Scheduled(DateTimeOffset? next) => last = last with { Next = next };

    public void Started(DateTimeOffset now)
    {
        running = true;
        // A request that landed after the worker woke for the timer left its count behind: it is this run.
        requested.Wait(0);
        Interlocked.Exchange(ref pending, 0);
        last = last with { Started = now };
    }

    public void Finished(DateTimeOffset now, IReadOnlyList<RefreshResult> results, EvaluationSummary? evaluation)
    {
        last = last with { Finished = now, Results = results, Evaluation = evaluation };
        running = false;
    }

    record PriceRun(DateTimeOffset? Started, DateTimeOffset? Finished, IReadOnlyList<RefreshResult> Results, DateTimeOffset? Next,
        EvaluationSummary? Evaluation);
}

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
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = Timeout.InfiniteTimeSpan;
            var failed = false;
            try
            {
                using var scope = scopes.CreateScope();
                var values = await scope.ServiceProvider.GetRequiredService<Admin.SettingsStore>().ValuesAsync(stoppingToken);
                var schedule = PriceSchedule.From(values);
                if (schedule.Enabled) delay = firstWait ? options.Value.StartDelay : schedule.UntilNext(time.GetUtcNow());
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
            await Task.WhenAny(timer, asked, changed);
            await wait.CancelAsync();
            if (stoppingToken.IsCancellationRequested) return;
            // More than one signal can have completed at once, and WhenAny names only one: a request must never be lost
            // (its count is used up), so it runs whatever else fired. A changed schedule or the retry only reads it again.
            var run = asked.IsCompletedSuccessfully || (timer.IsCompletedSuccessfully && !failed);
            if (!run) continue;
            firstWait = false; // The start-up wait is over once it ended in a run (a schedule save during it starts it again).

            await RunAsync(stoppingToken);
        }
    }

    async Task RunAsync(CancellationToken stoppingToken)
    {
        // Prices belong to the database this host owns. While the admin page has the API on the other one, a run would
        // write there and leave the home database stale, so it waits for the next one.
        if (databases.Active != databases.Home)
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
            await state.Writing.WaitAsync(stoppingToken);
            try
            {
                results = await scope.ServiceProvider.GetRequiredService<PriceRefresher>().RunAsync(stoppingToken);
            }
            finally
            {
                state.Writing.Release();
            }
            logger.LogInformation("Prices refreshed: {Updated} updated, {Full} in full, {Failed} failed.",
                results.Count(r => r.Outcome == RefreshOutcome.Updated), results.Count(r => r.Outcome == RefreshOutcome.FullHistory),
                results.Count(r => r.Outcome == RefreshOutcome.Failed));
            // The rules read the prices just stored.
            var evaluator = scope.ServiceProvider.GetRequiredService<StrategyEvaluator>();
            evaluation = await evaluator.RunAsync(stoppingToken);
            logger.LogInformation("Strategies evaluated: {Strategies}, {Signals} new signals, {Trades} new trades, {Missed} missed, {Failed} failed.",
                evaluation.Strategies, evaluation.NewSignals, evaluation.NewTrades, evaluation.Missed, evaluation.Failed);
            // AI strategies' signals come from their rounds; the daily check makes their trades and moves their rounds on.
            var ai = await scope.ServiceProvider.GetRequiredService<Ai.AiRounds>().RunDailyAsync(stoppingToken);
            logger.LogInformation("AI strategies checked: {Strategies}, {Trades} new trades, {Missed} missed, {Failed} failed.",
                ai.Strategies, ai.NewTrades, ai.Missed, ai.Failed);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError(error, "The price refresh or the evaluation failed.");
        }
        finally
        {
            state.Finished(time.GetUtcNow(), results, evaluation);
        }
        await SendNotificationsAsync(stoppingToken);
    }

    /// <summary>
    /// Right after the check, the alerts for the live signals it found that are still waiting, then the daily overview. After the
    /// run is recorded as finished, and apart from it: an email problem is not a refresh failure.
    /// </summary>
    async Task SendNotificationsAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var notifications = scope.ServiceProvider.GetRequiredService<Notifications.NotificationService>();
            if (!await notifications.EnabledAsync(stoppingToken)) return;
            logger.LogInformation("Sent {Count} signal alerts.", await notifications.SendSignalAlertsAsync(UsSession.LastComplete(time.GetUtcNow()), stoppingToken));
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogWarning("The signal alerts couldn't be sent: {Reason}", error.Message);
        }
        // Apart from the alerts, so a failed alert doesn't hold the overviews back.
        try
        {
            using var scope = scopes.CreateScope();
            var notifications = scope.ServiceProvider.GetRequiredService<Notifications.NotificationService>();
            if (await notifications.EnabledAsync(stoppingToken))
                logger.LogInformation("Sent {Count} daily overviews.", await notifications.SendOverviewsAsync(time.GetUtcNow(), stoppingToken));
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogWarning("The daily overviews couldn't be sent: {Reason}", error.Message);
        }
    }
}
