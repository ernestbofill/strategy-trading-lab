using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Admin;
using StrategyTradingLab.Api.Invites;

namespace StrategyTradingLab.Api.Auth;

public record SignUpRequest(string? Email, string? Password, string? Locale, string? TimeZone, bool OverviewEmail, bool DisclaimerAccepted,
    string? Currency = null, string? FirstName = null, string? LastName = null, string? InviteCode = null, string? TermsVersion = null);
public record LogInRequest(string? Email, string? Password);
public record TokenRequest(string? Token);
public record ResetPasswordRequest(string? Token, string? Password);
public record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);
public record EmailRequest(string? Email);
public record CurrencyRequest(string? Currency);
public record LocaleRequest(string? Locale);
public record OverviewEmailRequest(bool? Enabled);
public record NameRequest(string? FirstName, string? LastName);

/// <summary>
/// Who an invite is for, to greet them on Sign-up: only the first name and language. Anyone holding the code (a
/// forwarded message) sees this, so not the invitee's email or last name.
/// </summary>
public record SignUpInvite(string Code, string FirstName, string Locale, string? InvitedBy);

/// <summary>What Sign-up needs to know first: whether an invite is needed, and the invite the link carries, if it is usable.</summary>
public record SignUpOptions(bool InviteOnly, SignUpInvite? Invite);

public record ResetRecipient(string Email, string Locale);

/// <summary>The logged-in user, for the app's start-up check.</summary>
public record AccountDto(string Email, string? FirstName, string? LastName, bool EmailVerified, string Locale, string TimeZone, bool OverviewEmail, string Currency, UserRole Role, bool AiSharing = false, string? TermsVersionAccepted = null, DateTimeOffset? TermsAcceptedAt = null);

/// <summary>A message for the user, the same whatever the email (sign-up, forgot password).</summary>
public record AccountMessage(string Message);

/// <summary>Sign-up, log-in, sessions and password links (backend/AUTH.md, spec: Sign-up, Log-in, Forgot and reset password).</summary>
public static class AccountEndpoints
{
    public const string SignUpLimit = "account-sign-up", LogInLimit = "account-log-in", EmailLimit = "account-email",
        PasswordLimit = "account-password", InviteLimit = "account-invite", LogOutLimit = "account-log-out";
    static readonly TimeSpan VerifyFor = TimeSpan.FromHours(24), ResetFor = TimeSpan.FromHours(1);

    /// <summary>Checked when the email has no account, so a wrong email takes as long as a wrong password.</summary>
    static readonly string DummyHash = new PasswordHasher<User>().HashPassword(null!, "not a real password");

