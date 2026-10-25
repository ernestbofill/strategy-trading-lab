namespace StrategyTradingLab.Api.Auth;

/// <summary>
/// The language of a request: the app sends exactly <c>en</c> or <c>ca</c> in Accept-Language on every request (the user's
/// choice; nothing is guessed from the browser). Anything else, or no header, reads as English.
/// </summary>
public static class RequestLocale
{
    public static string From(HttpRequest request) => From(request.Headers.AcceptLanguage.ToString());

    public static string From(string? acceptLanguage) =>
        acceptLanguage is not null && acceptLanguage.TrimStart().StartsWith("ca", StringComparison.OrdinalIgnoreCase) ? "ca" : "en";
}
