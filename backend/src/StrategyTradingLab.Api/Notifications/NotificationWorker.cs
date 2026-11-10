using StrategyTradingLab.Api.Admin;
using StrategyTradingLab.Api.Evaluation;

namespace StrategyTradingLab.Api.Notifications;

/// <summary>
/// Looks every hour for users whose notification hour has come: signal alerts and the summary held for it (or that the send right
/// after the daily check couldn't deliver), and snoozed signals' and AI rounds' reminders (see <see cref="NotificationService"/>, <see cref="NotifyTime"/>).
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
                    // Alerts held for the user's notification hour (and any a run couldn't send) go out here, before the summary.
                    var now = time.GetUtcNow();
                    var session = UsSession.LastComplete(now);
                    var alerts = await service.SendSignalAlertsAsync(session, stoppingToken) + await service.SendSignalEndedAsync(session, stoppingToken);
                    if (alerts > 0) logger.LogInformation("Sent {Count} signal alerts.", alerts);
                    var sent = await service.SendOverviewsAsync(now, stoppingToken);
                    if (sent > 0) logger.LogInformation("Sent {Count} summaries.", sent);
                    var reminders = await service.SendSnoozeRemindersAsync(now, stoppingToken);
                    if (reminders > 0) logger.LogInformation("Sent {Count} snooze reminders.", reminders);
                    var rounds = await service.SendAiRoundRemindersAsync(now, stoppingToken);
                    if (rounds > 0) logger.LogInformation("Sent {Count} AI round reminders.", rounds);
                    // Push after email. A signal alert a run couldn't push (a push service down) is retried here, every hour.
                    var pushed = await service.PushSignalAlertsAsync(session, stoppingToken)
                        + await service.PushSignalEndedAsync(session, stoppingToken)
                        + await service.PushSnoozeRemindersAsync(now, stoppingToken)
                        + await service.PushAiRoundRemindersAsync(now, stoppingToken)
                        + await service.PushOverviewsAsync(now, stoppingToken);
                    if (pushed > 0) logger.LogInformation("Pushed {Count} notifications.", pushed);
                    // Apart, so a failure here doesn't hold back the reminders and summaries above it next hour.
                    try
                    {
                        var close = await service.PushGettingCloseAsync(session, stoppingToken);
                        if (close > 0) logger.LogInformation("Pushed {Count} getting close alerts.", close);
                    }
                    catch (Exception error) when (error is not OperationCanceledException)
                    {
                        logger.LogWarning("The getting close push failed: {Reason}", error.Message);
                    }
                }
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                logger.LogWarning("The overview check failed: {Reason}", error.Message); // Typically the database doesn't answer: next hour.
            }
            delay = UntilNextHour(time.GetUtcNow());
        }
    }

    /// <summary>
/// The wait until the next top of the hour. A user's chosen hour is reached at most 45 minutes late (time zones 45 minutes off the
/// hour); a summary that comes due after the next close is still sent (<see cref="NotificationService.SendOverviewsAsync"/>).
/// </summary>
    public static TimeSpan UntilNextHour(DateTimeOffset now) => TimeSpan.FromHours(1) - TimeSpan.FromTicks(now.UtcTicks % TimeSpan.TicksPerHour);
}