    public static void MapAccount(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/account").WithTags("Account");

        group.MapGet("/sign-up-options", async (string? invite, AppDbContext db, SettingsStore settings, CancellationToken cancellationToken) =>
        {
            var found = await InviteEndpoints.FindUsableAsync(db, invite, cancellationToken);
            var invitedBy = found is null ? null
                : await db.Users.Where(u => u.Id == found.CreatedBy).Select(u => u.FirstName).SingleOrDefaultAsync(cancellationToken);
            return TypedResults.Ok(new SignUpOptions(await InviteEndpoints.InviteOnlyAsync(settings, cancellationToken),
                found is null ? null : new SignUpInvite(found.Code, found.FirstName, found.Locale, invitedBy)));
        })
        .RequireRateLimiting(InviteLimit)
        .WithSummary("Whether sign-up needs an invite, and who the given invite is for (null when it can't be used).");

        group.MapPost("/sign-up", async Task<Results<Accepted<AccountMessage>, ValidationProblem>> (SignUpRequest request,
            AppDbContext db, IPasswordHasher<User> hasher, Sessions sessions, AccountEmails emails, SettingsStore settings,
            TimeProvider time, HttpContext context, CancellationToken cancellationToken) =>
        {
            var reqLocale = RequestLocale.From(context.Request);
            var errors = new Dictionary<string, string[]>();
            var (firstName, lastName) = CheckNames(request.FirstName, request.LastName, errors, reqLocale);
            var email = AccountRules.NormalizeEmail(request.Email);
            if (email is null) errors["email"] = [Messages.UseAnEmail.In(reqLocale)];
            if (AccountRules.PasswordError(request.Password, email, reqLocale) is { } passwordError) errors["password"] = [passwordError];
            var locale = request.Locale ?? "en";
            if (!AccountRules.Locales.Contains(locale)) errors["locale"] = [Messages.PickLanguage.In(reqLocale)];
            var currency = request.Currency ?? AccountRules.Currencies[0];
            if (!AccountRules.Currencies.Contains(currency)) errors["currency"] = [Messages.PickCurrency.In(reqLocale)];
            if (!AccountRules.IsTimeZone(request.TimeZone)) errors["timeZone"] = [Messages.PickTimeZone.In(reqLocale)];
            if (!request.DisclaimerAccepted) errors["disclaimerAccepted"] = [Messages.TickTheBox.In(reqLocale)];
            var termsVersion = string.IsNullOrWhiteSpace(request.TermsVersion) ? AccountRules.CurrentTermsVersion : request.TermsVersion.Trim();
            if (termsVersion != AccountRules.CurrentTermsVersion || termsVersion.Length > AccountRules.TermsVersionMax)
                errors["termsVersion"] = [Messages.InvalidTermsVersion.In(reqLocale)];
            // An invite counts whatever the setting, so an invited person gets its role while sign-ups are open too.
            var invite = await InviteEndpoints.FindUsableAsync(db, request.InviteCode, cancellationToken);
            if (invite is null && !string.IsNullOrWhiteSpace(request.InviteCode)) errors["inviteCode"] = [Messages.InviteNotValid.In(reqLocale)];
            else if (invite is null && await InviteEndpoints.InviteOnlyAsync(settings, cancellationToken))
                errors["inviteCode"] = [Messages.InviteNeeded.In(reqLocale)];
            if (errors.Count > 0) return TypedResults.ValidationProblem(errors);

            // The same answer whether or not the email has an account (spec: Sign-up). The emails go out after it.
            var answer = TypedResults.Accepted((string?)null, new AccountMessage(Messages.CheckYourEmail.In(reqLocale)));
            // Emails compare without case (citext); the warning goes to the address on file. The invite stays unused.
            if (await db.Users.Where(u => u.Email == email).Select(u => new { u.Email, u.Locale }).SingleOrDefaultAsync(cancellationToken) is { } registered)
            {
                hasher.HashPassword(null!, request.Password!); // The same slow step as a new account, so timing tells less.
                emails.AlreadyRegistered(registered.Email, registered.Locale);
                return answer;
            }

            var now = time.GetUtcNow();
            var user = new User
            {
                Email = email!, FirstName = firstName, LastName = lastName, TimeZone = request.TimeZone!, Locale = locale,
                Currency = currency, OverviewEmail = request.OverviewEmail, DisclaimerAcceptedAt = now,
                TermsVersionAccepted = termsVersion, TermsAcceptedAt = now,
                Role = invite?.Role ?? UserRole.User, InviteId = invite?.Id,
            };
            user.PasswordHash = hasher.HashPassword(user, request.Password!);
            db.Users.Add(user);
            // The invite's use and the account are one: a failed sign-up leaves the invite unused.
            await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
            {
                if (invite is not null && !await InviteEndpoints.ClaimAsync(db, invite.Id, cancellationToken))
                    return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["inviteCode"] = [Messages.InviteNotValid.In(reqLocale)] });
                try
                {
                    await db.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
                {
                    await transaction.RollbackAsync(cancellationToken);
                    var existingUser = await db.Users.Where(u => u.Email == email).Select(u => u.Locale).FirstOrDefaultAsync(cancellationToken);
                    emails.AlreadyRegistered(email!, existingUser ?? locale); // Signed up twice at the same moment.
                    return answer;
                }
            }
            emails.Verify(email!, await NewToken(db, user, EmailTokenPurpose.VerifyEmail, now + VerifyFor, cancellationToken), locale);
            await sessions.StartAsync(user, cancellationToken);
            return answer;
        })
        .RequireRateLimiting(SignUpLimit)
        .WithSummary("Creates an account and logs it in. Answers the same when the email already has one (that account gets an email).");

