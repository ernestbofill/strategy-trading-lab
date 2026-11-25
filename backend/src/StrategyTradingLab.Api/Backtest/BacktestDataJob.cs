namespace StrategyTradingLab.Api.Backtest;

public enum BacktestJobKind { Draw, Snapshot }

/// <summary>The draw or snapshot running now, and where it is.</summary>
public record BacktestJobRun(BacktestJobKind Kind, DateTimeOffset Started, JobProgress? Progress);

/// <summary>How the last one ended: <c>Error</c> is null when it worked.</summary>
public record BacktestJobEnd(BacktestJobKind Kind, DateTimeOffset Started, DateTimeOffset Finished, string? Error);

/// <summary>
/// Runs one draw or snapshot at a time in the background, as the owner asks: a draw takes several minutes (one request to Yahoo
/// after another). In memory: a restart forgets the run in progress, and the stored draw and snapshot stay as they were.
/// </summary>
public class BacktestDataJob(IServiceScopeFactory scopes, IHostApplicationLifetime lifetime, TimeProvider time, ILogger<BacktestDataJob> logger)
{
    readonly Lock gate = new();
    BacktestJobRun? running;
    BacktestJobEnd? last;

    public (BacktestJobRun? Running, BacktestJobEnd? Last) Status
    {
        get { lock (gate) return (running, last); }
    }

    /// <summary>Starts it in the background. False when one is already running.</summary>
    public bool TryStart(BacktestJobKind kind)
    {
        lock (gate)
        {
            if (running is not null) return false;
            running = new(kind, time.GetUtcNow(), null);
        }
        _ = Task.Run(() => RunAsync(kind, lifetime.ApplicationStopping));
        return true;
    }

    async Task RunAsync(BacktestJobKind kind, CancellationToken stopping)
    {
        string? error = null;
        try
        {
            using var scope = scopes.CreateScope();
            var data = scope.ServiceProvider.GetRequiredService<BacktestData>();
            if (kind == BacktestJobKind.Draw) await data.DrawAsync(Report, stopping);
            else await data.SnapshotAsync(Report, stopping);
        }
        catch (BacktestDataException failure)
        {
            error = failure.Message;
        }
        catch (Exception failure) when (failure is not OperationCanceledException || !stopping.IsCancellationRequested)
        {
            logger.LogError(failure, "The backtest's {Kind} failed.", kind);
            error = $"It failed: {failure.Message}";
        }
        catch (OperationCanceledException)
        {
            error = "The API stopped before it finished.";
        }
        finally
        {
            lock (gate)
            {
                last = new(kind, running!.Started, time.GetUtcNow(), error);
                running = null;
            }
        }
    }

    void Report(JobProgress progress)
    {
        lock (gate)
            if (running is not null) running = running with { Progress = progress };
    }
}
