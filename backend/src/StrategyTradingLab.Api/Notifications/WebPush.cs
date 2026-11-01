using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Notifications;

/// <summary>What a push service answered: delivered, the subscription is gone for good (404, 410), or it failed this time.</summary>
public enum PushOutcome { Sent, Gone, Failed }

/// <summary>Sends web push messages (spec: Notifications). A fake replaces it in tests.</summary>
public interface IPushSender
{
    /// <summary>The VAPID public key the browser subscribes with (base64url), or null when push isn't set up on this server.</summary>
    string? PublicKey { get; }

    Task<PushOutcome> SendAsync(PushSubscription subscription, string payload, CancellationToken cancellationToken);
}

/// <summary>
/// Web push with VAPID (RFC 8292) and the aes128gcm content encoding (RFC 8291, RFC 8188), on .NET's own crypto: no
/// package. The keys come from the server's environment, <c>VAPID_PUBLIC_KEY</c> and <c>VAPID_PRIVATE_KEY</c> (base64url,
/// the raw P-256 point and scalar); without them push is off and the app only emails.
/// </summary>
public class WebPushSender(HttpClient http, IConfiguration configuration, ILogger<WebPushSender> logger) : IPushSender
{
    /// <summary>How long a push service keeps a message for a device that is off: a day, as a signal's alert is about today.</summary>
    const int TimeToLiveSeconds = 24 * 60 * 60;

    public string? PublicKey => Keys()?.PublicKey;

    public async Task<PushOutcome> SendAsync(PushSubscription subscription, string payload, CancellationToken cancellationToken)
    {
        if (Keys() is not { } keys) return PushOutcome.Failed;
        if (!WebPushEndpoints.IsPushService(subscription.Endpoint, out var endpoint)) return PushOutcome.Gone;

        var body = WebPushEncryption.Encrypt(Encoding.UTF8.GetBytes(payload),
            WebEncoders.Base64UrlDecode(subscription.P256dh), WebEncoders.Base64UrlDecode(subscription.Auth));
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = new ByteArrayContent(body) };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        request.Content.Headers.ContentEncoding.Add("aes128gcm");
        request.Headers.Add("TTL", TimeToLiveSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        request.Headers.Add("Urgency", "normal");
        var audience = endpoint.GetLeftPart(UriPartial.Authority);
        request.Headers.Authorization = new AuthenticationHeaderValue("vapid",
            $"t={Vapid.Token(audience, Subject, keys, DateTimeOffset.UtcNow)}, k={keys.PublicKey}");

        using var response = await http.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode) return PushOutcome.Sent;
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone) return PushOutcome.Gone;
        // Not the endpoint: it identifies the device.
        logger.LogWarning("A push service refused a message: {Status}", (int)response.StatusCode);
        return PushOutcome.Failed;
    }

    /// <summary>Who to contact about these messages, which the push services ask for: the app's address.</summary>
    string Subject => (configuration["PublicUrl"] ?? "https://localhost").TrimEnd('/');

    VapidKeys? Keys() =>
        configuration["VAPID_PUBLIC_KEY"] is { Length: > 0 } publicKey && configuration["VAPID_PRIVATE_KEY"] is { Length: > 0 } privateKey
            ? new VapidKeys(publicKey, privateKey)
            : null;
}

/// <summary>A VAPID key pair, base64url: the uncompressed P-256 public point (65 bytes) and the private scalar (32 bytes).</summary>
public record VapidKeys(string PublicKey, string PrivateKey)
{
    /// <summary>A new pair, for the server's environment (<c>dotnet run -- vapid-keys</c> prints one).</summary>
    public static VapidKeys Create()
    {
        using var key = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var parameters = key.ExportParameters(true);
        return new(WebEncoders.Base64UrlEncode(WebPushEncryption.Point(parameters.Q)), WebEncoders.Base64UrlEncode(parameters.D!));
    }
}

/// <summary>The VAPID token (RFC 8292): a JWT signed with ES256 that says which app sends to which push service.</summary>
public static class Vapid
{
    /// <summary>12 hours, half the 24 hours push services accept at most.</summary>
    static readonly TimeSpan Lifetime = TimeSpan.FromHours(12);