        group.MapPost("/log-in", async Task<Results<NoContent, ProblemHttpResult>> (LogInRequest request, AppDbContext db,
            IPasswordHasher<User> hasher, Sessions sessions, LogInThrottle throttle, HttpContext context, CancellationToken cancellationToken) =>
        {
            var reqLocale = RequestLocale.From(context.Request);
            var email = AccountRules.NormalizeEmail(request.Email) ?? (request.Email ?? "").Trim();
            // Counted before the password is checked, so tries sent at once can't all get past the lock.
            if (!throttle.TryAttempt(email))
                return TypedResults.Problem(Messages.TooManyTries.In(reqLocale), statusCode: StatusCodes.Status429TooManyRequests);

            // A password longer than any account can have is refused before it is hashed (it costs the same time).
            var tooLong = (request.Password?.Length ?? 0) > AccountRules.PasswordMax;
            var user = tooLong ? null : await db.Users.SingleOrDefaultAsync(u => u.Email == email, cancellationToken);
            var result = user?.PasswordHash is { } hash
                ? hasher.VerifyHashedPassword(user, hash, request.Password ?? "")
                : Failed(hasher, tooLong ? "" : request.Password);
            if (user is null || result == PasswordVerificationResult.Failed)
                return TypedResults.Problem(Messages.WrongLogIn.In(reqLocale), statusCode: StatusCodes.Status401Unauthorized);
            if (result == PasswordVerificationResult.SuccessRehashNeeded)
            {
                // Only over the hash just checked, so it can't undo a reset that finished meanwhile.
                var rehashed = hasher.HashPassword(user, request.Password!);
                var checkedHash = user.PasswordHash;
                await db.Users.Where(u => u.Id == user.Id && u.PasswordHash == checkedHash)
                    .ExecuteUpdateAsync(u => u.SetProperty(x => x.PasswordHash, rehashed), cancellationToken);
            }
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
        .RequireRateLimiting(LogOutLimit)
        .WithSummary("Ends this device's session.");

        group.MapGet("/me", async (Sessions sessions, CancellationToken cancellationToken) =>
        {
            var user = (await sessions.CurrentAsync(cancellationToken))!;
            return TypedResults.Ok(new AccountDto(user.Email, user.FirstName, user.LastName, user.EmailVerifiedAt is not null, user.Locale, user.TimeZone, user.OverviewEmail, user.Currency, user.Role, user.AiSharing, user.TermsVersionAccepted, user.TermsAcceptedAt));
        })
        .RequireUser()
        .WithSummary("The logged-in user, or 401: the app's start-up check.");

        group.MapPost("/currency", async Task<Results<NoContent, ValidationProblem>> (CurrencyRequest request, AppDbContext db,
            Sessions sessions, HttpContext context, CancellationToken cancellationToken) =>
        {
            var reqLocale = RequestLocale.From(context.Request);
            if (request.Currency is not { } currency || !AccountRules.Currencies.Contains(currency))
                return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["currency"] = [Messages.PickCurrency.In(reqLocale)] });
            var user = (await sessions.CurrentAsync(cancellationToken))!;
            // Strategies already started keep the currency they began with; only new ones use this.
            await db.Users.Where(u => u.Id == user.Id).ExecuteUpdateAsync(u => u.SetProperty(x => x.Currency, currency), cancellationToken);
            return TypedResults.NoContent();
        })
        .RequireUser()
        .WithSummary("Sets the currency of the user's new strategies. Running ones keep theirs.");

