using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using StrategyTradingLab.Api.Admin;

namespace StrategyTradingLab.Api.Email;

/// <summary>Why an email wasn't sent, in words for the admin page. Never holds a password.</summary>
public class EmailException(string message, Exception? inner = null, bool transient = false) : Exception(message, inner)
{
    /// <summary>Whether trying again a moment later may work (a busy server, a timeout), not a refusal.</summary>
    public bool Transient => transient;
}

/// <summary>A failed send is tried once more after a short wait, when the failure may be passing.</summary>
public static class EmailRetry
{
    public static readonly TimeSpan Wait = TimeSpan.FromSeconds(3);

    public static async Task OnceAsync(Func<Task> send, TimeSpan wait, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            await send();
        }
        catch (EmailException error) when (error.Transient && !cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Sending an email failed, trying once more in {Seconds}s.", wait.TotalSeconds);
            await Task.Delay(wait, cancellationToken);
            await send();
        }
    }
}

/// <summary>Email isn't set up yet (no account, server or password): nothing was sent and no server was contacted.</summary>
public class EmailNotSetUpException() : EmailException("Email isn't set up: set the account, the server and the password first.");

/// <summary>Sends the app's emails.</summary>
public interface IEmailSender
{
    /// <summary>Sends an email to one address, with answers going to its ReplyTo when it is set. Throws <see cref="EmailException"/>.</summary>
    Task SendAsync(OutgoingEmail email, CancellationToken cancellationToken);
}

/// <summary>
/// SMTP with STARTTLS, set on the admin page (Gmail with an app password, as in the POC). Each send reads all the
/// settings, the password included, in one <see cref="SettingsStore.ValuesAsync"/> call, so they always belong together.
/// </summary>
public class SmtpEmailSender(SettingsStore settings, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    /// <summary>Sends to <paramref name="email"/>'s address with the admin page's settings.</summary>
    public async Task SendAsync(OutgoingEmail email, CancellationToken cancellationToken)
    {
        var values = await settings.ValuesAsync(cancellationToken);
        if (values["SMTP_USER"] is not { } user || values["SMTP_HOST"] is not { } host || values["SMTP_PASSWORD"] is not { } password)
            throw new EmailNotSetUpException();
        await EmailRetry.OnceAsync(() => SendAsync(values, user, password, host, email, cancellationToken), EmailRetry.Wait, logger, cancellationToken);
    }

    async Task SendAsync(IReadOnlyDictionary<string, string?> values, string user, string password, string host, OutgoingEmail email,
        CancellationToken cancellationToken)
    {
        var (to, subject, body, replyTo, html, unsubscribeUrl) = email;
        var port = int.TryParse(values["SMTP_PORT"], out var p) ? p : 587;
        MailMessage message;
        SmtpClient client;
        try
        {
            // MailAddress takes exactly one address, so a value like "a@x.com,b@y.com" can never add a recipient.
            message = new MailMessage(new MailAddress(values["SMTP_FROM"] ?? user), new MailAddress(to)) {
                Subject = subject, SubjectEncoding = Encoding.UTF8, Body = body, BodyEncoding = Encoding.UTF8,
            };
            // multipart/alternative: clients that show HTML show it, the rest the plain text, which says the same.
            // Quoted-printable: the HTML is one long line, over SMTP's 998-character limit if sent as 7bit.
            if (html is not null)
            {
                var view = AlternateView.CreateAlternateViewFromString(html, Encoding.UTF8, MediaTypeNames.Text.Html);
                view.TransferEncoding = TransferEncoding.QuotedPrintable;
                message.AlternateViews.Add(view);
            }
            if (replyTo is not null) message.ReplyToList.Add(new MailAddress(replyTo));
            // One click, as Gmail and Yahoo ask of bulk senders (RFC 8058): the mail app POSTs to the address, with no page in between.
            if (unsubscribeUrl is not null)
            {
                message.Headers.Add("List-Unsubscribe", $"<{unsubscribeUrl}>");
                message.Headers.Add("List-Unsubscribe-Post", "List-Unsubscribe=One-Click");
            }
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
                    : $"{host}:{port} refused it: {Reason(error)}", error, IsTransient(error));
            }
        }
    }

    /// <summary>A timeout, a dropped connection or the server saying "busy, try later" (4xx): worth one more try. A refusal or a bad password isn't.</summary>
    static bool IsTransient(Exception error) => error switch
    {
        OperationCanceledException or IOException => true,
        SmtpException { StatusCode: SmtpStatusCode.ServiceNotAvailable or SmtpStatusCode.MailboxBusy or SmtpStatusCode.LocalErrorInProcessing
            or SmtpStatusCode.InsufficientStorage or SmtpStatusCode.GeneralFailure } => true,
        _ => false,
    };

    /// <summary>The server's own words (e.g. Gmail's "Username and Password not accepted"), which never contain the password.</summary>
    static string Reason(Exception error) =>
        error is SmtpException { InnerException: { } inner } smtp ? $"{smtp.Message} ({inner.Message})" : error.Message;
}
