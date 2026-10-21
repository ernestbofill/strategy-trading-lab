using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Auth;

public record SignUpRequest(string? Email, string? Password, string? Locale, string? TimeZone, bool OverviewEmail, bool DisclaimerAccepted);
public record LogInRequest(string? Email, string? Password);
public record TokenRequest(string? Token);
public record ResetPasswordRequest(string? Token, string? Password);
public record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);
public record EmailRequest(string? Email);

/// <summary>The logged-in user, for the app's start-up check.</summary>
public record AccountDto(string Email, bool EmailVerified, string Locale, string TimeZone, bool OverviewEmail);

/// <summary>A message for the user, the same whatever the email (sign-up, forgot password).</summary>
public record AccountMessage(string Message);

/// <summary>Sign-up, log-in, sessions and password links (backend/AUTH.md, spec: Sign-up, Log-in, Forgot and reset password).</summary>
public static class AccountEndpoints
{
    public const string SignUpLimit = "account-sign-up", LogInLimit = "account-log-in", EmailLimit = "account-email",
        PasswordLimit = "account-password";
    static readonly TimeSpan VerifyFor = TimeSpan.FromHours(24), ResetFor = TimeSpan.FromHours(1);
    const string WrongLogIn = "That email and password don't match.";
    const string ExpiredLink = "This link has expired. Ask for a new one.";

    /// <summary>Checked when the email has no account, so a wrong email takes as long as a wrong password.</summary>
    static readonly string DummyHash = new PasswordHasher<User>().HashPassword(null!, "not a real password");

    public static void MapAccount(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/account").WithTags("Account");

        group.MapPost("/sign-up", async Task<Results<Accepted<AccountMessage>, ValidationProblem>> (SignUpRequest request,
            AppDbContext db, IPasswordHasher<User> hasher, Sessions sessions, AccountEmails emails, TimeProvider time,
            CancellationToken cancellationToken) =>
        {
            var errors = new Dictionary<string, string[]>();
            var email = AccountRules.NormalizeEmail(request.Email);
            if (email is null) errors["email"] = ["Use an email address, e.g. you@gmail.com."];
            if (AccountRules.PasswordError(request.Password, email) is { } passwordError) errors["password"] = [passwordError];
            var locale = request.Locale ?? "en";
            if (!AccountRules.Locales.Contains(locale)) errors["locale"] = ["Pick English or Catalan."];
            if (!AccountRules.IsTimeZone(request.TimeZone)) errors["timeZone"] = ["Pick your time zone."];
            if (!request.DisclaimerAccepted) errors["disclaimerAccepted"] = ["Tick the box to go on."];
            if (errors.Count > 0) return TypedResults.ValidationProblem(errors);

            // The same answer whether or not the email has an account (spec: Sign-up). The emails go out after it.
            var answer = TypedResults.Accepted((string?)null, new AccountMessage("Check your email."));
            // Emails compare without case (citext); the warning goes to the address on file.
            if (await db.Users.Where(u => u.Email == email).Select(u => u.Email).SingleOrDefaultAsync(cancellationToken) is { } registered)
            {
                emails.AlreadyRegistered(registered);
                return answer;
            }

            var now = time.GetUtcNow();
            var user = new User
            {
                Email = email!, TimeZone = request.TimeZone!, Locale = locale, OverviewEmail = request.OverviewEmail,
                DisclaimerAcceptedAt = now,
            };
            user.PasswordHash = hasher.HashPassword(user, request.Password!);
            db.Users.Add(user);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                emails.AlreadyRegistered(email!); // Signed up twice at the same moment.
                return answer;
            }
            emails.Verify(email!, await NewToken(db, user, EmailTokenPurpose.VerifyEmail, now + VerifyFor, cancellationToken));
            await sessions.StartAsync(user, cancellationToken);
            return answer;
        })
        .RequireRateLimiting(SignUpLimit)
        .WithSummary("Creates an account and logs it in. Answers the same when the email already has one (that account gets an email).");

