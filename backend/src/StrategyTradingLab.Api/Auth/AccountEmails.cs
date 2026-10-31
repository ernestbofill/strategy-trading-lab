using StrategyTradingLab.Api.Email;

namespace StrategyTradingLab.Api.Auth;

/// <summary>The account emails (spec: Sign-up, Forgot and reset password), in English and Catalan.</summary>
public class AccountEmails(EmailQueue queue, IConfiguration configuration)
{
    string AppUrl => (configuration["PublicUrl"] ?? throw new InvalidOperationException("PublicUrl isn't set.")).TrimEnd('/');

    EmailDocument Email(string locale) => new EmailDocument(locale, AppUrl).Eyebrow(locale == "ca" ? "El teu compte" : "Your account");

    public void Verify(string to, string token, string locale = "en")
    {
        var url = $"{AppUrl}/verify-email?token={token}";
        var reclaimUrl = $"{AppUrl}/forgot-password";
        queue.Enqueue(new(to, locale == "ca"
            ? Email(locale)
                .Action("Confirma el teu correu obrint aquest enllaç:", "Confirma el correu", url)
                .Paragraph("Funciona durant 24 hores. Si no t'has registrat a Strategy Trading Lab, ignora aquest correu: el compte quedarà sense confirmar.")
                .Paragraph($"Per prendre el control del compte, restableix la contrasenya a {reclaimUrl}.")
                .Build("Confirma el teu correu per a Strategy Trading Lab")
            : Email(locale)
                .Action("Confirm your email by opening this link:", "Confirm my email", url)
                .Paragraph("It works for 24 hours. If you didn't sign up for Strategy Trading Lab, ignore this email: the account will stay unconfirmed.")
                .Paragraph($"To take control of the account, reset its password at {reclaimUrl}.")
                .Build("Confirm your email for Strategy Trading Lab")));
    }

    public void AlreadyRegistered(string to, string locale = "en")
    {
        queue.Enqueue(new(to, locale == "ca"
            ? Email(locale)
                .Paragraph("Algú ha intentat crear un compte de Strategy Trading Lab amb aquest correu, però ja en tens un.")
                .Links([("Inicia la sessió", $"{AppUrl}/log-in"), ("Has oblidat la contrasenya?", $"{AppUrl}/forgot-password")])
                .Paragraph("Si no has estat tu, pots ignorar aquest correu: res no ha canviat.")
                .Build("Algú ha intentat crear un compte amb el teu correu")
            : Email(locale)
                .Paragraph("Someone tried to create a Strategy Trading Lab account with this email, but you already have one.")
                .Links([("Log in", $"{AppUrl}/log-in"), ("Forgot your password?", $"{AppUrl}/forgot-password")])
                .Paragraph("If it wasn't you, you can ignore this email: nothing has changed.")
                .Build("Someone tried to sign up with your email")));
    }

    public void Reset(string to, string token, string locale = "en")
    {
        var url = $"{AppUrl}/reset-password?token={token}";
        queue.Enqueue(new(to, locale == "ca"
            ? Email(locale)
                .Action("Tria una nova contrasenya obrint aquest enllaç:", "Tria una contrasenya", url)
                .Paragraph("Funciona durant 1 hora. Si no ho has demanat, ignora aquest correu: la teva contrasenya continua sent la mateixa.")
                .Build("Tria una nova contrasenya per a Strategy Trading Lab")
            : Email(locale)
                .Action("Choose a new password by opening this link:", "Choose a new password", url)
                .Paragraph("It works for 1 hour. If you didn't ask for it, ignore this email: your password stays the same.")
                .Build("Choose a new password for Strategy Trading Lab")));
    }
}
