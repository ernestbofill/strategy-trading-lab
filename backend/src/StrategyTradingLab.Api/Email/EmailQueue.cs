using System.Threading.Channels;
using StrategyTradingLab.Api.Admin;
using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Email;

/// <summary>
/// An email to send: <paramref name="Body"/> is the plain text, <paramref name="Html"/> the same as HTML (see <see cref="EmailDocument"/>);
/// <paramref name="ReplyTo"/> is where an answer goes, when not to the app's own account; <paramref name="UnsubscribeUrl"/> turns
/// on the one-click unsubscribe headers that Gmail and Yahoo require of bulk senders.
/// </summary>
public record OutgoingEmail(string To, string Subject, string Body, string? ReplyTo = null, string? Html = null, string? UnsubscribeUrl = null)
{
    public OutgoingEmail(string to, EmailContent content, string? replyTo = null)
        : this(to, content.Subject, content.Text, replyTo, content.Html, content.OneClickUnsubscribe) { }
}

/// <summary>
/// Emails sent after the request has answered, so an answer never waits on (or reveals, by its timing) a send.
/// In memory: an email queued just before a restart is lost, which the user fixes with "send the link again".
/// </summary>
public class EmailQueue
{
    readonly Channel<OutgoingEmail> channel;

    public EmailQueue(ILogger<EmailQueue> logger) =>
        // In DropWrite mode TryWrite answers true even when it drops the email, so the drop is logged here.
        channel = Channel.CreateBounded<OutgoingEmail>(new BoundedChannelOptions(500) { FullMode = BoundedChannelFullMode.DropWrite },
            dropped => logger.LogWarning("The email queue is full: \"{Subject}\" was dropped.", dropped.Subject));

    public void Enqueue(OutgoingEmail email) => channel.Writer.TryWrite(email);

    public IAsyncEnumerable<OutgoingEmail> ReadAllAsync(CancellationToken cancellationToken) => channel.Reader.ReadAllAsync(cancellationToken);
}

/// <summary>Sends the queued emails one by one. Without email set up, Development logs them (links included) instead.</summary>
public class EmailWorker(EmailQueue queue, IServiceScopeFactory scopes, IHostEnvironment environment, ILogger<EmailWorker> logger)
    : BackgroundService
{
    // True until a send works, so the first email after a restart clears an alert left open; then only after a failure.
    bool failing = true;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var email in queue.ReadAllAsync(stoppingToken))
        {
            using var scope = scopes.CreateScope();
            try
            {
                await scope.ServiceProvider.GetRequiredService<IEmailSender>().SendAsync(email, stoppingToken);
                // Still failing if the alert couldn't be cleared, so the next email tries again.
                if (failing) failing = !await Alerts(scope).ClearAsync(AlertKey, stoppingToken);
            }
            catch (EmailSuppressedException)
            {
                // Already logged by the sender; not a problem with email.
            }
            catch (EmailNotSetUpException) when (environment.IsDevelopment())
            {
                logger.LogInformation("Email isn't set up, so here it is instead.\nTo: {To}\nSubject: {Subject}\n\n{Body}", email.To, email.Subject, email.Body);
            }
            catch (EmailException error)
            {
                // Never the body: it holds single-use links.
                logger.LogWarning("Couldn't send \"{Subject}\": {Reason}", email.Subject, error.Message);
                failing = true;
                await Alerts(scope).RaiseAsync(AlertSource.Email, Alert(email, error is EmailNotSetUpException
                    ? error.Message : "The mail server refused or couldn't send it."), stoppingToken);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                logger.LogError(error, "Sending \"{Subject}\" failed.", email.Subject);
                failing = true;
                await Alerts(scope).RaiseAsync(AlertSource.Email, Alert(email, "The email couldn't be written or sent."), stoppingToken);
            }
        }
    }

    const string AlertKey = "email:account";

    static AdminAlerts Alerts(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<AdminAlerts>();

    /// <summary>Never the address or the body (single-use links): the subject says which kind of email it was.</summary>
    static NewAlert Alert(OutgoingEmail email, string problem) => new(AlertKey, "Account emails failed",
        $"The last one: \"{email.Subject}\". {problem} The count is the emails that failed since the last one that went out.");
}
