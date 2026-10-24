namespace StrategyTradingLab.Api.Notifications;

/// <summary>Looks every hour for users whose morning has begun and who haven't had today's overview (see <see cref="NotificationService"/>).</summary>
public class NotificationWorker(IServiceScopeFactory scopes, TimeProvider time, ILogger<NotificationWorker> logger) : BackgroundService
{
    static readonly TimeSpan Every = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // A minute after start, so a release doesn't wait an hour; then on the hour.
        var delay = TimeSpan.FromMinutes(1);
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(delay, time, stoppingToken);
            try
            {
                using var scope = scopes.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<NotificationService>();
                if (await service.EnabledAsync(stoppingToken))
                {
                    var sent = await service.SendOverviewsAsync(time.GetUtcNow(), stoppingToken);
                    if (sent > 0) logger.LogInformation("Sent {Count} daily overviews.", sent);
                }
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                logger.LogWarning("The overview check failed: {Reason}", error.Message); // Typically the database doesn't answer: next hour.
            }
            delay = Every;
        }
    }
}
