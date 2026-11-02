using System.Net.Mail;
using Microsoft.Extensions.Logging.Abstractions;
using StrategyTradingLab.Api.Email;

namespace StrategyTradingLab.Api.Tests;

public class EmailSendingTests
{
    [Fact]
    public async Task A_busy_server_is_tried_once_more_and_a_refusal_is_not()
    {
        var calls = 0;
        await EmailRetry.OnceAsync(() => ++calls == 1 ? throw new EmailException("busy", transient: true) : Task.CompletedTask,
            TimeSpan.Zero, NullLogger.Instance, default);
        Assert.Equal(2, calls);

        calls = 0;
        await Assert.ThrowsAsync<EmailException>(() => EmailRetry.OnceAsync(() => { calls++; throw new EmailException("busy", transient: true); },
            TimeSpan.Zero, NullLogger.Instance, default));
        Assert.Equal(2, calls); // Once more, not forever.

        calls = 0;
        await Assert.ThrowsAsync<EmailException>(() => EmailRetry.OnceAsync(() => { calls++; throw new EmailException("550 no such user"); },
            TimeSpan.Zero, NullLogger.Instance, default));
        Assert.Equal(1, calls);

        calls = 0;
        await Assert.ThrowsAsync<EmailNotSetUpException>(() => EmailRetry.OnceAsync(() => { calls++; throw new EmailNotSetUpException(); },
            TimeSpan.Zero, NullLogger.Instance, default));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void The_one_click_headers_are_the_ones_gmail_and_yahoo_ask_for()
    {
        using var message = new MailMessage("from@example.com", "to@example.com");

        SmtpEmailSender.AddUnsubscribeHeaders(message, "https://app.example/api/email/unsubscribe/tok3n/alerts");

        Assert.Equal("<https://app.example/api/email/unsubscribe/tok3n/alerts>", message.Headers["List-Unsubscribe"]);
        Assert.Equal("List-Unsubscribe=One-Click", message.Headers["List-Unsubscribe-Post"]);
    }
}
