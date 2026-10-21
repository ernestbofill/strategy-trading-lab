namespace StrategyTradingLab.Api.Admin;

/// <summary>
/// A setting the admin page can set. Only these: the database URL and anything the API needs to start stay in
/// the server's environment (api.env, .env.local). The page's value wins over the server's, which wins over the default.
/// </summary>
/// <param name="BoundTo">For a secret: the settings it belongs with. When any of them changes, by any means, the secret
/// is cleared, so it is never sent somewhere new (a mail server swapped in by whoever knows the page's password).</param>
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
        new("SMTP_USER", "Email", "The email account that sends the emails, e.g. you@gmail.com.",
            Validate: Email),
        new("SMTP_PASSWORD", "Email", "Its password. For Gmail, an app password.", Secret: true,
            BoundTo: ["SMTP_USER", "SMTP_HOST", "SMTP_PORT"]),
        new("SMTP_HOST", "Email", "The mail server. Empty with a @gmail.com user: smtp.gmail.com."),
        new("SMTP_PORT", "Email", "The mail server's port (STARTTLS).", Default: "587", Validate: Port),
        new("SMTP_FROM", "Email", "The sender address. Empty: SMTP_USER.", Validate: Email),
    ];

    public static SettingDefinition? Find(string key) => All.FirstOrDefault(setting => setting.Key == key);

    static string? Email(string value) =>
        value.Length <= 254 && value.IndexOf('@') is > 0 and var at && at < value.Length - 1 && !value.Any(char.IsWhiteSpace)
            ? null
            : "Use an email address.";

    static string? Port(string value) =>
        int.TryParse(value, out var port) && port is >= 1 and <= 65535 ? null : "Use a port from 1 to 65535.";
}
