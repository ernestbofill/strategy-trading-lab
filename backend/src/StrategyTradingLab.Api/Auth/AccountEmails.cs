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

    /// <summary>The last email: the account was deleted at the user's request (spec: Account, Delete my account).</summary>
    public void Deleted(string to, string? firstName, string locale = "en")
    {
        var named = !string.IsNullOrWhiteSpace(firstName);
        queue.Enqueue(new(to, locale == "ca"
            ? Email(locale)
                .Paragraph(named ? $"Hola, {firstName}: hem eliminat el teu compte de Strategy Trading Lab, tal com has demanat." : "Hem eliminat el teu compte de Strategy Trading Lab, tal com has demanat.")
                .Paragraph("Les teves estratègies, senyals, operacions i dades s'han esborrat, i totes les sessions s'han tancat. Aquest és l'últim mail que t'enviem.")
                .Paragraph("Si no has estat tu, algú tenia la teva contrasenya: canvia-la als altres llocs on la facis servir.")
                .Build("El teu compte s'ha eliminat")
            : Email(locale)
                .Paragraph(named ? $"Hi {firstName}, we deleted your Strategy Trading Lab account, as you asked." : "We deleted your Strategy Trading Lab account, as you asked.")
                .Paragraph("Your strategies, signals, trades and data are gone, and every session has ended. This is the last email we send you.")
                .Paragraph("If this wasn't you, someone had your password: change it wherever else you use it.")
                .Build("Your account is deleted")));
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

    /// <summary>Welcome email for new users after confirming their email address.</summary>
    public void Welcome(string to, string? firstName, string locale = "en")
    {
        var ca = locale == "ca";
        var welcomeUrl = Link(locale, "/welcome");
        var appUrl = Link(locale, "/settings#app");
        var title = ca
            ? (string.IsNullOrWhiteSpace(firstName) ? "Et donem la benvinguda a Strategy Trading Lab" : $"Et donem la benvinguda, {firstName}")
            : (string.IsNullOrWhiteSpace(firstName) ? "Welcome to Strategy Trading Lab" : $"Welcome, {firstName}");

        var doc = new EmailDocument(locale, AppUrl)
            .Eyebrow(ca ? "Benvinguda" : "Welcome")
            .Heading(title)
            .Paragraph(ca
                ? "Strategy Trading Lab t'ajuda a provar i seguir estratègies d'inversió amb regles senzilles i provades: fes-les servir en simulació o segueix-les manualment al teu broker. L'app mai no opera per tu ni es connecta al teu broker."
                : "Strategy Trading Lab helps you test and follow investing strategies with simple, tested rules — run them as a simulation or follow them manually at your own broker. The app never places orders and never connects to a broker.")
            .Action(ca ? "Descobreix com funcionen el creador d'estratègies, la simulació i el seguiment manual:"
                       : "Learn how the strategy builder, simulation, and manual tracking work:",
                    ca ? "Descobreix com funciona" : "See how it works", welcomeUrl)
            .Section(ca ? "Instal·la l'aplicació al mòbil" : "Install on your phone",
            [
                new(ca ? "Obre l'app amb el Safari, toca el botó Comparteix i tria 'Afegeix a la pantalla d'inici'."
                       : "Open the app in Safari, tap the Share button, and choose 'Add to Home Screen'.", Tag: "iOS"),
                new(ca ? "Obre l'app amb el Chrome o el navegador, toca el menú (⋮) i tria 'Instal·la l'aplicació' o 'Afegeix a la pantalla d'inici'."
                       : "Open the app in Chrome or your browser, tap the menu (⋮), and choose 'Install app' or 'Add to Home screen'.", Tag: "Android"),
            ], url: appUrl)
            .Disclaimer(ca ? "No és assessorament financer. Els senyals segueixen les teves regles."
                           : "Not financial advice. Signals follow your rules.");

        queue.Enqueue(new(to, doc.Build(ca ? "Et donem la benvinguda a Strategy Trading Lab" : "Welcome to Strategy Trading Lab")));
    }
}
