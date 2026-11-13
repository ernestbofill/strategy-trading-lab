using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Admin;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Evaluation;

namespace StrategyTradingLab.Api.MarketData;

public class PriceRefreshOptions
{
    /// <summary>The first run after the API starts, so a release doesn't leave the prices a day old.</summary>
    public TimeSpan StartDelay { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Fetches from Yahoo outside the daily run: a strategy's check when it starts or its list gains a stock
    /// (<see cref="PriceRefreshState.RequestCheck"/>), with its stocks' latest prices during a US session, and a signal's latest
    /// price when its page opens. The API tests turn it
    /// off: it would reach Yahoo and run the rules behind their backs.
    /// </summary>
    public bool FetchOnDemand { get; set; } = true;
}

/// <summary>What the last refresh did and when the next one is, for the admin page. In memory: a restart forgets it.</summary>
public class PriceRefreshState
{
    readonly SemaphoreSlim requested = new(0, 1);
    readonly SemaphoreSlim changed = new(0, 1);
    readonly SemaphoreSlim checkRequested = new(0, 1);
    readonly HashSet<Guid> checks = [];
    readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, (DateTimeOffset At, Task<LatestPrice?> Price)> latestPrices = new();
    readonly Dictionary<Guid, (DateOnly Day, HashSet<Guid> Assets)> left = [];
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

    /// <summary>
    /// A strategy just started, or its list gained a stock: the worker fetches the prices it lacks and runs its rules now,
    /// instead of leaving it "not checked yet" until the next daily run. In the worker's loop, so never at once with that run.
    /// </summary>
    public void RequestCheck(Guid strategyId)
    {
        lock (checks) checks.Add(strategyId);
        Release(checkRequested);
    }

    /// <summary>
    /// A signal's page's latest price for an asset (#220): the one fetched within <paramref name="every"/>, else a fresh one from
    /// <paramref name="fetch"/>. Each try counts, so a source that fails or is slow isn't asked again on every page open (a failed
    /// try is remembered as null), and a page opened while a try runs waits for that same try, so both show the same price. A try
    /// that throws (its page closed) gives the others null and is forgotten, so the next page open tries again. Kept in memory
    /// only, never in the price history.
    /// </summary>
    public async Task<LatestPrice?> LatestPriceAsync(Guid assetId, DateTimeOffset now, TimeSpan every, Func<Task<LatestPrice?>> fetch,
        CancellationToken cancellationToken)
    {
        // Claimed in place of what was there, so of two page opens at once only one asks the source.
        var running = new TaskCompletionSource<LatestPrice?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var mine = (now, running.Task);
        var claimed = latestPrices.TryGetValue(assetId, out var tried)
            ? tried.At <= now - every && latestPrices.TryUpdate(assetId, mine, tried)
            : latestPrices.TryAdd(assetId, mine);
        if (!claimed) return latestPrices.TryGetValue(assetId, out var current) ? await current.Price.WaitAsync(cancellationToken) : null;
        try
        {
            var price = await fetch();
            running.SetResult(price);
            return price;
        }
        catch
        {
            running.SetResult(null);
            latestPrices.TryRemove(KeyValuePair.Create(assetId, mine));
            throw;
        }
    }

    /// <summary>
    /// Records the stocks a strategy's check at once just left to the daily run (no latest price during a US session, #220), in
    /// place of what its last check left. Their rows say so until the session ends. Days before today are forgotten.
    /// </summary>
    public void LeftToDailyRun(Guid strategyId, IReadOnlyCollection<Guid> assetIds, DateTimeOffset now)
    {
        var today = UsSession.Today(now);
        lock (left)
        {
            foreach (var old in left.Where(entry => entry.Value.Day != today).Select(entry => entry.Key).ToList()) left.Remove(old);
            if (assetIds.Count == 0) left.Remove(strategyId);
            else left[strategyId] = (today, [.. assetIds]);
        }
    }

