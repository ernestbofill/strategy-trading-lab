using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using StrategyTradingLab.Api.Notifications;

namespace StrategyTradingLab.Api.Tests;

public class WebPushTests
{
    static byte[] B64(string value) => WebEncoders.Base64UrlDecode(value);

    [Fact]
    public void Encryption_matches_RFC_8291s_example()
    {
        // RFC 8291, Appendix A: the server's key and salt are fixed so the output can be compared byte for byte.
        var serverPublic = B64("BP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27mlmlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A8");
        using var serverKey = ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            D = B64("yfWPiYE-n46HLnH0KqZOF1fJJU3MYrct3AELtAQ-oRw"),
            Q = WebPushEncryption.Point(serverPublic),
        });

        var body = WebPushEncryption.Encrypt(Encoding.UTF8.GetBytes("When I grow up, I want to be a watermelon"),
            B64("BCVxsr7N_eNgVRqvHtD0zTZsEc6-VV-JvLexhqUzORcxaOzi6-AYWXvTBHm4bjyPjs7Vd8pZGH6SRpkNtoIAiw4"),
            B64("BTBZMqHH6r4Tts7J_aSIgg"), serverKey, B64("DGv6ra1nlYgDCS1FRnbzlw"));

        Assert.Equal("DGv6ra1nlYgDCS1FRnbzlwAAEABBBP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27mlmlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A_yl95bQpu6cVPTpK4Mqgkf1CXztLVBSt2Ks3oZwbuwXPXLWyouBWLVWGNWQexSgSxsj_Qulcy4a-fN",
            WebEncoders.Base64UrlEncode(body));
    }

    [Fact]
    public void Each_message_gets_a_new_key_and_salt()
    {
        var keys = VapidKeys.Create(); // Any P-256 point stands in for a browser's.
        var plaintext = Encoding.UTF8.GetBytes("{}");
        var first = WebPushEncryption.Encrypt(plaintext, B64(keys.PublicKey), new byte[16]);
        var second = WebPushEncryption.Encrypt(plaintext, B64(keys.PublicKey), new byte[16]);
        Assert.NotEqual(first[..16], second[..16]);
        Assert.NotEqual(first[21..86], second[21..86]);
    }

    [Fact]
    public void The_VAPID_token_is_an_ES256_JWT_the_public_key_verifies()
    {
        var keys = VapidKeys.Create();
        var now = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
        var parts = Vapid.Token("https://fcm.googleapis.com", "https://stl.example", keys, now).Split('.');

        Assert.Equal(3, parts.Length);
        using var header = JsonDocument.Parse(B64(parts[0]));
        Assert.Equal("ES256", header.RootElement.GetProperty("alg").GetString());
        using var claims = JsonDocument.Parse(B64(parts[1]));
        Assert.Equal("https://fcm.googleapis.com", claims.RootElement.GetProperty("aud").GetString());
        Assert.Equal("https://stl.example", claims.RootElement.GetProperty("sub").GetString());
        Assert.Equal(now.AddHours(12).ToUnixTimeSeconds(), claims.RootElement.GetProperty("exp").GetInt64());

        using var verifier = ECDsa.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256, Q = WebPushEncryption.Point(B64(keys.PublicKey)) });
        Assert.True(verifier.VerifyData(Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]), B64(parts[2]), HashAlgorithmName.SHA256));
    }

    [Theory]
    [InlineData("https://fcm.googleapis.com/fcm/send/abc", true)]
    [InlineData("https://updates.push.services.mozilla.com/wpush/v2/abc", true)]
    [InlineData("https://web.push.apple.com/QGx", true)]
    [InlineData("https://wns2-db5p.notify.windows.com/w/?token=abc", true)]
    [InlineData("http://fcm.googleapis.com/fcm/send/abc", false)]
    [InlineData("https://fcm.googleapis.com:8443/fcm/send/abc", false)]
    [InlineData("https://evilpush.apple.com/x", false)]
    [InlineData("https://push.apple.com.evil.example/x", false)]
    [InlineData("https://user@fcm.googleapis.com/x", false)]
    [InlineData("https://localhost/x", false)]
    [InlineData("not a url", false)]
    public void Only_the_browsers_push_services_are_posted_to(string endpoint, bool allowed) =>
        Assert.Equal(allowed, WebPushEndpoints.IsPushService(endpoint, out _));
}
