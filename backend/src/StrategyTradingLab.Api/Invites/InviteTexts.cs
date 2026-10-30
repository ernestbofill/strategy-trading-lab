using StrategyTradingLab.Api.Email;

namespace StrategyTradingLab.Api.Invites;

/// <summary>
/// What an invite says, in its language (spec: Admin, Invites): the message to copy into WhatsApp or anywhere else, and
/// the email. Both carry the link and the code, so the invitee can open one or type the other on Sign-up.
/// Describe, don't instruct: what the app is and that it never trades for you, no urgency.
/// </summary>
public class InviteTexts(IConfiguration configuration)
{
    string AppUrl => (configuration["PublicUrl"] ?? throw new InvalidOperationException("PublicUrl isn't set.")).TrimEnd('/');

    public string SignUpUrl => $"{AppUrl}/sign-up";

    public string Link(string code) => $"{SignUpUrl}?invite={Uri.EscapeDataString(code)}";

    /// <summary>The message to copy, signed with the inviter's first name when they have one.</summary>
    public string Message(string code, string locale, string firstName, string? from)
    {
        var sign = from is null ? "" : $"\n\n– {from}";
        return locale == "ca"
            ? $"""
              Hola, {firstName}! T'he convidat a Strategy Trading Lab, una app per provar i seguir estratègies d'inversió amb les teves pròpies regles. Primer en simulació, sense diners reals. Mai no opera per tu ni es connecta al teu bròquer.

              Entra amb aquest enllaç: {Link(code)}
              O registra't a {SignUpUrl} amb el codi {code}{sign}
              """
            : $"""
              Hi {firstName}! I've invited you to Strategy Trading Lab, an app to test and follow investing strategies with your own rules. Try them in simulation first, with no real money. It never trades for you or connects to your broker.

              Join with this link: {Link(code)}
              Or sign up at {SignUpUrl} with the code {code}{sign}
              """;
    }

    public EmailContent Email(string code, string locale, string firstName, string? from)
    {
        var ca = locale == "ca";
        var document = new EmailDocument(locale, AppUrl).Eyebrow(ca ? "Invitació" : "Invitation");
        return (ca
                ? document
                    .Heading(from is null ? "Tens una invitació a Strategy Trading Lab" : $"{from} t'ha convidat a Strategy Trading Lab")
                    .Paragraph($"Hola, {firstName}!")
                    .Paragraph("Strategy Trading Lab és una app per provar i seguir estratègies d'inversió amb les teves pròpies regles. Comença en simulació, sense diners reals. Mai no opera per tu ni es connecta al teu bròquer.")
                    .Action("La teva invitació és a punt:", "Accepta la invitació", Link(code))
                    .Box($"O registra't a {SignUpUrl} amb el codi:\n{code}")
                : document
                    .Heading(from is null ? "You're invited to Strategy Trading Lab" : $"{from} invited you to Strategy Trading Lab")
                    .Paragraph($"Hi {firstName}!")
                    .Paragraph("Strategy Trading Lab is an app to test and follow investing strategies with your own rules. Start in simulation, with no real money. It never trades for you or connects to your broker.")
                    .Action("Your invite is ready:", "Accept the invite", Link(code))
                    .Box($"Or sign up at {SignUpUrl} with the code:\n{code}"))
            .Build(ca
                ? from is null ? "Tens una invitació a Strategy Trading Lab" : $"{from} t'ha convidat a Strategy Trading Lab"
                : from is null ? "You're invited to Strategy Trading Lab" : $"{from} invited you to Strategy Trading Lab");
    }
}
