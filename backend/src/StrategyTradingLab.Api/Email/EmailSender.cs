using System.Net;
using System.Net.Mail;
using StrategyTradingLab.Api.Admin;

namespace StrategyTradingLab.Api.Email;

/// <summary>Why an email wasn't sent, in words for the admin page. Never holds a password.</summary>
public class EmailException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Email isn't set up yet (no account, server or password): nothing was sent and no server was contacted.</summary>
public class EmailNotSetUpException() : EmailException("Email isn't set up: set the account, the server and the password first.");

/// <summary>Sends the app's emails.</summary>
public interface IEmailSender
{
    /// <summary>Sends a plain-text email to the mail account itself and returns its address. Throws <see cref="EmailException"/>.</summary>
    Task<string> SendToAccountAsync(string subject, string body, CancellationToken cancellationToken);

    /// <summary>Sends a plain-text email to one address. Throws <see cref="EmailException"/>.</summary>
    Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken);
}

/// <summary>
/// SMTP with STARTTLS, set on the admin page (Gmail with an app password, as in the POC). Each send reads all the
/// settings, the password included, in one <see cref="SettingsStore.ValuesAsync"/> call, so they always belong together.
/// </summary>
public class SmtpEmailSender(SettingsStore settings, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    public Task<string> SendToAccountAsync(string subject, string body, CancellationToken cancellationToken) =>
        SendFromSettingsAsync(null, subject, body, cancellationToken);

    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken) =>
        SendFromSettingsAsync(to, subject, body, cancellationToken);

    /// <summary>Sends to <paramref name="to"/>, or to the account itself when it is null, and returns the account.</summary>
    async Task<string> SendFromSettingsAsync(string? to, string subject, string body, CancellationToken cancellationToken)
    {
        var values = await settings.ValuesAsync(cancellationToken);
        if (values["SMTP_USER"] is not { } user || values["SMTP_HOST"] is not { } host || values["SMTP_PASSWORD"] is not { } password)
            throw new EmailNotSetUpException();
        await SendAsync(values, user, password, host, to ?? user, subject, body, cancellationToken);
        return user;
    }

    async Task SendAsync(IReadOnlyDictionary<string, string?> values, string user, string password, string host, string to,
        string subject, string body, CancellationToken cancellationToken)
    {
        var port = int.TryParse(values["SMTP_PORT"], out var p) ? p : 587;
        MailMessage message;
        SmtpClient client;
        try
        {
            // MailAddress takes exactly one address, so a value like "a@x.com,b@y.com" can never add a recipient.
            message = new MailMessage(new MailAddress(values["SMTP_FROM"] ?? user), new MailAddress(to)) { Subject = subject, Body = body };
            client = new SmtpClient(host, port)
            {
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(user, password),
                Timeout = (int)Timeout.TotalMilliseconds,
            };
        }
        catch (Exception error) when (error is FormatException or ArgumentException)
        {
            throw new EmailException($"The email settings can't be used: {error.Message}", error);
        }

        using (message)
        using (client)
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            timeout.CancelAfter(Timeout);
            try
            {
                await client.SendMailAsync(message, timeout.Token);
                logger.LogInformation("Sent an email through {Host}:{Port}.", host, port); // Not the subject: it holds names.
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw; // The request went away (a closed tab), not the server.
            }
            catch (Exception error) when (error is SmtpException or OperationCanceledException or IOException
                or System.Security.Authentication.AuthenticationException)
            {
                logger.LogWarning(error, "Sending \"{Subject}\" through {Host}:{Port} failed.", subject, host, port);
                throw new EmailException(error is OperationCanceledException
                    ? $"{host}:{port} didn't answer in time."
                    : $"{host}:{port} refused it: {Reason(error)}", error);
            }
        }
    }

    /// <summary>The server's own words (e.g. Gmail's "Username and Password not accepted"), which never contain the password.</summary>
    static string Reason(Exception error) =>
        error is SmtpException { InnerException: { } inner } smtp ? $"{smtp.Message} ({inner.Message})" : error.Message;
}
