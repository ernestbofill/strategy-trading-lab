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

    /// <summary>Runs <paramref name="write"/> holding <see cref="Writing"/>, so no other price write runs meanwhile.</summary>
    public async Task<T> WithWriteLockAsync<T>(Func<Task<T>> write, CancellationToken cancellationToken)
    {
        await Writing.WaitAsync(cancellationToken);
        try
        {
            return await write();
        }
        finally
        {
            Writing.Release();
        }
    }
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