        group.MapPost("/name", async Task<Results<NoContent, ValidationProblem>> (NameRequest request, AppDbContext db,
            Sessions sessions, HttpContext context, CancellationToken cancellationToken) =>
        {
            var reqLocale = RequestLocale.From(context.Request);
            var errors = new Dictionary<string, string[]>();
            var (firstName, lastName) = CheckNames(request.FirstName, request.LastName, errors, reqLocale);
            if (errors.Count > 0) return TypedResults.ValidationProblem(errors);
            var user = (await sessions.CurrentAsync(cancellationToken))!;
            await db.Users.Where(u => u.Id == user.Id)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.FirstName, firstName).SetProperty(x => x.LastName, lastName), cancellationToken);
            return TypedResults.NoContent();
        })
        .RequireUser()
        .WithSummary("Sets the logged-in user's first and last name.");

        group.MapPost("/locale", async Task<Results<NoContent, ValidationProblem>> (LocaleRequest request, AppDbContext db,
            Sessions sessions, HttpContext context, CancellationToken cancellationToken) =>
        {
            var reqLocale = RequestLocale.From(context.Request);
            if (request.Locale is not { } locale || !AccountRules.Locales.Contains(locale))
                return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["locale"] = [Messages.PickLanguage.In(reqLocale)] });
            var user = (await sessions.CurrentAsync(cancellationToken))!;
            await db.Users.Where(u => u.Id == user.Id).ExecuteUpdateAsync(u => u.SetProperty(x => x.Locale, locale), cancellationToken);
            return TypedResults.NoContent();
        })
        .RequireUser()
        .WithSummary("Sets the language of the user's account and emails.");

        group.MapPost("/overview-email", async Task<Results<NoContent, ValidationProblem>> (OverviewEmailRequest request, AppDbContext db,
            Sessions sessions, HttpContext context, CancellationToken cancellationToken) =>
        {
            var reqLocale = RequestLocale.From(context.Request);
            if (request.Enabled is not { } enabled)
                return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["enabled"] = [Messages.SayTrueOrFalse.In(reqLocale)] });
            var user = (await sessions.CurrentAsync(cancellationToken))!;
            await db.Users.Where(u => u.Id == user.Id).ExecuteUpdateAsync(u => u.SetProperty(x => x.OverviewEmail, enabled), cancellationToken);
            return TypedResults.NoContent();
        })
        .RequireUser()
        .WithSummary("Turns the daily overview email on or off for the logged-in user.");

        // It doesn't log anyone in: a link that logged in whoever opened it would let someone log a victim into the
        // attacker's account, or keep using an account they created with the victim's email before the victim did.
        group.MapPost("/verify-email", async Task<Results<NoContent, ProblemHttpResult>> (TokenRequest request, AppDbContext db,
            TimeProvider time, HttpContext context, CancellationToken cancellationToken) =>
        {
            var reqLocale = RequestLocale.From(context.Request);
            if (await FindToken(db, request.Token, EmailTokenPurpose.VerifyEmail, time, cancellationToken) is not { } token
                || !await Claim(db, token, time, cancellationToken))
                return TypedResults.Problem(Messages.ExpiredLink.In(reqLocale), statusCode: StatusCodes.Status400BadRequest);
            var now = time.GetUtcNow();
            await db.Users.Where(u => u.Id == token.UserId && u.EmailVerifiedAt == null)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.EmailVerifiedAt, now), cancellationToken);
            return TypedResults.NoContent();
        })
        .RequireRateLimiting(PasswordLimit)
        .WithSummary("Confirms the email from the link in the email. It logs no one in.");

        group.MapPost("/resend-verification", async (AppDbContext db, Sessions sessions, AccountEmails emails, TimeProvider time,
            CancellationToken cancellationToken) =>
        {
            var user = (await sessions.CurrentAsync(cancellationToken))!;
            if (user.EmailVerifiedAt is null)
                emails.Verify(user.Email, await NewToken(db, user, EmailTokenPurpose.VerifyEmail, time.GetUtcNow() + VerifyFor, cancellationToken), user.Locale);
            return TypedResults.NoContent();
        })
        .RequireUser()
        .RequireRateLimiting(EmailLimit)
        .WithSummary("Sends the verification link again.");

        group.MapPost("/forgot-password", async (EmailRequest request, AppDbContext db, AccountEmails emails, TimeProvider time,
            HttpContext context, CancellationToken cancellationToken) =>
        {
            var reqLocale = RequestLocale.From(context.Request);
            // One statement finds the user and stores the link, so a known email takes as many round trips as an unknown one.
            if (AccountRules.NormalizeEmail(request.Email) is { } email)
            {
                var (token, hash) = SecretTokens.New();
                var expiresAt = time.GetUtcNow() + ResetFor;
                var registered = await db.Database.SqlQuery<ResetRecipient>($"""
                    WITH u AS (SELECT id, email, locale FROM users WHERE email = {email}::citext),
                         t AS (INSERT INTO email_tokens (id, user_id, purpose, token_hash, expires_at)
                               SELECT {Guid.CreateVersion7()}, id, 'reset_password', {hash}, {expiresAt} FROM u)
                    SELECT email::text AS "Email", locale AS "Locale" FROM u
                    """).ToListAsync(cancellationToken);
                if (registered is [var to]) emails.Reset(to.Email, token, to.Locale);
            }
            return TypedResults.Accepted((string?)null, new AccountMessage(Messages.ResetLinkSent.In(reqLocale)));
        })
        .RequireRateLimiting(EmailLimit)
        .WithSummary("Emails a reset link if the email has an account. Always the same answer.");

        group.MapPost("/reset-password", async Task<Results<NoContent, ProblemHttpResult, ValidationProblem>> (
            ResetPasswordRequest request, AppDbContext db, IPasswordHasher<User> hasher, Sessions sessions, LogInThrottle throttle,
            TimeProvider time, HttpContext context, CancellationToken cancellationToken) =>
        {
            var reqLocale = RequestLocale.From(context.Request);
            var token = await FindToken(db, request.Token, EmailTokenPurpose.ResetPassword, time, cancellationToken);
            if (token is null) return TypedResults.Problem(Messages.ExpiredLink.In(reqLocale), statusCode: StatusCodes.Status400BadRequest);
            var user = await db.Users.SingleAsync(u => u.Id == token.UserId, cancellationToken);
            // The password is checked before the link is used, so a refused password doesn't use it up.
            if (AccountRules.PasswordError(request.Password, user.Email, reqLocale) is { } error)
                return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["password"] = [error] });
            var newHash = hasher.HashPassword(user, request.Password!); // Before the transaction, so the lock isn't held over it.

            // All or nothing: the link, the other reset links, the new password and the ended sessions.
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await LockPasswordAsync(db, user.Id, cancellationToken);
            if (!await Claim(db, token, time, cancellationToken))
                return TypedResults.Problem(Messages.ExpiredLink.In(reqLocale), statusCode: StatusCodes.Status400BadRequest);
            var now = time.GetUtcNow();
            user.PasswordHash = newHash;
            user.EmailVerifiedAt ??= now; // The link reached the inbox.
            await BurnResetLinks(db, user.Id, now, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await sessions.EndAllAsync(user.Id, keepThisOne: false, cancellationToken);
            await sessions.StartAsync(user, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throttle.Succeeded(user.Email);
            return TypedResults.NoContent();
        })
        .RequireRateLimiting(PasswordLimit)
        .WithSummary("Sets a new password from the link in the email: ends every session, then logs this device in.");

        group.MapPost("/change-password", async Task<Results<NoContent, ValidationProblem>> (ChangePasswordRequest request,
            AppDbContext db, IPasswordHasher<User> hasher, Sessions sessions, TimeProvider time, HttpContext context, CancellationToken cancellationToken) =>
        {
            var reqLocale = RequestLocale.From(context.Request);
            var user = (await sessions.CurrentAsync(cancellationToken))!;
            var wrongCurrent = TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["currentPassword"] = [Messages.WrongCurrentPassword.In(reqLocale)] });
            if (user.PasswordHash is not { } checkedHash
                || hasher.VerifyHashedPassword(user, checkedHash, request.CurrentPassword ?? "") == PasswordVerificationResult.Failed)
                return wrongCurrent;
            if (AccountRules.PasswordError(request.NewPassword, user.Email, reqLocale) is { } error)
                return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["newPassword"] = [error] });
            var newHash = hasher.HashPassword(user, request.NewPassword!);

            // All or nothing, and after any reset that is running: if one changed the password meanwhile, this is refused.
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            if (await LockPasswordAsync(db, user.Id, cancellationToken) != checkedHash) return wrongCurrent;
            user.PasswordHash = newHash;
            await BurnResetLinks(db, user.Id, time.GetUtcNow(), cancellationToken); // A link sent earlier could undo it.
            await db.SaveChangesAsync(cancellationToken);
            await sessions.EndAllAsync(user.Id, keepThisOne: true, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return TypedResults.NoContent();
        })
        .RequireUser()
        .RequireRateLimiting(PasswordLimit)
        .WithSummary("Changes the password; ends the user's other sessions and unused reset links.");
    }

    static PasswordVerificationResult Failed(IPasswordHasher<User> hasher, string? password)
    {
        hasher.VerifyHashedPassword(null!, DummyHash, password ?? "");
        return PasswordVerificationResult.Failed;
    }

    /// <summary>The normalized names; the ones that don't pass are added to <c>errors</c>.</summary>
    static (string? FirstName, string? LastName) CheckNames(string? first, string? last, Dictionary<string, string[]> errors, string locale)
    {
        var firstName = AccountRules.NormalizeName(first);
        if (firstName is null) errors["firstName"] = [Messages.EnterFirstName.In(locale)];
        var lastName = AccountRules.NormalizeName(last);
        if (lastName is null) errors["lastName"] = [Messages.EnterLastName.In(locale)];
        return (firstName, lastName);
    }

    static async Task<string> NewToken(AppDbContext db, User user, EmailTokenPurpose purpose, DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        var (token, hash) = SecretTokens.New();
        db.EmailTokens.Add(new EmailToken { UserId = user.Id, Purpose = purpose, TokenHash = hash, ExpiresAt = expiresAt });
        await db.SaveChangesAsync(cancellationToken);
        return token;
    }

    /// <summary>
    /// Locks the user's row until the transaction ends, so password changes for one user run one at a time, and returns
    /// the password hash as stored now. NO KEY UPDATE still lets sessions and links be added for the user meanwhile.
    /// </summary>
    static async Task<string> LockPasswordAsync(AppDbContext db, Guid userId, CancellationToken cancellationToken) =>
        (await db.Database.SqlQuery<string>($"SELECT COALESCE(password_hash, '') FROM users WHERE id = {userId} FOR NO KEY UPDATE")
            .ToListAsync(cancellationToken)).Single(); // Not composed: a locking clause can't sit in EF's subquery.

    /// <summary>Uses up the user's unused reset links, once the password has changed.</summary>
    static Task BurnResetLinks(AppDbContext db, Guid userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        db.EmailTokens.Where(t => t.UserId == userId && t.Purpose == EmailTokenPurpose.ResetPassword && t.UsedAt == null)
            .ExecuteUpdateAsync(t => t.SetProperty(x => x.UsedAt, now), cancellationToken);

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
    /// Uses a found token: false when another request used it first. The claim is one UPDATE … WHERE used_at IS NULL,
    /// so two requests with the same link can't both use it.
    /// </summary>
    static async Task<bool> Claim(AppDbContext db, EmailToken found, TimeProvider time, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        return await db.EmailTokens.Where(t => t.Id == found.Id && t.UsedAt == null)
            .ExecuteUpdateAsync(t => t.SetProperty(x => x.UsedAt, now), cancellationToken) == 1;
    }
}
