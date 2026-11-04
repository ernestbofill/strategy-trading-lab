using StrategyTradingLab.Api.Email;

namespace StrategyTradingLab.Api.Auth;

/// <summary>The account emails (spec: Sign-up, Forgot and reset password), in English and Catalan.</summary>
public class AccountEmails(EmailQueue queue, IConfiguration configuration)
{
    string AppUrl => AppLink.AppUrl(configuration);

    /// <summary>A page of the app, in the email's language (spec: Language).</summary>
    string Link(string locale, string path) => AppLink.To(AppUrl, locale, path);

    EmailDocument Email(string locale) => new EmailDocument(locale, AppUrl).Eyebrow(locale == "ca" ? "El teu compte" : "Your account");

    public void Verify(string to, string token, string locale = "en")
    {
        var url = Link(locale, $"/verify-email?token={token}");
        var reset = Link(locale, "/forgot-password");
        // A stranger may have signed up with this email first (backend/AUTH.md): the owner can ignore it, or take the
        // account back with a reset, which ends every other session.
        queue.Enqueue(new(to, locale == "ca"
            ? Email(locale)
                .Action("Confirma el teu mail obrint aquest enllaç:", "Confirma el mail", url)
                .Paragraph("Funciona durant 24 hores. Si no t'has registrat a Strategy Trading Lab, ignora aquest mail: el compte queda sense confirmar.")
                .Paragraph("Si algú altre s'ha registrat amb el teu mail, pots recuperar el compte: una contrasenya nova tanca totes les altres sessions.")
                .Links([("Has oblidat la contrasenya?", reset)])
                .Build("Confirma el teu mail per a Strategy Trading Lab")
            : Email(locale)
                .Action("Confirm your email by opening this link:", "Confirm my email", url)
                .Paragraph("It works for 24 hours. If you didn't sign up for Strategy Trading Lab, ignore this email: the account stays unconfirmed.")
                .Paragraph("If someone else signed up with your email, you can take the account back: a new password ends every other session.")
                .Links([("Forgot your password?", reset)])
                .Build("Confirm your email for Strategy Trading Lab")));
    }

    public void AlreadyRegistered(string to, string locale = "en")
    {
        queue.Enqueue(new(to, locale == "ca"
            ? Email(locale)
                .Paragraph("Algú ha intentat crear un compte de Strategy Trading Lab amb aquest mail, però ja en tens un.")
                .Links([("Inicia sessió", Link(locale, "/log-in")), ("Has oblidat la contrasenya?", Link(locale, "/forgot-password"))])
                .Paragraph("Si no has estat tu, pots ignorar aquest mail: res no ha canviat.")
                .Build("Algú ha intentat crear un compte amb el teu mail")
            : Email(locale)
                .Paragraph("Someone tried to create a Strategy Trading Lab account with this email, but you already have one.")
                .Links([("Log in", Link(locale, "/log-in")), ("Forgot your password?", Link(locale, "/forgot-password"))])
                .Paragraph("If it wasn't you, you can ignore this email: nothing has changed.")
                .Build("Someone tried to sign up with your email")));
    }

    public void Reset(string to, string token, string locale = "en")
    {
        var url = Link(locale, $"/reset-password?token={token}");
        queue.Enqueue(new(to, locale == "ca"
            ? Email(locale)
                .Action("Tria una nova contrasenya obrint aquest enllaç:", "Tria una contrasenya", url)
                .Paragraph("Funciona durant 1 hora. Si no ho has demanat, ignora aquest mail: la teva contrasenya continua sent la mateixa.")
                .Build("Tria una nova contrasenya per a Strategy Trading Lab")
            : Email(locale)
                .Action("Choose a new password by opening this link:", "Choose a new password", url)
                .Paragraph("It works for 1 hour. If you didn't ask for it, ignore this email: your password stays the same.")
                .Build("Choose a new password for Strategy Trading Lab")));
    }

    /// <summary>Warns that an unconfirmed account will be deleted in 7 days unless its email is confirmed.</summary>
    public void UnconfirmedWarning(string to, string token, string locale = "en")
    {
        var url = Link(locale, $"/verify-email?token={token}");
        queue.Enqueue(new(to, locale == "ca"
            ? Email(locale)
                .Action("Confirma el teu mail obrint aquest enllaç:", "Confirma el mail", url)
                .Paragraph("Funciona durant 24 hores. Fa 23 dies que vas crear el compte, però encara no s'ha confirmat.")
                .Paragraph("Per mantenir el compte, confirma el mail durant els propers 7 dies: els comptes que no es confirmen s'eliminen al cap de 30 dies.")
                .Paragraph("Si no t'has registrat a Strategy Trading Lab, ignora aquest mail: s'eliminarà automàticament.")
                .Build("Confirma el teu mail per mantenir el teu compte de Strategy Trading Lab")
            : Email(locale)
                .Action("Confirm your email by opening this link:", "Confirm my email", url)
                .Paragraph("It works for 24 hours. You created your account 23 days ago, but the email hasn't been confirmed yet.")
                .Paragraph("To keep your account, confirm your email within the next 7 days: accounts never confirmed are deleted after 30 days.")
                .Paragraph("If you didn't sign up for Strategy Trading Lab, ignore this email: it will be deleted automatically.")
                .Build("Confirm your email to keep your Strategy Trading Lab account")));
    }
}
