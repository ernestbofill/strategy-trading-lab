using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Auth;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Feedback;

public record CreateFeedbackRequest(FeedbackKind? Kind, string? Text, string? Route = null, string? Element = null, string? SelectedText = null);

/// <summary>What the owner can change afterwards: the comment, its type, and open / closed. Where and by whom it was made stays.</summary>
public record UpdateFeedbackRequest(FeedbackKind? Kind = null, FeedbackStatus? Status = null, string? Text = null);

public record FeedbackDto(Guid Id, string UserEmail, FeedbackKind Kind, FeedbackStatus Status, string Text, string? Route,
    string? Element, string? SelectedText, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? ClosedAt);

/// <summary>
/// Comments and issues written in the app: bug, idea, note or feature, optionally pinned to the text selected on a screen.
/// Any logged-in user with a confirmed email may submit feedback; only the owner reviews, edits, closes or deletes it.
/// </summary>
public static partial class FeedbackEndpoints
{
    public const string SubmitLimit = "feedback-submit";
    public const int MaxTextLength = 4000, MaxRouteLength = 300, MaxElementLength = 300, MaxSelectedTextLength = 2000;

    // A path inside the app: one leading "/", then no second "/" or backslash (browsers read "/\x" as "//x"),
    // no whitespace or control characters (browsers drop tabs and newlines, so "/<tab>/x" is "//x").
    [GeneratedRegex(@"^/(?!/)(?!.*//)[^\\\s\x00-\x1f\x7f]*$")]
    public static partial Regex InAppPath();

    public static void MapFeedback(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/feedback").WithTags("Feedback");

        group.MapPost("/", async Task<Results<Created<FeedbackDto>, ValidationProblem>> (CreateFeedbackRequest request,
            AppDbContext db, Sessions sessions, TimeProvider time, CancellationToken cancellationToken) =>
        {
            var errors = ValidateCreate(request, out var text, out var route, out var element, out var selectedText);
            if (errors.Count > 0) return TypedResults.ValidationProblem(errors);

            var user = (await sessions.CurrentAsync(cancellationToken))!;
            var now = time.GetUtcNow();
            var item = new FeedbackItem
            {
                Id = Guid.CreateVersion7(),
                UserId = user.Id,
                Kind = request.Kind!.Value,
                Status = FeedbackStatus.Open,
                Text = text,
                Route = route,
                Element = element,
                SelectedText = selectedText,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.FeedbackItems.Add(item);
            await db.SaveChangesAsync(cancellationToken);
            return TypedResults.Created($"/feedback/{item.Id}", ToDto(item, user.Email));
        })
        .RequireAuthorization(Policies.VerifiedEmail)
        .RequireRateLimiting(SubmitLimit)
        .WithSummary("Submits a feedback comment, optionally pinned to selected text on a screen. Requires a confirmed email.");

        group.MapGet("/", async (string? status, AppDbContext db, CancellationToken cancellationToken) =>
        {
            FeedbackStatus? parsedStatus = null;
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (!Enum.TryParse<FeedbackStatus>(status, ignoreCase: true, out var s))
                    return Results.BadRequest(new HttpValidationProblemDetails(new Dictionary<string, string[]>
                    {
                        ["status"] = ["Pick open or closed."]
                    }));
                parsedStatus = s;
            }

            var query = from item in db.FeedbackItems.AsNoTracking()
                        join user in db.Users.AsNoTracking() on item.UserId equals user.Id
                        where parsedStatus == null || item.Status == parsedStatus
                        orderby item.CreatedAt descending, item.Id descending
                        select new FeedbackDto(item.Id, user.Email, item.Kind, item.Status, item.Text, item.Route, item.Element,
                            item.SelectedText, item.CreatedAt, item.UpdatedAt, item.ClosedAt);
            return Results.Ok(await query.ToListAsync(cancellationToken));
        })
        .RequireAuthorization(Policies.Owner)
        .WithSummary("Lists feedback comments, newest first, optionally filtered by status (open or closed). Owner only.");

        group.MapPatch("/{id:guid}", async Task<Results<Ok<FeedbackDto>, NotFound, ValidationProblem>> (Guid id,
            UpdateFeedbackRequest request, AppDbContext db, TimeProvider time, CancellationToken cancellationToken) =>
        {
            var errors = ValidateUpdate(request, out var text);
            if (errors.Count > 0) return TypedResults.ValidationProblem(errors);

            var item = await db.FeedbackItems.SingleOrDefaultAsync(f => f.Id == id, cancellationToken);
            if (item is null) return TypedResults.NotFound();

            if (request.Kind is { } kind && item.Kind != kind) item.Kind = kind;
            if (text is not null && item.Text != text) item.Text = text;
            if (request.Status is { } status && item.Status != status)
            {
                item.Status = status;
                item.ClosedAt = status == FeedbackStatus.Closed ? time.GetUtcNow() : null;
            }
            if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync(cancellationToken);

            var email = await db.Users.Where(u => u.Id == item.UserId).Select(u => u.Email).SingleAsync(cancellationToken);
            return TypedResults.Ok(ToDto(item, email));
        })
        .RequireAuthorization(Policies.Owner)
        .WithSummary("Updates a feedback comment's type, text or open/closed status. Owner only.");

        group.MapDelete("/{id:guid}", async Task<Results<NoContent, NotFound>> (Guid id, AppDbContext db, CancellationToken cancellationToken) =>
        {
            var deleted = await db.FeedbackItems.Where(f => f.Id == id).ExecuteDeleteAsync(cancellationToken);
            return deleted == 0 ? TypedResults.NotFound() : TypedResults.NoContent();
        })
        .RequireAuthorization(Policies.Owner)
        .WithSummary("Deletes a feedback comment. Owner only.");
    }

