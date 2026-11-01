using System.Security.Cryptography;

namespace StrategyTradingLab.Api.Auth;

/// <summary>Random tokens for session cookies and email links. Only their SHA-256 is stored, so a database leak gives none.</summary>
public static class SecretTokens
{
    /// <summary>32 random bytes, URL-safe base64, and the hash to store.</summary>
    public static (string Token, byte[] Hash) New()
    {
        var token = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        return (token, Hash(token));
    }

    /// <summary>A random token that is kept as it is, for the unsubscribe links in emails, which must be rebuilt for every email.</summary>
    public static string NewPlain() => Base64UrlEncode(RandomNumberGenerator.GetBytes(24));

    public static byte[] Hash(string token) => SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));

    static string Base64UrlEncode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
