using StrategyTradingLab.Api.Email;

namespace StrategyTradingLab.Api.Tests;

/// <summary>Email links carry the email's language as a path prefix (spec: Language). No database.</summary>
public class AppLinkTests
{
    [Theory]
    [InlineData("ca", "/sign-up?invite=ABCD-EFGH", "https://app.example/ca/sign-up?invite=ABCD-EFGH")]
    [InlineData("en", "/strategies/1/signals/2", "https://app.example/en/strategies/1/signals/2")]
    [InlineData("ca", "/", "https://app.example/ca")]
    [InlineData("fr", "/log-in", "https://app.example/en/log-in")] // A language the app doesn't have opens in English.
    [InlineData("", "/log-in", "https://app.example/en/log-in")]
    public void A_link_starts_with_its_language(string locale, string path, string expected) =>
        Assert.Equal(expected, AppLink.To("https://app.example", locale, path));

    [Fact]
    public void A_path_must_start_with_a_slash() =>
        Assert.Throws<ArgumentException>(() => AppLink.To("https://app.example", "en", "log-in"));
}
