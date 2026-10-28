using StrategyTradingLab.Api.Email;

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
        Assert.Contains("O obre aquest enlla&#231;:", email.Html);
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
