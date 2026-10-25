using System.Net.Mail;

namespace StrategyTradingLab.Api.Auth;

/// <summary>What an account's fields must be (spec: Sign-up). Messages are for the user.</summary>
public static class AccountRules
{
    public const int PasswordMin = 10, PasswordMax = 128;
    public static readonly IReadOnlyList<string> Locales = ["en", "ca"];
    /// <summary>The currencies a user can pick for new strategies (ISO 4217). The first is the default.</summary>
    public static readonly IReadOnlyList<string> Currencies = ["EUR", "USD", "GBP"];

    /// <summary>The address as stored: trimmed, exactly one plain address. Null when it isn't one.</summary>
    public static string? NormalizeEmail(string? value)
    {
        var email = value?.Trim() ?? "";
        return email.Length <= 254 && MailAddress.TryCreate(email, out var address) && address.Address == email
            && address.DisplayName.Length == 0 && email.Count(c => c == '@') == 1
            ? email
            : null;
    }

    public static string? PasswordError(string? password, string? email, string locale = "en")
    {
        if (password is null || password.Length < PasswordMin) return Messages.PasswordTooShort.In(locale, PasswordMin);
        if (password.Length > PasswordMax) return Messages.PasswordTooLong.In(locale, PasswordMax);
        if (email is not null && string.Equals(password.Trim(), email, StringComparison.OrdinalIgnoreCase))
            return Messages.PasswordIsEmail.In(locale);
        return null;
    }

    public static bool IsTimeZone(string? id) => id is { Length: > 0 and <= 64 } && TimeZoneInfo.TryFindSystemTimeZoneById(id, out _);
}
