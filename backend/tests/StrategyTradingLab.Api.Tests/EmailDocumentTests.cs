using Microsoft.Extensions.Configuration;
using StrategyTradingLab.Api.Email;
using StrategyTradingLab.Api.Invites;

namespace StrategyTradingLab.Api.Tests;

public class EmailDocumentTests
{
    [Fact]
    public void The_text_and_the_html_say_the_same_and_the_text_ends_with_the_disclaimer()
    {
        var email = new EmailDocument("en", "https://app.example").Eyebrow("Signal · Live")
            .Paragraph("You asked to be reminded.")
            .Box("Your rules are met.\nClose on 22 Oct 2026: $187.5")
            .Action("See it:", "See the signal", "https://app.example/s/1")
            .Section("Your strategies", [new("Dips (Live): since 1 Oct 2026", Tag: "Live", Live: true)])
            .Disclaimer("Not financial advice. Signals follow your rules.")
            .Build("Subject");

        Assert.Equal("You asked to be reminded.\n\nYour rules are met.\nClose on 22 Oct 2026: $187.5\n\nSee it:\nhttps://app.example/s/1\n\n" +
            "Your strategies\n- (Live) Dips (Live): since 1 Oct 2026\n\nNot financial advice. Signals follow your rules.", email.Text);
        foreach (var part in new[] { "lang=\"en\"", "Signal &#183; Live", "Close on 22 Oct 2026: $187.5", "href=\"https://app.example/s/1\"", "See the signal",
                     "Your strategies", ">Live</span>", "Not financial advice. Signals follow your rules.", "href=\"https://app.example/\"" })
            Assert.Contains(part, email.Html);
        Assert.DoesNotContain("&#9650;", email.Html);
        Assert.DoesNotContain("Or open this link:", email.Html);
    }

    [Fact]
    public void Action_without_lead_emits_no_lead_paragraph()
    {
        var email = new EmailDocument("en", "https://app.example")
            .Action("Accept the invite", "https://app.example/sign-up?invite=ABCD-EFGH")
            .Build("Invite");

        Assert.Equal("https://app.example/sign-up?invite=ABCD-EFGH", email.Text);
        Assert.Contains("Accept the invite", email.Html);
        Assert.Contains("href=\"https://app.example/sign-up?invite=ABCD-EFGH\"", email.Html);
        Assert.DoesNotContain("Or open this link:", email.Html);
    }

    [Fact]
    public void Code_block_formats_label_code_and_hint()
    {
        var email = new EmailDocument("en", "https://app.example")
            .Code("Your code", "DVN6-2PTF", "Or enter this code on sign-up.")
            .Build("Invite");

        Assert.Equal("Your code: DVN6-2PTF\nOr enter this code on sign-up.", email.Text);
        Assert.Contains("Your code", email.Html);
        Assert.Contains("DVN6-2PTF", email.Html);
        Assert.Contains("Or enter this code on sign-up.", email.Html);
        Assert.Contains("bgcolor=\"#100d23\"", email.Html);
        Assert.Contains("white-space:nowrap", email.Html);

        var withoutHint = new EmailDocument("en", "https://app.example")
            .Code("Your code", "DVN6-2PTF")
            .Build("Invite");

        Assert.Equal("Your code: DVN6-2PTF", withoutHint.Text);
        Assert.Contains("Your code", withoutHint.Html);
        Assert.Contains("DVN6-2PTF", withoutHint.Html);
    }

    [Fact]
    public void Code_values_are_encoded_in_the_html()
    {
        var email = new EmailDocument().Code("Label <tag>", "CODE & 123", "Hint \"safe\" <alert>").Build("Code");

        Assert.DoesNotContain("<tag>", email.Html);
        Assert.DoesNotContain("<alert>", email.Html);
        Assert.Contains("Label &lt;tag&gt;", email.Html);
        Assert.Contains("CODE &amp; 123", email.Html);
        Assert.Contains("Hint &quot;safe&quot; &lt;alert&gt;", email.Html);
        Assert.Equal("Label <tag>: CODE & 123\nHint \"safe\" <alert>", email.Text);
    }

    [Fact]
    public void Invite_email_is_formatted_in_english_and_catalan_with_or_without_from()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["PublicUrl"] = "https://app.example" }).Build();
        var texts = new InviteTexts(config);

        var enAnon = texts.Email("ABCD-EFGH", "en", "Alice", null);
        Assert.Equal("You're invited to Strategy Trading Lab", enAnon.Subject);
        Assert.Contains("Hi Alice!", enAnon.Text);
        Assert.Contains("Accept the invite", enAnon.Html);
        Assert.Contains("https://app.example/sign-up?invite=ABCD-EFGH", enAnon.Html);
        Assert.Contains("Your code", enAnon.Html);
        Assert.Contains("ABCD-EFGH", enAnon.Html);
        Assert.Contains("Or enter this code on sign-up.", enAnon.Html);

        var caNamed = texts.Email("ABCD-EFGH", "ca", "Jordi", "Ernest");
        Assert.Equal("Ernest t'ha convidat a Strategy Trading Lab", caNamed.Subject);
        Assert.Contains("Hola, Jordi!", caNamed.Text);
        Assert.Contains("https://app.example/sign-up?invite=ABCD-EFGH", caNamed.Text);
        Assert.Contains("Accepta la invitaci&#243;", caNamed.Html);
        Assert.Contains("El teu codi", caNamed.Html);
        Assert.Contains("ABCD-EFGH", caNamed.Html);
        Assert.Contains("O escriu aquest codi en registrar-te.", caNamed.Html);
    }

    [Fact]
    public void User_values_are_encoded_in_the_html()
    {
        var email = new EmailDocument().Quote("About", "<script>alert(1)</script>").Paragraph("Tom & \"Jerry\"\nline two").Build("<b>Subject</b>");

        Assert.DoesNotContain("<script>", email.Html);
        Assert.DoesNotContain("<b>Subject</b>", email.Html);
        Assert.Contains("&lt;script&gt;", email.Html);
        Assert.Contains("Tom &amp; &quot;Jerry&quot;<br>line two", email.Html);
        Assert.Equal("About: \"<script>alert(1)</script>\"\n\nTom & \"Jerry\"\nline two", email.Text);
    }

    [Fact]
    public void A_catalan_email_is_in_catalan()
    {
        var email = new EmailDocument("ca", "https://app.example").Action("Confirma:", "Confirma el correu", "https://app.example/v").Build("Assumpte");

        Assert.Contains("lang=\"ca\"", email.Html);
        Assert.DoesNotContain("O obre aquest enlla", email.Html);
        Assert.Contains("Confirma el correu", email.Html);
        Assert.Contains("opacity:0;color:#100d23;\">Confirma:</div>", email.Html); // The inbox preview is the call to action.
        Assert.Contains("Obre l&#39;aplicaci&#243;", email.Html);
    }

    [Fact]
    public void A_long_preview_is_cut_without_splitting_an_emoji()
    {
        var html = new EmailDocument().Paragraph(new string('a', 138) + "😀" + new string('b', 10)).Build("S").Html;

        Assert.Contains(new string('a', 138) + "…</div>", html);
    }
}