    /// <summary>The stocks of a strategy its check at once left to the daily run this session; none outside a session.</summary>
    public IReadOnlySet<Guid> LeftToDailyRun(Guid strategyId, DateTimeOffset now)
    {
        if (!UsSession.InProgress(now)) return new HashSet<Guid>();
        lock (left)
            return left.TryGetValue(strategyId, out var entry) && entry.Day == UsSession.Today(now) ? entry.Assets : new HashSet<Guid>();
    }

    /// <summary>The strategies asked to be checked since the last call.</summary>
    public IReadOnlyList<Guid> TakeChecks()
    {
        lock (checks)
        {
            var taken = checks.ToList();
            checks.Clear();
            return taken;
        }
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

    public Task WaitForCheckAsync(CancellationToken cancellationToken) => checkRequested.WaitAsync(cancellationToken);

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
        if (databases.Active != databases.Home) return;
        var newSignals = 0;
        foreach (var id in ids)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var refresher = scope.ServiceProvider.GetRequiredService<PriceRefresher>();
                IReadOnlyList<RefreshResult> fetched;
                await state.Writing.WaitAsync(stoppingToken);
                try
                {
                    fetched = await refresher.CatchUpAsync(id, stoppingToken);
                }
                finally
                {
                    state.Writing.Release();
                }
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

    /// <summary>Raises an alert in a scope of its own, for when the run's scope failed.</summary>
    async Task RaiseAsync(NewAlert alert, CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AdminAlerts>().RaiseAsync(AlertSource.DailyCheck, alert, stoppingToken);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogWarning("The admin alert couldn't be raised: {Reason}", error.Message);
        }
    }

    /// <summary>
    /// Right after the check, the pushes of what it changed (<see cref="PushChangesAsync"/>), then the alerts for the live signals
    /// it found that are still waiting, then the daily overview. After the run is recorded as finished, and apart from it: a push
    /// or email problem is not a refresh failure.
    /// </summary>
    async Task SendNotificationsAsync(CancellationToken stoppingToken)
    {
        // First, and apart from the emails: pushes go out as soon as the check has run, whatever the user's notification hour.
        await PushChangesAsync(stoppingToken);
        try
        {
            using var scope = scopes.CreateScope();
            var notifications = scope.ServiceProvider.GetRequiredService<Notifications.NotificationService>();
            if (!await notifications.EnabledAsync(stoppingToken)) return;
            var session = UsSession.LastComplete(time.GetUtcNow());
            logger.LogInformation("Sent {Count} signal alerts.", await notifications.SendSignalAlertsAsync(session, stoppingToken));
            // A buy signal that ended unanswered, after its alert (one this run just sent waits for its next day).
            logger.LogInformation("Sent {Count} ended signal alerts.", await notifications.SendSignalEndedAsync(session, stoppingToken));
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

    /// <summary>
    /// The pushes of what a check changed: signals, their end, stocks becoming candidates or getting close. Never failing the
    /// check: what couldn't go out is retried by the hourly <see cref="Notifications.NotificationWorker"/>.
    /// </summary>
    async Task PushChangesAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var notifications = scope.ServiceProvider.GetRequiredService<Notifications.NotificationService>();
            if (!await notifications.EnabledAsync(stoppingToken)) return;
            var now = time.GetUtcNow();
            var session = UsSession.LastComplete(now);
            logger.LogInformation("Pushed {Count} signal alerts.", await notifications.PushSignalAlertsAsync(session, stoppingToken));
            // A buy signal that ended unanswered, after its alert (one this run just pushed is told on a later check).
            logger.LogInformation("Pushed {Count} ended signal alerts.", await notifications.PushSignalEndedAsync(session, stoppingToken));
            logger.LogInformation("Pushed {Count} stock changes.", await notifications.PushStockChangesAsync(now, stoppingToken));
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogWarning("The pushes couldn't be sent: {Reason}", error.Message);
        }
    }
}
