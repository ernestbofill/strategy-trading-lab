using StrategyTradingLab.Api.Email;

namespace StrategyTradingLab.Api.Auth;

/// <summary>The account emails (spec: Sign-up, Forgot and reset password), in English until the Catalan texts exist.</summary>
public class AccountEmails(EmailQueue queue, IConfiguration configuration)
{
    string AppUrl => (configuration["PublicUrl"] ?? throw new InvalidOperationException("PublicUrl isn't set.")).TrimEnd('/');

    public void Verify(string to, string token) => queue.Enqueue(new(to, "Confirm your email for Strategy Trading Lab",
        $"Confirm your email by opening this link:\n\n{AppUrl}/verify-email?token={token}\n\n" +
        "It works for 24 hours. If you didn't sign up for Strategy Trading Lab, ignore this email."));

    public void AlreadyRegistered(string to) => queue.Enqueue(new(to, "Someone tried to sign up with your email",
        $"Someone tried to create a Strategy Trading Lab account with this email, but you already have one.\n\n" +
        $"Log in: {AppUrl}/log-in\nForgot your password? {AppUrl}/forgot-password\n\n" +
        "If it wasn't you, you can ignore this email: nothing has changed."));

    public void Reset(string to, string token) => queue.Enqueue(new(to, "Choose a new password for Strategy Trading Lab",
        $"Choose a new password by opening this link:\n\n{AppUrl}/reset-password?token={token}\n\n" +
        "It works for 1 hour. If you didn't ask for it, ignore this email: your password stays the same."));
}
