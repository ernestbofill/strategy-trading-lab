using StrategyTradingLab.Api.Auth;

namespace StrategyTradingLab.Api.Email;

/// <summary>
/// The app's addresses in emails and invite messages (spec: Language). Each starts with the language of the email that holds
/// it, as a path prefix (<c>https://…/ca/sign-up?invite=…</c>), so the page opens in that language before anyone has logged
/// in. The app reads the prefix, keeps the language on the device (unless an account is signed in: its language wins) and
/// drops the prefix from the address. The base is <c>PublicUrl</c>, never the request's Host header.
/// </summary>
public static class AppLink
{
    /// <summary>
    /// <paramref name="path"/> (starting with "/") in <paramref name="locale"/> on <paramref name="appUrl"/> (without a
    /// trailing "/"). An unknown language gets the default, English, so a link always opens a page the app has.
    /// </summary>
    public static string To(string appUrl, string locale, string path = "/")
    {
        if (!path.StartsWith('/')) throw new ArgumentException("An app path starts with \"/\".", nameof(path));
        var language = AccountRules.Locales.Contains(locale) ? locale : AccountRules.Locales[0];
        return $"{appUrl}/{language}{(path == "/" ? "" : path)}";
    }

    /// <summary>The configured <c>PublicUrl</c> without its trailing "/". Emails can't be built without it.</summary>
    public static string AppUrl(IConfiguration configuration) =>
        (configuration["PublicUrl"] ?? throw new InvalidOperationException("PublicUrl isn't set.")).TrimEnd('/');
}