    public static string Token(string audience, string subject, VapidKeys keys, DateTimeOffset now)
    {
        static string Part(object value) => WebEncoders.Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(value));
        var unsigned = Part(new { typ = "JWT", alg = "ES256" }) + "." + Part(new { aud = audience, exp = (now + Lifetime).ToUnixTimeSeconds(), sub = subject });

        var publicKey = WebEncoders.Base64UrlDecode(keys.PublicKey);
        using var signer = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            D = WebEncoders.Base64UrlDecode(keys.PrivateKey),
            Q = WebPushEncryption.Point(publicKey),
        });
        // .NET signs as r || s (IEEE P1363), the JWS format.
        return unsigned + "." + WebEncoders.Base64UrlEncode(signer.SignData(Encoding.ASCII.GetBytes(unsigned), HashAlgorithmName.SHA256));
    }
}

/// <summary>Message encryption for web push (RFC 8291): one aes128gcm record, readable only by the subscribed browser.</summary>
public static class WebPushEncryption
{
    const int RecordSize = 4096;

    /// <summary>Encrypts with a new server key and salt, as every message must.</summary>
    public static byte[] Encrypt(byte[] plaintext, byte[] userAgentPublicKey, byte[] authSecret)
    {
        using var serverKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        return Encrypt(plaintext, userAgentPublicKey, authSecret, serverKey, RandomNumberGenerator.GetBytes(16));
    }

    /// <summary>With a given server key and salt: for RFC 8291's example in the tests.</summary>
    public static byte[] Encrypt(byte[] plaintext, byte[] userAgentPublicKey, byte[] authSecret, ECDiffieHellman serverKey, byte[] salt)
    {
        // A record holds the text, the delimiter and the tag: messages are small (a title, a line and a link).
        if (plaintext.Length + 1 + 16 > RecordSize) throw new ArgumentException("The message is too long for one record.", nameof(plaintext));
        var serverPublicKey = Point(serverKey.ExportParameters(false).Q);
        using var userAgentKey = ECDiffieHellman.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256, Q = Point(userAgentPublicKey) });
        var sharedSecret = serverKey.DeriveRawSecretAgreement(userAgentKey.PublicKey);

        var keyInfo = Concat(Encoding.ASCII.GetBytes("WebPush: info\0"), userAgentPublicKey, serverPublicKey);
        var inputKey = HKDF.Expand(HashAlgorithmName.SHA256, HKDF.Extract(HashAlgorithmName.SHA256, sharedSecret, authSecret), 32, keyInfo);
        var pseudoRandomKey = HKDF.Extract(HashAlgorithmName.SHA256, inputKey, salt);
        var contentKey = HKDF.Expand(HashAlgorithmName.SHA256, pseudoRandomKey, 16, Encoding.ASCII.GetBytes("Content-Encoding: aes128gcm\0"));
        var nonce = HKDF.Expand(HashAlgorithmName.SHA256, pseudoRandomKey, 12, Encoding.ASCII.GetBytes("Content-Encoding: nonce\0"));

        // The last (and only) record ends with the delimiter 2, and no padding.
        var padded = Concat(plaintext, [2]);
        var ciphertext = new byte[padded.Length];
        var tag = new byte[16];
        using (var aes = new AesGcm(contentKey, tag.Length)) aes.Encrypt(nonce, padded, ciphertext, tag);

        var header = new byte[16 + 4 + 1];
        salt.CopyTo(header, 0);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(16), RecordSize);
        header[20] = (byte)serverPublicKey.Length;
        return Concat(header, serverPublicKey, ciphertext, tag);
    }

    /// <summary>An uncompressed P-256 point: 0x04, then X and Y.</summary>
    public static byte[] Point(ECPoint point) => Concat([4], point.X!, point.Y!);

    public static ECPoint Point(byte[] uncompressed)
    {
        if (uncompressed is not [4, ..] || uncompressed.Length != 65) throw new ArgumentException("Not an uncompressed P-256 point.", nameof(uncompressed));
        return new ECPoint { X = uncompressed[1..33], Y = uncompressed[33..] };
    }

    static byte[] Concat(params byte[][] parts) => [.. parts.SelectMany(part => part)];
}
