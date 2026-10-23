using Microsoft.Extensions.Options;
using StrategyTradingLab.Api.Admin;

namespace StrategyTradingLab.Api.MarketData;

public class PriceRefreshOptions
{
    /// <summary>Off in Development and in tests, so running or testing locally never calls Yahoo by itself.</summary>
    public bool Enabled { get; set; }
    /// <summary>When the daily run happens, UTC: after the US close in both summer and winter time (21:00 UTC at the latest).</summary>
    public TimeOnly DailyAtUtc { get; set; } = new(22, 0);
    /// <summary>The first run after the API starts, so a release doesn't leave the prices a day old.</summary>
    public TimeSpan StartDelay { get; set; } = TimeSpan.FromMinutes(1);
}

/// <summary>What the last refresh did, for the admin page. In memory: a restart forgets it (the first run follows).</summary>
public class PriceRefreshState
{
    readonly SemaphoreSlim requested = new(0, 1);
    int pending;
    volatile bool running;
    volatile PriceRun last = new(null, null, []);

    public bool Running => running || pending == 1;
    public DateTimeOffset? LastStarted => last.Started;
    public DateTimeOffset? LastFinished => last.Finished;
    public IReadOnlyList<RefreshResult> LastResults => last.Results;

    /// <summary>Asks the worker to run now. False if a run is on, or already asked for.</summary>
    public bool Request()
    {
        if (running || Interlocked.Exchange(ref pending, 1) == 1) return false;
        requested.Release();
        return true;
    }

    public Task WaitForRequestAsync(CancellationToken cancellationToken) => requested.WaitAsync(cancellationToken);

    /// <summary>The run was asked for but not made (see the worker).</summary>
    public void Skipped() => Interlocked.Exchange(ref pending, 0);

    public void Started(DateTimeOffset now)
    {
        running = true;
        Interlocked.Exchange(ref pending, 0);
        last = new PriceRun(now, last.Finished, last.Results);
    }

    public void Finished(DateTimeOffset now, IReadOnlyList<RefreshResult> results)
    {
        last = new PriceRun(last.Started, now, results);
        running = false;
    }

    record PriceRun(DateTimeOffset? Started, DateTimeOffset? Finished, IReadOnlyList<RefreshResult> Results);
}

/// <summary>Refreshes the prices once a day after the US close, and when the admin page asks.</summary>
public class PriceRefreshWorker(PriceRefreshState state, DatabaseSwitch databases, IServiceScopeFactory scopes, IOptions<PriceRefreshOptions> options,
    TimeProvider time, ILogger<PriceRefreshWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        var delay = options.Value.StartDelay;
        while (!stoppingToken.IsCancellationRequested)
        {
            using var wait = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var timer = Task.Delay(delay, time, wait.Token);
            var asked = state.WaitForRequestAsync(wait.Token);
            await Task.WhenAny(timer, asked);
            await wait.CancelAsync();
            if (stoppingToken.IsCancellationRequested) return;

            await RunAsync(stoppingToken);
            delay = UntilNext(time.GetUtcNow(), options.Value.DailyAtUtc);
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
        try
        {
            using var scope = scopes.CreateScope();
            results = await scope.ServiceProvider.GetRequiredService<PriceRefresher>().RunAsync(stoppingToken);
            logger.LogInformation("Prices refreshed: {Updated} updated, {Full} in full, {Failed} failed.",
                results.Count(r => r.Outcome == RefreshOutcome.Updated), results.Count(r => r.Outcome == RefreshOutcome.FullHistory),
                results.Count(r => r.Outcome == RefreshOutcome.Failed));
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError(error, "The price refresh failed.");
        }
        finally
        {
            state.Finished(time.GetUtcNow(), results);
        }
    }

    /// <summary>The wait until the next daily time (today's if it hasn't passed, else tomorrow's).</summary>
    public static TimeSpan UntilNext(DateTimeOffset now, TimeOnly at)
    {
        var next = new DateTimeOffset(DateOnly.FromDateTime(now.UtcDateTime), at, TimeSpan.Zero);
        if (next <= now) next = next.AddDays(1);
        return next - now;
    }
}