        group.MapPost("/log-in", async Task<Results<NoContent, ProblemHttpResult>> (LogInRequest request, AppDbContext db,
            IPasswordHasher<User> hasher, Sessions sessions, LogInThrottle throttle, CancellationToken cancellationToken) =>
        {
            var email = AccountRules.NormalizeEmail(request.Email) ?? (request.Email ?? "").Trim();
            if (throttle.IsLocked(email))
                return TypedResults.Problem("Too many tries. Wait a few minutes, or reset your password.", statusCode: StatusCodes.Status429TooManyRequests);

            var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email, cancellationToken);
            var result = user?.PasswordHash is { } hash
                ? hasher.VerifyHashedPassword(user, hash, request.Password ?? "")
                : Failed(hasher, request.Password);
            if (user is null || result == PasswordVerificationResult.Failed)
            {
                throttle.Failed(email);
                return TypedResults.Problem(WrongLogIn, statusCode: StatusCodes.Status401Unauthorized);
            }
            if (result == PasswordVerificationResult.SuccessRehashNeeded) user.PasswordHash = hasher.HashPassword(user, request.Password!);
            throttle.Succeeded(email);
            await sessions.StartAsync(user, cancellationToken);
            return TypedResults.NoContent();
        })
        .RequireRateLimiting(LogInLimit)
        .WithSummary("Logs in with email and password. One message for a wrong email or a wrong password.");

        group.MapPost("/log-out", async (Sessions sessions, CancellationToken cancellationToken) =>
        {
            await sessions.EndAsync(cancellationToken);
            return TypedResults.NoContent();
        })
        .WithSummary("Ends this device's session.");

        group.MapGet("/me", async (Sessions sessions, CancellationToken cancellationToken) =>
        {
            var user = (await sessions.CurrentAsync(cancellationToken))!;
            return TypedResults.Ok(new AccountDto(user.Email, user.EmailVerifiedAt is not null, user.Locale, user.TimeZone, user.OverviewEmail));
        })
        .RequireUser()
        .WithSummary("The logged-in user, or 401: the app's start-up check.");

        group.MapPost("/verify-email", async Task<Results<NoContent, ProblemHttpResult>> (TokenRequest request, AppDbContext db,
            Sessions sessions, TimeProvider time, CancellationToken cancellationToken) =>
        {
            if (await UseToken(db, request.Token, EmailTokenPurpose.VerifyEmail, time, cancellationToken) is not { } user)
                return TypedResults.Problem(ExpiredLink, statusCode: StatusCodes.Status400BadRequest);
            user.EmailVerifiedAt ??= time.GetUtcNow();
            await db.SaveChangesAsync(cancellationToken);
            await sessions.StartAsync(user, cancellationToken);
            return TypedResults.NoContent();
        })
        .RequireRateLimiting(PasswordLimit)
        .WithSummary("Confirms the email from the link in the email, and logs that user in.");

        group.MapPost("/resend-verification", async (AppDbContext db, Sessions sessions, AccountEmails emails, TimeProvider time,
            CancellationToken cancellationToken) =>
        {
            var user = (await sessions.CurrentAsync(cancellationToken))!;
            if (user.EmailVerifiedAt is null)
                emails.Verify(user.Email, await NewToken(db, user, EmailTokenPurpose.VerifyEmail, time.GetUtcNow() + VerifyFor, cancellationToken));
            return TypedResults.NoContent();
        })
        .RequireUser()
        .RequireRateLimiting(EmailLimit)
        .WithSummary("Sends the verification link again.");

        group.MapPost("/forgot-password", async (EmailRequest request, AppDbContext db, AccountEmails emails, TimeProvider time,
            CancellationToken cancellationToken) =>
        {
            var email = AccountRules.NormalizeEmail(request.Email);
            if (email is not null && await db.Users.SingleOrDefaultAsync(u => u.Email == email, cancellationToken) is { } user)
                emails.Reset(user.Email, await NewToken(db, user, EmailTokenPurpose.ResetPassword, time.GetUtcNow() + ResetFor, cancellationToken));
            return TypedResults.Accepted((string?)null, new AccountMessage("If that email has an account, we sent a link. It works for 1 hour."));
        })
        .RequireRateLimiting(EmailLimit)
        .WithSummary("Emails a reset link if the email has an account. Always the same answer.");

        group.MapPost("/reset-password", async Task<Results<NoContent, ProblemHttpResult, ValidationProblem>> (
            ResetPasswordRequest request, AppDbContext db, IPasswordHasher<User> hasher, Sessions sessions, TimeProvider time,
            CancellationToken cancellationToken) =>
        {
            // The password is checked before the link is used, so a refused password doesn't use it up.
            var token = await FindToken(db, request.Token, EmailTokenPurpose.ResetPassword, time, cancellationToken);
            if (token is null) return TypedResults.Problem(ExpiredLink, statusCode: StatusCodes.Status400BadRequest);
            var owner = await db.Users.AsNoTracking().SingleAsync(u => u.Id == token.UserId, cancellationToken);
            if (AccountRules.PasswordError(request.Password, owner.Email) is { } error)
                return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["password"] = [error] });
            if (await UseToken(db, request.Token, EmailTokenPurpose.ResetPassword, time, cancellationToken) is not { } user)
                return TypedResults.Problem(ExpiredLink, statusCode: StatusCodes.Status400BadRequest);

            var now = time.GetUtcNow();
            user.PasswordHash = hasher.HashPassword(user, request.Password!);
            user.EmailVerifiedAt ??= now; // The link reached the inbox.
            await db.EmailTokens.Where(t => t.UserId == user.Id && t.Purpose == EmailTokenPurpose.ResetPassword && t.UsedAt == null)
                .ExecuteUpdateAsync(t => t.SetProperty(x => x.UsedAt, now), cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await sessions.EndAllAsync(user.Id, keepThisOne: false, cancellationToken);
            await sessions.StartAsync(user, cancellationToken);
            return TypedResults.NoContent();
        })
        .RequireRateLimiting(PasswordLimit)
        .WithSummary("Sets a new password from the link in the email: ends every session, then logs this device in.");

        group.MapPost("/change-password", async Task<Results<NoContent, ValidationProblem>> (ChangePasswordRequest request,
            AppDbContext db, IPasswordHasher<User> hasher, Sessions sessions, CancellationToken cancellationToken) =>
        {
            var user = (await sessions.CurrentAsync(cancellationToken))!;
            if (user.PasswordHash is null
                || hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword ?? "") == PasswordVerificationResult.Failed)
                return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["currentPassword"] = ["That isn't your current password."] });
            if (AccountRules.PasswordError(request.NewPassword, user.Email) is { } error)
                return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["newPassword"] = [error] });

            user.PasswordHash = hasher.HashPassword(user, request.NewPassword!);
            await db.SaveChangesAsync(cancellationToken);
            await sessions.EndAllAsync(user.Id, keepThisOne: true, cancellationToken);
            return TypedResults.NoContent();
        })
        .RequireUser()
        .RequireRateLimiting(PasswordLimit)
        .WithSummary("Changes the password; ends the user's other sessions.");
    }

    static PasswordVerificationResult Failed(IPasswordHasher<User> hasher, string? password)
    {
        hasher.VerifyHashedPassword(null!, DummyHash, password ?? "");
        return PasswordVerificationResult.Failed;
    }

    static async Task<string> NewToken(AppDbContext db, User user, EmailTokenPurpose purpose, DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        var (token, hash) = SecretTokens.New();
        db.EmailTokens.Add(new EmailToken { UserId = user.Id, Purpose = purpose, TokenHash = hash, ExpiresAt = expiresAt });
        await db.SaveChangesAsync(cancellationToken);
        return token;
    }

    /// <summary>The unused, unexpired token of that purpose, looked up by its hash.</summary>
    static Task<EmailToken?> FindToken(AppDbContext db, string? token, EmailTokenPurpose purpose, TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (token is not { Length: > 0 and < 100 }) return Task.FromResult<EmailToken?>(null);
        var hash = SecretTokens.Hash(token);
        var now = time.GetUtcNow();
        return db.EmailTokens.SingleOrDefaultAsync(t => t.TokenHash == hash && t.Purpose == purpose && t.UsedAt == null && t.ExpiresAt > now,
            cancellationToken);
    }

    /// <summary>
    /// Uses the token and returns its user, or null when it isn't valid. The claim is one UPDATE … WHERE used_at IS NULL,
    /// so two requests with the same link can't both use it.
    /// </summary>
    static async Task<User?> UseToken(AppDbContext db, string? token, EmailTokenPurpose purpose, TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await FindToken(db, token, purpose, time, cancellationToken) is not { } found) return null;
        var now = time.GetUtcNow();
        var claimed = await db.EmailTokens.Where(t => t.Id == found.Id && t.UsedAt == null)
            .ExecuteUpdateAsync(t => t.SetProperty(x => x.UsedAt, now), cancellationToken);
        return claimed == 1 ? await db.Users.SingleAsync(u => u.Id == found.UserId, cancellationToken) : null;
    }
}