    public static Dictionary<string, string[]> ValidateCreate(CreateFeedbackRequest request, out string text, out string? route,
        out string? element, out string? selectedText)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Kind is not { } kind || !Enum.IsDefined(kind))
            errors["kind"] = ["Pick a feedback type."];

        text = (request.Text ?? "").Trim();
        if (text.Length == 0) errors["text"] = ["Write a comment."];
        else if (text.Length > MaxTextLength) errors["text"] = [$"Use at most {MaxTextLength:N0} characters."];

        route = NormalizeOptional(request.Route);
        if (route is not null)
        {
            if (route.Length > MaxRouteLength) errors["route"] = [$"Use at most {MaxRouteLength} characters."];
            else if (!InAppPath().IsMatch(route)) errors["route"] = ["The page must be a path in the app, like /dashboard."];
        }

        element = NormalizeOptional(request.Element);
        if (element is { Length: > MaxElementLength })
            errors["element"] = [$"Use at most {MaxElementLength} characters."];

        selectedText = NormalizeOptional(request.SelectedText);
        if (selectedText is { Length: > MaxSelectedTextLength })
            errors["selectedText"] = [$"Use at most {MaxSelectedTextLength:N0} characters."];

        return errors;
    }

    public static Dictionary<string, string[]> ValidateUpdate(UpdateFeedbackRequest request, out string? text)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Kind is { } kind && !Enum.IsDefined(kind))
            errors["kind"] = ["Pick a feedback type."];
        if (request.Status is { } status && !Enum.IsDefined(status))
            errors["status"] = ["Pick open or closed."];

        if (request.Text is not null)
        {
            text = request.Text.Trim();
            if (text.Length == 0) errors["text"] = ["Write a comment."];
            else if (text.Length > MaxTextLength) errors["text"] = [$"Use at most {MaxTextLength:N0} characters."];
        }
        else
        {
            text = null;
        }
        return errors;
    }

    static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    static FeedbackDto ToDto(FeedbackItem item, string userEmail) =>
        new(item.Id, userEmail, item.Kind, item.Status, item.Text, item.Route, item.Element, item.SelectedText,
            item.CreatedAt, item.UpdatedAt, item.ClosedAt);
}
