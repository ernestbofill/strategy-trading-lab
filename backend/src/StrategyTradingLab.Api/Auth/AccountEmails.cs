using StrategyTradingLab.Api.Email;

namespace StrategyTradingLab.Api.Auth;

/// <summary>The account emails (spec: Sign-up, Forgot and reset password), in English and Catalan.</summary>
public class AccountEmails(EmailQueue queue, IConfiguration configuration)
{
    string AppUrl => (configuration["PublicUrl"] ?? throw new InvalidOperationException("PublicUrl isn't set.")).TrimEnd('/');

    public void Verify(string to, string token, string locale = "en")
    {
        if (locale == "ca")
            queue.Enqueue(new(to, "Confirma el teu correu per a Strategy Trading Lab",
                $"Confirma el teu correu obrint aquest enllaç:\n\n{AppUrl}/verify-email?token={token}\n\n" +
                "Funciona durant 24 hores. Si no t'has registrat a Strategy Trading Lab, ignora aquest correu."));
        else
            queue.Enqueue(new(to, "Confirm your email for Strategy Trading Lab",
                $"Confirm your email by opening this link:\n\n{AppUrl}/verify-email?token={token}\n\n" +
                "It works for 24 hours. If you didn't sign up for Strategy Trading Lab, ignore this email."));
    }

    public void AlreadyRegistered(string to, string locale = "en")
    {
        if (locale == "ca")
            queue.Enqueue(new(to, "Algú ha intentat crear un compte amb el teu correu",
                $"Algú ha intentat crear un compte de Strategy Trading Lab amb aquest correu, però ja en tens un.\n\n" +
                $"Inicia la sessió: {AppUrl}/log-in\nHas oblidat la contrasenya? {AppUrl}/forgot-password\n\n" +
                "Si no has estat tu, pots ignorar aquest correu: res no ha canviat."));
        else
            queue.Enqueue(new(to, "Someone tried to sign up with your email",
                $"Someone tried to create a Strategy Trading Lab account with this email, but you already have one.\n\n" +
                $"Log in: {AppUrl}/log-in\nForgot your password? {AppUrl}/forgot-password\n\n" +
                "If it wasn't you, you can ignore this email: nothing has changed."));
    }

    public void Reset(string to, string token, string locale = "en")
    {
        if (locale == "ca")
            queue.Enqueue(new(to, "Tria una nova contrasenya per a Strategy Trading Lab",
                $"Tria una nova contrasenya obrint aquest enllaç:\n\n{AppUrl}/reset-password?token={token}\n\n" +
                "Funciona durant 1 hora. Si no ho has demanat, ignora aquest correu: la teva contrasenya continua sent la mateixa."));
        else
            queue.Enqueue(new(to, "Choose a new password for Strategy Trading Lab",
                $"Choose a new password by opening this link:\n\n{AppUrl}/reset-password?token={token}\n\n" +
                "It works for 1 hour. If you didn't ask for it, ignore this email: your password stays the same."));
    }
}
