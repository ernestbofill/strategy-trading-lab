using Microsoft.Extensions.Caching.Memory;
using StrategyTradingLab.Api.Auth;

namespace StrategyTradingLab.Api.Tests;

/// <summary>The account rules, tokens and the log-in lock. No database.</summary>
public class AccountRulesTests
{
    sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Theory]
    [InlineData(" Me@Example.com ", "Me@Example.com")]
    [InlineData("me@example.com,them@example.com", null)]
    [InlineData("Me <me@example.com>", null)]
    [InlineData("a@b@c", null)]
    [InlineData("not an email", null)]
    public void Emails_are_one_plain_address(string value, string? expected) =>
        Assert.Equal(expected, AccountRules.NormalizeEmail(value));

    [Theory]
    [InlineData("short", "me@example.com", false)]
    [InlineData("ten chars!", "me@example.com", true)]
    [InlineData("ME@EXAMPLE.COM", "me@example.com", false)]
    public void Passwords_need_10_characters_and_not_the_email(string password, string email, bool valid) =>
        Assert.Equal(valid, AccountRules.PasswordError(password, email) is null);

    [Fact]
    public void A_password_over_128_characters_is_refused() =>
        Assert.NotNull(AccountRules.PasswordError(new string('a', 129), null));

    [Fact]
    public void Tokens_are_random_and_only_their_hash_matches()
    {
        var (token, hash) = SecretTokens.New();
        var (other, _) = SecretTokens.New();

        Assert.NotEqual(token, other);
        Assert.Equal(hash, SecretTokens.Hash(token));
        Assert.Matches("^[A-Za-z0-9_-]{43}$", token);
    }

    [Fact]
    public void Five_failures_lock_for_15_minutes_and_old_failures_dont_count()
    {
        var clock = new Clock(new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero));
        var throttle = new LogInThrottle(new MemoryCache(new MemoryCacheOptions()), clock);

        for (var i = 0; i < 4; i++) throttle.Failed("a@b.c");
        Assert.False(throttle.IsLocked("a@b.c"));
        clock.Now += TimeSpan.FromMinutes(16); // The window has passed: counting starts again.
        for (var i = 0; i < 4; i++) throttle.Failed("A@B.C");
        Assert.False(throttle.IsLocked("a@b.c"));
        throttle.Failed("a@b.c");
        Assert.True(throttle.IsLocked("a@b.c"));
        clock.Now += TimeSpan.FromMinutes(15);
        Assert.False(throttle.IsLocked("a@b.c"));
    }
}
