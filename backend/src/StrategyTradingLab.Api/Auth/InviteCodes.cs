using System.Security.Cryptography;

namespace StrategyTradingLab.Api.Auth;

/// <summary>
/// Invite codes: "ABCD-EFGH", 8 characters from 31 that can't be mistaken for each other (no 0/O, 1/I/L), about 39 bits.
/// Short enough to type from a message; guessing one is kept slow by the per-client limits on sign-up and the invite check.
/// </summary>
public static class InviteCodes
{
    const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    const int Group = 4;
    /// <summary>Two groups and the dash, as stored.</summary>
    public const int Length = 2 * Group + 1;

    public static string New()
    {
        var chars = new char[2 * Group];
        for (var i = 0; i < chars.Length; i++) chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return Format(chars);
    }

    /// <summary>A typed code as stored: case and spaces or dashes don't matter. Null when it can't be a code.</summary>
    public static string? Normalize(string? typed)
    {
        if (typed is not { Length: <= 40 }) return null;
        var chars = typed.Where(char.IsAsciiLetterOrDigit).Select(char.ToUpperInvariant).ToArray();
        return chars.Length == 2 * Group && chars.All(Alphabet.Contains) ? Format(chars) : null;
    }

    static string Format(char[] chars) => $"{new string(chars, 0, Group)}-{new string(chars, Group, Group)}";
}
