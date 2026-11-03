using StrategyTradingLab.Api.Email;

namespace StrategyTradingLab.Api.Invites;

/// <summary>
/// What an invite says, in its language (spec: Admin, Invites): the message to copy into WhatsApp or anywhere else, and
/// the email. Both carry the link and the code, so the invitee can open one or type the other on Sign-up.
/// Describe, don't instruct: what the app is and that it never trades for you, no urgency.
/// </summary>
public class InviteTexts(IConfiguration configuration)
{
    string AppUrl => AppLink.AppUrl(configuration);

    /// <summary>Sign-up in the invite's language, so the invitee's first page is in it (spec: Language).</summary>
    public string SignUpUrl(string locale) => AppLink.To(AppUrl, locale, "/sign-up");

    public string Link(string code, string locale) => $"{SignUpUrl(locale)}?invite={Uri.EscapeDataString(code)}";

    /// <summary>The message to copy, signed with the inviter's first name when they have one.</summary>
    public string Message(string code, string locale, string firstName, string? from)
    {
        var sign = from is null ? "" : $"\n\n– {from}";
        return locale == "ca"
            ? $"""
              Hola, {firstName}! T'he convidat a Strategy Trading Lab, una app per provar i seguir estratègies d'inversió amb les teves pròpies regles. És una versió "Alpha" de prova, disponible només per invitació. És simulació, sense diners reals, la app mai no opera per tu ni es connecta al teu bròquer.

              Entra amb aquest enllaç: {Link(code, locale)}
              O registra't a {SignUpUrl(locale)} amb el codi {code}{sign}
              """
            : $"""
              Hi {firstName}! I've invited you to Strategy Trading Lab, an app to test and follow investing strategies with your own rules. It is in a closed alpha, available by invitation only. Try them in simulation first, with no real money. It never trades for you or connects to your broker.

              Join with this link: {Link(code, locale)}
              Or sign up at {SignUpUrl(locale)} with the code {code}{sign}
              """;
    }

    public EmailContent Email(string code, string locale, string firstName, string? from)
    {
        var ca = locale == "ca";
        var title = (ca, from) switch
        {
            (true, null) => "Tens una invitació a Strategy Trading Lab",
            (true, _) => $"{from} t'ha convidat a Strategy Trading Lab",
            (false, null) => "You're invited to Strategy Trading Lab",
            _ => $"{from} invited you to Strategy Trading Lab",
        };
        var doc = new EmailDocument(locale, AppUrl)
            .Eyebrow(ca ? "Invitació · Alfa tancada" : "Invitation · Closed alpha")
            .Heading(title);
        return (ca
                ? doc
                    .Paragraph($"Hola, {firstName}!")
                    .Paragraph("Strategy Trading Lab és una app per provar i seguir estratègies d'inversió amb les teves pròpies regles. Actualment és en una versió \"Alpha\" de prova, disponible només per invitació. És simulació, sense diners reals, la app mai no opera per tu ni es connecta al teu bròquer.")
                    .Action("Accepta la invitació", Link(code, locale))
                    .Code("El teu codi", code, "O escriu aquest codi en registrar-te.")
                : doc
                    .Paragraph($"Hi {firstName}!")
                    .Paragraph("Strategy Trading Lab is an app to test and follow investing strategies with your own rules. It is currently in a closed alpha, available by invitation only. Start in simulation, with no real money. It never trades for you or connects to your broker.")
                    .Action("Accept the invite", Link(code, locale))
                    .Code("Your code", code, "Or enter this code on sign-up."))
            .Build(title);
    }
}
