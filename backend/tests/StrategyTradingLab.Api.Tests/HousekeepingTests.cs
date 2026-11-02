using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StrategyTradingLab.Api.Auth;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Tests;

/// <summary>Tests for daily clean-up: sessions, tokens, and unconfirmed account warning/deletion.</summary>
public class HousekeepingTests(AccountsApi api) : IClassFixture<AccountsApi>
{
    [NeonFact]
    public async Task Expired_sessions_and_tokens_are_deleted_while_active_ones_are_kept()
    {
        var (client, email) = await api.SignedUpAsync();
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        var now = DateTimeOffset.UtcNow;

        var activeSessionToken = SecretTokens.NewPlain();
        var expiredSessionToken = SecretTokens.NewPlain();
        db.Sessions.AddRange(
            new UserSession { UserId = user.Id, TokenHash = SecretTokens.Hash(activeSessionToken), ExpiresAt = now.AddDays(1) },
            new UserSession { UserId = user.Id, TokenHash = SecretTokens.Hash(expiredSessionToken), ExpiresAt = now.AddDays(-1) }
        );

        var activeEmailToken = SecretTokens.NewPlain();
        var expiredEmailToken = SecretTokens.NewPlain();
        db.EmailTokens.AddRange(
            new EmailToken { UserId = user.Id, Purpose = EmailTokenPurpose.VerifyEmail, TokenHash = SecretTokens.Hash(activeEmailToken), ExpiresAt = now.AddHours(1) },
            new EmailToken { UserId = user.Id, Purpose = EmailTokenPurpose.VerifyEmail, TokenHash = SecretTokens.Hash(expiredEmailToken), ExpiresAt = now.AddHours(-1) }
        );
        await db.SaveChangesAsync();

        var housekeeping = scope.ServiceProvider.GetRequiredService<HousekeepingService>();
        var summary = await housekeeping.RunAsync(CancellationToken.None);

        Assert.True(summary.ExpiredSessions >= 1);
        Assert.True(summary.ExpiredTokens >= 1);

        // Active session and token exist; expired ones do not
        Assert.True(await db.Sessions.AnyAsync(s => s.TokenHash == SecretTokens.Hash(activeSessionToken)));
        Assert.False(await db.Sessions.AnyAsync(s => s.TokenHash == SecretTokens.Hash(expiredSessionToken)));
        Assert.True(await db.EmailTokens.AnyAsync(t => t.TokenHash == SecretTokens.Hash(activeEmailToken)));
        Assert.False(await db.EmailTokens.AnyAsync(t => t.TokenHash == SecretTokens.Hash(expiredEmailToken)));
    }

    [NeonFact]
    public async Task Unconfirmed_accounts_at_23_days_are_warned_once_with_working_token()
    {
        var (client, email) = await api.SignedUpAsync();
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        var now = DateTimeOffset.UtcNow;

        // Set user to 24 days old, unconfirmed
        user.CreatedAt = now.AddDays(-24);
        user.EmailVerifiedAt = null;
        user.UnconfirmedWarnedAt = null;
        await db.SaveChangesAsync();

        var housekeeping = scope.ServiceProvider.GetRequiredService<HousekeepingService>();
        var summary = await housekeeping.RunAsync(CancellationToken.None);

        Assert.True(summary.WarnedAccounts >= 1);

        // Warning email sent with token
        var warningEmail = await api.Emails.WaitForAsync(email, "Confirm your email to keep");
        Assert.NotNull(warningEmail);
        var token = CapturedEmails.Token(warningEmail!);
        Assert.NotEmpty(token);

        // Token works to verify the account
        using var visitor = api.CreateApiClient();
        var verifyResponse = await visitor.PostAsJsonAsync("/account/verify-email", new { token });
        Assert.Equal(HttpStatusCode.NoContent, verifyResponse.StatusCode);

        // User is now verified
        await db.Entry(user).ReloadAsync();
        Assert.NotNull(user.EmailVerifiedAt);
        Assert.NotNull(user.UnconfirmedWarnedAt);

        // Running housekeeping again does not re-warn
        var secondRun = await housekeeping.RunAsync(CancellationToken.None);
        Assert.Equal(0, secondRun.WarnedAccounts);
    }

    [NeonFact]
    public async Task Unconfirmed_accounts_at_30_days_are_deleted_with_their_data()
    {
        var (client, email) = await api.SignedUpAsync();
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        var now = DateTimeOffset.UtcNow;

        // Add a strategy for this unconfirmed user
        var strategy = new Strategy
        {
            UserId = user.Id,
            TemplateKey = "dip",
            DefaultsVersion = 1,
            Name = $"Old Strategy {Guid.NewGuid()}",
            Mode = StrategyMode.Simulation,
            Status = StrategyStatus.Running,
            StartedOn = DateOnly.FromDateTime(DateTime.UtcNow),
            Currency = "EUR",
            Budget = new Budget(1000, 100, 5),
            Params = System.Text.Json.JsonDocument.Parse("{}"),
        };
        db.Strategies.Add(strategy);

        // Set user to 31 days old, unconfirmed
        user.CreatedAt = now.AddDays(-31);
        user.EmailVerifiedAt = null;
        await db.SaveChangesAsync();

        var housekeeping = scope.ServiceProvider.GetRequiredService<HousekeepingService>();
        var summary = await housekeeping.RunAsync(CancellationToken.None);

        Assert.True(summary.DeletedAccounts >= 1);

        // User and strategy are deleted
        Assert.False(await db.Users.AnyAsync(u => u.Id == user.Id));
        Assert.False(await db.Strategies.AnyAsync(s => s.Id == strategy.Id));
    }

    [NeonFact]
    public async Task Confirmed_accounts_and_young_unconfirmed_accounts_are_kept()
    {
        var (_, confirmedEmail) = await api.VerifiedAsync();
        var (_, youngEmail) = await api.SignedUpAsync();

        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var confirmedUser = await db.Users.SingleAsync(u => u.Email == confirmedEmail);
        var youngUser = await db.Users.SingleAsync(u => u.Email == youngEmail);

        // Confirmed user is 40 days old; young user is 10 days old
        confirmedUser.CreatedAt = DateTimeOffset.UtcNow.AddDays(-40);
        youngUser.CreatedAt = DateTimeOffset.UtcNow.AddDays(-10);
        await db.SaveChangesAsync();

        var housekeeping = scope.ServiceProvider.GetRequiredService<HousekeepingService>();
        await housekeeping.RunAsync(CancellationToken.None);

        Assert.True(await db.Users.AnyAsync(u => u.Id == confirmedUser.Id));
        Assert.True(await db.Users.AnyAsync(u => u.Id == youngUser.Id));
    }
}
