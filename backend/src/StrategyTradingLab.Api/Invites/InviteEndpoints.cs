using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Admin;
using StrategyTradingLab.Api.Auth;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Email;

namespace StrategyTradingLab.Api.Invites;

/// <summary>Someone who signed up with an invite.</summary>
public record InvitedUser(string Email, string? FirstName, string? LastName, DateTimeOffset JoinedAt);

/// <summary>An invite on /admin/invites, with its link and the message to copy, in its language.</summary>
public record AdminInvite(Guid Id, string Code, string Link, string Message, string FirstName, string? LastName, string? Email,
    string Locale, UserRole Role, int MaxUses, int Uses, DateTimeOffset CreatedAt, DateTimeOffset? SentAt, DateTimeOffset? RevokedAt,
    IReadOnlyList<InvitedUser> Joined);

/// <summary>A new invite. <c>Send</c> also emails it to <c>Email</c>.</summary>
public record NewInvite(string? FirstName, string? LastName, string? Email, string? Locale, bool Send = false);

/// <summary>30 invite emails an hour in all, so the page can't flood the mail account's sending limit.</summary>
public sealed class InviteEmailLimit() : HourlyLimit(30);

/// <summary>
/// The owner's invites (spec: Admin, Invites): make one for a person, in their language, and send it by email or copy
/// its message. Each one is used once and gives the vip role. Only the owner can invite for now; vips' own links come later.
/// </summary>
public static class InviteEndpoints
{
    const string TooManyEmails = "Too many invite emails: try again in an hour, or copy the message instead.";

    public static void MapInvites(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/invites").WithTags("Admin").RequireAuthorization(Policies.Owner);

        group.MapGet("/", async (AppDbContext db, Sessions sessions, InviteTexts texts, CancellationToken cancellationToken) =>
        {
            var owner = (await sessions.CurrentAsync(cancellationToken))!;
            var invites = await db.Invites.AsNoTracking().Where(i => i.CreatedBy == owner.Id)
                .OrderByDescending(i => i.CreatedAt).ToListAsync(cancellationToken);
            var ids = invites.Select(i => i.Id).ToList();
            var joined = (await db.Users.AsNoTracking().Where(u => u.InviteId != null && ids.Contains(u.InviteId.Value))
                    .OrderBy(u => u.CreatedAt)
                    .Select(u => new { InviteId = u.InviteId!.Value, User = new InvitedUser(u.Email, u.FirstName, u.LastName, u.CreatedAt) })
                    .ToListAsync(cancellationToken))
                .ToLookup(x => x.InviteId, x => x.User);
            return invites.Select(i => Dto(i, texts, owner.FirstName, [.. joined[i.Id]])).ToList();
        })
        .WithSummary("The owner's invites, newest first, with who joined with each.");

        group.MapPost("/", async Task<Results<Ok<AdminInvite>, ValidationProblem, ProblemHttpResult>> (NewInvite request,
            AppDbContext db, Sessions sessions, InviteTexts texts, EmailQueue emails, InviteEmailLimit limit, TimeProvider time,
            CancellationToken cancellationToken) =>
        {
            var errors = new Dictionary<string, string[]>();
            var firstName = AccountRules.NormalizeName(request.FirstName);
            if (firstName is null) errors["firstName"] = ["Enter their first name: the message greets them by it."];
            string? lastName = null;
            if (!string.IsNullOrWhiteSpace(request.LastName) && (lastName = AccountRules.NormalizeName(request.LastName)) is null)
                errors["lastName"] = [$"Use at most {AccountRules.NameMax} characters."];
            string? email = null;
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                if (request.Send) errors["email"] = ["Enter their email to send it."];
            }
            else if ((email = AccountRules.NormalizeEmail(request.Email)) is null) errors["email"] = ["Use one email address, e.g. anna@gmail.com."];
            if (request.Locale is not { } locale || !AccountRules.Locales.Contains(locale)) errors["locale"] = ["Pick English or Catalan."];
            if (errors.Count > 0) return TypedResults.ValidationProblem(errors);
            if (request.Send && !limit.TryAcquire())
                return TypedResults.Problem(TooManyEmails, statusCode: StatusCodes.Status429TooManyRequests);

            var owner = (await sessions.CurrentAsync(cancellationToken))!;
            var invite = new Invite
            {
                Code = InviteCodes.New(), CreatedBy = owner.Id, Role = UserRole.Vip, FirstName = firstName!, LastName = lastName,
                Email = email, Locale = request.Locale!, CreatedAt = time.GetUtcNow(),
            };
            db.Invites.Add(invite);
            await db.SaveChangesAsync(cancellationToken); // Two random codes clashing is too rare to retry: the owner tries again.
            if (request.Send) await SendAsync(db, invite, owner, texts, emails, time, cancellationToken);
            return TypedResults.Ok(Dto(invite, texts, owner.FirstName, []));
        })
        .WithSummary("Makes an invite for one person; with send, also emails it to them.");

