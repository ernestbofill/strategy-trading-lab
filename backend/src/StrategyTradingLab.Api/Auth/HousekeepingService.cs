using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Ai;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Auth;

/// <summary>Summary of rows cleaned up and unconfirmed accounts warned/deleted by a housekeeping run.</summary>
public record HousekeepingSummary(int ExpiredSessions, int ExpiredTokens, int WarnedAccounts, int DeletedAccounts);

/// <summary>
/// Daily clean-up (spec: Limits and housekeeping): deletes expired sessions and email tokens,
/// warns unconfirmed accounts at 23 days with a fresh verification link, and deletes accounts
/// never confirmed after 30 days (cleaning up their shared AI rounds first).
/// </summary>
public class HousekeepingService(AppDbContext db, AccountEmails emails, AiRounds aiRounds, TimeProvider time, ILogger<HousekeepingService> logger)
{
    public static readonly TimeSpan UnconfirmedWarningAge = TimeSpan.FromDays(23);
    public static readonly TimeSpan UnconfirmedDeletionAge = TimeSpan.FromDays(30);

    public async Task<HousekeepingSummary> RunAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var expiredSessions = await db.Sessions.Where(s => s.ExpiresAt <= now).ExecuteDeleteAsync(cancellationToken);
        var expiredTokens = await db.EmailTokens.Where(t => t.ExpiresAt <= now).ExecuteDeleteAsync(cancellationToken);

        var cutoff23 = now - UnconfirmedWarningAge;
        var cutoff30 = now - UnconfirmedDeletionAge;

        // Warn accounts unconfirmed for 23 days once, giving them 7 days and a fresh link.
        var toWarn = await db.Users
            .Where(u => u.EmailVerifiedAt == null && u.CreatedAt <= cutoff23 && u.CreatedAt > cutoff30 && u.UnconfirmedWarnedAt == null)
            .ToListAsync(cancellationToken);

        var warnedCount = 0;
        foreach (var user in toWarn)
        {
            var (token, hash) = SecretTokens.New();
            db.EmailTokens.Add(new EmailToken
            {
                UserId = user.Id,
                Purpose = EmailTokenPurpose.VerifyEmail,
                TokenHash = hash,
                ExpiresAt = now.AddHours(24),
            });
            user.UnconfirmedWarnedAt = now;
            await db.SaveChangesAsync(cancellationToken);
            emails.UnconfirmedWarning(user.Email, token, user.Locale);
            warnedCount++;
        }

        // Delete accounts never confirmed after 30 days, removing any shared AI rounds first.
        var toDelete = await db.Users
            .Where(u => u.EmailVerifiedAt == null && u.CreatedAt <= cutoff30)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        var deletedAccounts = 0;
        foreach (var userId in toDelete)
        {
            await aiRounds.ForgetSharedAsync(userId, cancellationToken);
            deletedAccounts += await db.Users.Where(u => u.Id == userId).ExecuteDeleteAsync(cancellationToken);
        }

        logger.LogInformation("Housekeeping completed: {Sessions} expired sessions, {Tokens} expired tokens, {Warned} accounts warned, {Deleted} accounts deleted.",
            expiredSessions, expiredTokens, warnedCount, deletedAccounts);

        return new HousekeepingSummary(expiredSessions, expiredTokens, warnedCount, deletedAccounts);
    }
}
