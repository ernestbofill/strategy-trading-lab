using Microsoft.Extensions.Options;

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
    readonly SemaphoreSlim requested = new(0);
    public bool Running { get; private set; }
    public DateTimeOffset? LastStarted { get; private set; }
    public DateTimeOffset? LastFinished { get; private set; }
    public IReadOnlyList<RefreshResult> LastResults { get; private set; } = [];

    /// <summary>Asks the worker to run now. False if a run is on already.</summary>
    public bool Request()
    {
        if (Running) return false;
        requested.Release();
        return true;
    }

    public Task WaitForRequestAsync(CancellationToken cancellationToken) => requested.WaitAsync(cancellationToken);

    public void Started(DateTimeOffset now)
    {
        Running = true;
        LastStarted = now;
    }

    public void Finished(DateTimeOffset now, IReadOnlyList<RefreshResult> results)
    {
        Running = false;
        LastFinished = now;
        LastResults = results;
    }
}

/// <summary>Refreshes the prices once a day after the US close, and when the admin page asks.</summary>
public class PriceRefreshWorker(PriceRefreshState state, IServiceScopeFactory scopes, IOptions<PriceRefreshOptions> options,
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
