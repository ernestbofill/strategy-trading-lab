using System.Net;
using System.Net.Mail;
using StrategyTradingLab.Api.Admin;

namespace StrategyTradingLab.Api.Email;

/// <summary>Why an email wasn't sent, in words for the admin page. Never holds a password.</summary>
public class EmailException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Sends the app's emails.</summary>
public interface IEmailSender
{
    /// <summary>The address the account sends from (SMTP_USER), or null when email isn't set up.</summary>
    Task<string?> AccountAsync(CancellationToken cancellationToken);

    /// <summary>Sends a plain-text email. Throws <see cref="EmailException"/> when it can't.</summary>
    Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken);
}

/// <summary>
/// SMTP with STARTTLS, set on the admin page (Gmail with an app password, as in the POC). All the settings, the
/// password included, come from one <see cref="SettingsStore.ValuesAsync"/> call, so they always belong together.
/// </summary>
public class SmtpEmailSender(SettingsStore settings, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    public async Task<string?> AccountAsync(CancellationToken cancellationToken) =>
        (await settings.ValuesAsync(cancellationToken))["SMTP_USER"];

    public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken)
    {
        var values = await settings.ValuesAsync(cancellationToken);
        if (values["SMTP_USER"] is not { } user || values["SMTP_HOST"] is not { } host || values["SMTP_PASSWORD"] is not { } password)
            throw new EmailException("Email isn't set up: set the account, the server and the password first.");
        var port = int.TryParse(values["SMTP_PORT"], out var p) ? p : 587;

        using var message = new MailMessage(values["SMTP_FROM"] ?? user, to, subject, body);
        using var client = new SmtpClient(host, port)
        {
            EnableSsl = true,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(user, password),
            Timeout = (int)Timeout.TotalMilliseconds,
        };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        try
        {
            await client.SendMailAsync(message, timeout.Token);
            logger.LogInformation("Sent \"{Subject}\" through {Host}:{Port}.", subject, host, port);
        }
        catch (Exception error) when (error is SmtpException or OperationCanceledException or IOException or System.Security.Authentication.AuthenticationException)
        {
            logger.LogWarning(error, "Sending \"{Subject}\" through {Host}:{Port} failed.", subject, host, port);
            throw new EmailException(error is OperationCanceledException
                ? $"{host}:{port} didn't answer in time."
                : $"{host}:{port} refused it: {Reason(error)}", error);
        }
    }

    /// <summary>The server's own words (e.g. Gmail's "Username and Password not accepted"), which never contain the password.</summary>
    static string Reason(Exception error) =>
        error is SmtpException { InnerException: { } inner } smtp ? $"{smtp.Message} ({inner.Message})" : error.Message;
}
