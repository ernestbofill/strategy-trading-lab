using System.Globalization;

namespace StrategyTradingLab.Api.Admin;

/// <summary>
/// A setting the admin page can set. Only these: anything the API needs to start stays in the server's environment
/// (api.env, .env.local), the database URLs included: the page only switches between them (<see cref="DatabaseSwitch"/>). The page's value wins over the server's, which wins over the default.
/// </summary>
/// <param name="BoundTo">For a secret: the settings it belongs with. It only counts as set while they keep the values
/// they had when it was saved, so it is never sent somewhere new (a mail server swapped in by whoever knows the page's password).</param>
public record SettingDefinition(
    string Key,
    string Group,
    string Description,
    string? Default = null,
    bool Secret = false,
    Func<string, string?>? Validate = null,
    IReadOnlyList<string>? BoundTo = null);

public static class SettingDefinitions
{
    public static readonly IReadOnlyList<SettingDefinition> All =
    [
        // Defaults are Ernest's Gmail, as in the POC, so only the app password has to be typed (Ernest's call); the sender
        // follows the account. The password comes last: changing a setting above it unsets it, so fill the page top to bottom.
        new("SMTP_USER", "Email", "The email account that sends the emails.", Default: GmailAccount, Validate: Email),
        new("SMTP_HOST", "Email", "The mail server.", Default: "smtp.gmail.com", Validate: Host),
        new("SMTP_PORT", "Email", "The mail server's port (STARTTLS).", Default: "587", Validate: Port),
        new("SMTP_FROM", "Email", "The sender address. Empty: the account above.", Validate: Email),
        new("SMTP_PASSWORD", "Email",
            "The account's password: for Gmail, an app password. It only counts while the settings above stay as they were when it was saved.",
            Secret: true, BoundTo: ["SMTP_USER", "SMTP_HOST", "SMTP_PORT", "SMTP_FROM"]),

        // Signal alerts and the daily overview (Notifications/NotificationWorker).
        new("EMAILS_ENABLED", "Notifications", "Send the signal alerts and the daily overviews. Only one running copy may: the server. Off on a development computer.",
            Default: "true", Validate: Bool),

        // The daily price refresh (MarketData/PriceRefreshWorker). The first value is what it uses until someone sets one here.
        new("PRICES_SCHEDULE_ENABLED", "Prices", "Run the daily price refresh. Only one running copy may: the server. Off on a development computer.",
            Default: "true", Validate: Bool),
        new("PRICES_TIME_ZONE", "Prices", "The time zone for the time below, e.g. Europe/Madrid.", Default: "Europe/Madrid", Validate: TimeZone),
        new("PRICES_DAILY_TIME", "Prices", "When the daily refresh starts, HH:MM (after the US close, which is 22:00 in Madrid).",
            Default: "22:30", Validate: Time),

        // Who can sign up (Auth/AccountEndpoints, spec: Sign-up). Open while the app is built; invite only from the quiet launch.
        new(SignUps, "Sign-ups", "Who can sign up: open (anyone) or invite_only (only with an invite, made on /admin/invites).",
            Default: SignUpsOpen, Validate: SignUpMode),

        // "+ Add a stock" and the fund picker (Assets/AssetEndpoints). Picking one already in the app never counts.
        new(UserAssetLimit, "Assets", "How many new stocks and funds one user can add to the app. 0: none.",
            Default: "30", Validate: WholeNumber),
    ];

    public const string UserAssetLimit = "USER_ASSET_LIMIT";
    public const string SignUps = "SIGN_UPS", SignUpsOpen = "open", SignUpsInviteOnly = "invite_only";

    const string GmailAccount = "nest.u333@gmail.com";

    public static SettingDefinition? Find(string key) => All.FirstOrDefault(setting => setting.Key == key);

    /// <summary>Exactly one plain address: no display name, no list ("a@x.com,b@y.com" would add a recipient).</summary>
    static string? Email(string value) =>
        value.Length <= 254 && System.Net.Mail.MailAddress.TryCreate(value, out var address) && address.Address == value
            && address.DisplayName.Length == 0 && value.Count(c => c == '@') == 1
            ? null
            : "Use one email address, e.g. you@gmail.com.";

    static string? Host(string value) =>
        value.Length <= 253 && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-') ? null : "Use a server name, e.g. smtp.gmail.com.";

    static string? SignUpMode(string value) => value is SignUpsOpen or SignUpsInviteOnly ? null : "Use open or invite_only.";

    static string? Bool(string value) => value is "true" or "false" ? null : "Use true or false.";

    static string? TimeZone(string value) =>
        value.Length <= 64 && TimeZoneInfo.TryFindSystemTimeZoneById(value, out _) ? null : "Use a time zone name, e.g. Europe/Madrid.";

    static string? Time(string value) => System.Text.RegularExpressions.Regex.IsMatch(value, @"^([01]\d|2[0-3]):[0-5]\d$")
        ? null
        : "Use HH:MM, e.g. 22:30.";

    static string? WholeNumber(string value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out _)
            ? null
            : "Use a whole number, e.g. 30.";

    static string? Port(string value) =>
        int.TryParse(value, out var port) && port is >= 1 and <= 65535 ? null : "Use a port from 1 to 65535.";
}
