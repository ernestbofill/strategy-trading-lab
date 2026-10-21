namespace StrategyTradingLab.Api.Admin;

/// <summary>
/// A setting the admin page can set. Only these: anything the API needs to start stays in the server's environment
/// (api.env, .env.local), and the database URLs, which can't live in the database, are in <see cref="DatabaseSwitch"/>. The page's value wins over the server's, which wins over the default.
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
    ];

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

    static string? Port(string value) =>
        int.TryParse(value, out var port) && port is >= 1 and <= 65535 ? null : "Use a port from 1 to 65535.";
}
