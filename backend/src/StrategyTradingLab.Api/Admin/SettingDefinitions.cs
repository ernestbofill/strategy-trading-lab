namespace StrategyTradingLab.Api.Admin;

/// <summary>
/// A setting the admin page can set. Only these: the database URL and anything the API needs to start stay in
/// the server's environment (api.env, .env.local). The page's value wins over the server's, which wins over the default.
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
        new("SMTP_HOST", "Email", "The mail server.", Default: "smtp.gmail.com"),
        new("SMTP_PORT", "Email", "The mail server's port (STARTTLS).", Default: "587", Validate: Port),
        new("SMTP_FROM", "Email", "The sender address. Empty: the account above.", Validate: Email),
        new("SMTP_PASSWORD", "Email",
            "The account's password: for Gmail, an app password. It only counts while the account, server and port above stay as they were when it was saved.",
            Secret: true, BoundTo: ["SMTP_USER", "SMTP_HOST", "SMTP_PORT"]),
    ];

    const string GmailAccount = "nest.u333@gmail.com";

    public static SettingDefinition? Find(string key) => All.FirstOrDefault(setting => setting.Key == key);

    static string? Email(string value) =>
        value.Length <= 254 && value.IndexOf('@') is > 0 and var at && at < value.Length - 1 && !value.Any(char.IsWhiteSpace)
            ? null
            : "Use an email address.";

    static string? Port(string value) =>
        int.TryParse(value, out var port) && port is >= 1 and <= 65535 ? null : "Use a port from 1 to 65535.";
}
