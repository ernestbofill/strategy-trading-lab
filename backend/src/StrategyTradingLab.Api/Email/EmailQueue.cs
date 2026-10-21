using System.Threading.Channels;

namespace StrategyTradingLab.Api.Email;

public record OutgoingEmail(string To, string Subject, string Body);

/// <summary>
/// Emails sent after the request has answered, so an answer never waits on (or reveals, by its timing) a send.
/// In memory: an email queued just before a restart is lost, which the user fixes with "send the link again".
/// </summary>
public class EmailQueue
{
    readonly Channel<OutgoingEmail> channel = Channel.CreateBounded<OutgoingEmail>(new BoundedChannelOptions(500)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
    });

    public void Enqueue(OutgoingEmail email) => channel.Writer.TryWrite(email);

    public IAsyncEnumerable<OutgoingEmail> ReadAllAsync(CancellationToken cancellationToken) => channel.Reader.ReadAllAsync(cancellationToken);
}

/// <summary>Sends the queued emails one by one. Without email set up, Development logs them (links included) instead.</summary>
public class EmailWorker(EmailQueue queue, IServiceScopeFactory scopes, IHostEnvironment environment, ILogger<EmailWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var email in queue.ReadAllAsync(stoppingToken))
        {
            using var scope = scopes.CreateScope();
            try
            {
                await scope.ServiceProvider.GetRequiredService<IEmailSender>().SendAsync(email.To, email.Subject, email.Body, stoppingToken);
            }
            catch (EmailNotSetUpException) when (environment.IsDevelopment())
            {
                logger.LogInformation("Email isn't set up, so here it is instead.\nTo: {To}\nSubject: {Subject}\n\n{Body}", email.To, email.Subject, email.Body);
            }
            catch (EmailException error)
            {
                // Never the body: it holds single-use links.
                logger.LogWarning("Couldn't send \"{Subject}\": {Reason}", email.Subject, error.Message);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                logger.LogError(error, "Sending \"{Subject}\" failed.", email.Subject);
            }
        }
    }
}