        group.MapPost("/{id:guid}/send", async Task<Results<Ok<AdminInvite>, NotFound, ProblemHttpResult>> (Guid id, AppDbContext db,
            Sessions sessions, InviteTexts texts, EmailQueue emails, InviteEmailLimit limit, TimeProvider time, CancellationToken cancellationToken) =>
        {
            var owner = (await sessions.CurrentAsync(cancellationToken))!;
            if (await db.Invites.SingleOrDefaultAsync(i => i.Id == id && i.CreatedBy == owner.Id, cancellationToken) is not { } invite)
                return TypedResults.NotFound();
            if (invite.Email is null)
                return TypedResults.Problem("This invite has no email: copy its message instead.", statusCode: StatusCodes.Status400BadRequest);
            if (!Usable(invite)) return TypedResults.Problem("This invite can't be used any more.", statusCode: StatusCodes.Status400BadRequest);
            if (!limit.TryAcquire()) return TypedResults.Problem(TooManyEmails, statusCode: StatusCodes.Status429TooManyRequests);
            await SendAsync(db, invite, owner, texts, emails, time, cancellationToken);
            return TypedResults.Ok(Dto(invite, texts, owner.FirstName, []));
        })
        .WithSummary("Emails an unused invite (again) to the address it was made for.");

        group.MapPost("/{id:guid}/revoke", async Task<Results<NoContent, NotFound>> (Guid id, AppDbContext db, Sessions sessions,
            TimeProvider time, CancellationToken cancellationToken) =>
        {
            var owner = (await sessions.CurrentAsync(cancellationToken))!;
            var now = time.GetUtcNow();
            var found = await db.Invites.Where(i => i.Id == id && i.CreatedBy == owner.Id)
                .ExecuteUpdateAsync(i => i.SetProperty(x => x.RevokedAt, x => x.RevokedAt ?? now), cancellationToken);
            return found == 1 ? TypedResults.NoContent() : TypedResults.NotFound();
        })
        .WithSummary("Stops an invite from being used. Accounts made with it stay as they are.");
    }

    /// <summary>Not revoked and not used up.</summary>
    public static bool Usable(Invite invite) => invite.RevokedAt is null && invite.Uses < invite.MaxUses;

    /// <summary>The usable invite with that code (typed in any case, with or without the dash), or null.</summary>
    public static async Task<Invite?> FindUsableAsync(AppDbContext db, string? typed, CancellationToken cancellationToken) =>
        InviteCodes.Normalize(typed) is { } code
            ? await db.Invites.AsNoTracking()
                .SingleOrDefaultAsync(i => i.Code == code && i.RevokedAt == null && i.Uses < i.MaxUses, cancellationToken)
            : null;

    /// <summary>Uses the invite once: false when it was used up or revoked meanwhile. One UPDATE, so two sign-ups can't share its last use.</summary>
    public static async Task<bool> ClaimAsync(AppDbContext db, Guid id, CancellationToken cancellationToken) =>
        await db.Invites.Where(i => i.Id == id && i.RevokedAt == null && i.Uses < i.MaxUses)
            .ExecuteUpdateAsync(i => i.SetProperty(x => x.Uses, x => x.Uses + 1), cancellationToken) == 1;

    /// <summary>Whether sign-ups need an invite: the admin setting, open unless it says invite_only.</summary>
    public static async Task<bool> InviteOnlyAsync(SettingsStore settings, CancellationToken cancellationToken) =>
        (await settings.ValuesAsync(cancellationToken)).GetValueOrDefault(SettingDefinitions.SignUps) == SettingDefinitions.SignUpsInviteOnly;

    /// <summary>Queues the invite's email, with answers going to the owner, and notes when it was sent.</summary>
    static async Task SendAsync(AppDbContext db, Invite invite, User owner, InviteTexts texts, EmailQueue emails, TimeProvider time,
        CancellationToken cancellationToken)
    {
        emails.Enqueue(new OutgoingEmail(invite.Email!, texts.Email(invite.Code, invite.Locale, invite.FirstName, owner.FirstName), owner.Email));
        invite.SentAt = time.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
    }

    static AdminInvite Dto(Invite i, InviteTexts texts, string? from, IReadOnlyList<InvitedUser> joined) =>
        new(i.Id, i.Code, texts.Link(i.Code), texts.Message(i.Code, i.Locale, i.FirstName, from), i.FirstName, i.LastName, i.Email,
            i.Locale, i.Role, i.MaxUses, i.Uses, i.CreatedAt, i.SentAt, i.RevokedAt, joined);
}
