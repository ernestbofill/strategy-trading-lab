using StrategyTradingLab.Api.Admin;

namespace StrategyTradingLab.Api.Notifications;

/// <summary>
/// Looks every hour for users whose morning has begun and who haven't had a snoozed signal's or an AI round's reminder, and for an
/// overview the send right after the daily check couldn't deliver (see <see cref="NotificationService"/>).
/// </summary>
public class NotificationWorker(DatabaseSwitch databases, IServiceScopeFactory scopes, TimeProvider time, ILogger<NotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // A minute after start, so a release doesn't wait an hour; then on the hour.
        var delay = TimeSpan.FromMinutes(1);
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(delay, time, stoppingToken);
            // Like the price refresh: while the admin page has the API on the other database, its users are not ours to email.
            if (databases.Active != databases.Home)
            {
                delay = UntilNextHour(time.GetUtcNow());
                continue;
            }
            try
            {
                using var scope = scopes.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<NotificationService>();
                if (await service.EnabledAsync(stoppingToken))
                {
                    var sent = await service.SendOverviewsAsync(time.GetUtcNow(), stoppingToken);
                    if (sent > 0) logger.LogInformation("Sent {Count} daily overviews.", sent);
                    var reminders = await service.SendSnoozeRemindersAsync(time.GetUtcNow(), stoppingToken);
                    if (reminders > 0) logger.LogInformation("Sent {Count} snooze reminders.", reminders);
                    var rounds = await service.SendAiRoundRemindersAsync(time.GetUtcNow(), stoppingToken);
                    if (rounds > 0) logger.LogInformation("Sent {Count} AI round reminders.", rounds);
                }
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                logger.LogWarning("The overview check failed: {Reason}", error.Message); // Typically the database doesn't answer: next hour.
            }
            delay = UntilNextHour(time.GetUtcNow());
        }
    }

    /// <summary>The wait until the next top of the hour (UTC offsets are whole hours or half hours, so every user's 07:00 is reached within the hour that holds it).</summary>
    public static TimeSpan UntilNextHour(DateTimeOffset now) => TimeSpan.FromHours(1) - TimeSpan.FromTicks(now.UtcTicks % TimeSpan.TicksPerHour);
}
