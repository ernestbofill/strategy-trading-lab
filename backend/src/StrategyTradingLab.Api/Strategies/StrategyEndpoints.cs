using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Strategies;

public static class StrategyEndpoints
{
    public static void MapStrategies(this IEndpointRouteBuilder app)
    {
        app.MapGet("/dashboard", async (AppDbContext db, TimeProvider time, CancellationToken cancellationToken) =>
        {
            var user = await CurrentUser(db, cancellationToken);
            var strategies = await db.Strategies
                .Where(strategy => strategy.UserId == user.Id && strategy.Status == StrategyStatus.Running)
                .OrderByDescending(strategy => strategy.StartedOn).ThenByDescending(strategy => strategy.CreatedAt)
                .ToListAsync(cancellationToken);
            return new Dashboard(Today(user, time), [.. strategies.Select(strategy => Summary(strategy, user.Locale))], []);
        })
        .WithTags("Dashboard")
        .WithName("GetDashboard")
        .WithSummary("The user's running strategies and what waits for them (nothing yet: signals come later).");

        var group = app.MapGroup("/strategies").WithTags("Strategies");

        group.MapPost("/", async Task<Results<Created<StrategySummary>, ValidationProblem, Conflict<string>, NotFound>> (
            NewStrategy request, AppDbContext db, TimeProvider time, CancellationToken cancellationToken) =>
        {
            if (TemplateCatalog.Find(request.TemplateKey ?? "") is not { } template) return TypedResults.NotFound();

            var ids = request.AssetIds ?? [];
            var assets = await db.Assets.Where(asset => ids.Contains(asset.Id)).ToListAsync(cancellationToken);
            var errors = NewStrategyValidation.Validate(request, template, assets);
            if (errors.Count > 0) return TypedResults.ValidationProblem(errors);

            var user = await CurrentUser(db, cancellationToken);
            var name = request.Name.Trim();
            if (await db.Strategies.AnyAsync(strategy => strategy.UserId == user.Id && strategy.Status == StrategyStatus.Running
                    && strategy.Name.ToLower() == name.ToLower(), cancellationToken))
                return TypedResults.Conflict("You already have a running strategy with that name.");

            var strategy = new Strategy
            {
                UserId = user.Id,
                TemplateKey = template.Key,
                DefaultsVersion = template.DefaultsVersion,
                Name = name,
                Mode = request.Mode,
                Status = StrategyStatus.Running,
                StartedOn = Today(user, time),
                Currency = user.Currency,
                Budget = request.Budget,
                Params = template.ParamsJson(),
                Assets = [.. assets.Select(asset => new StrategyAsset
                    { AssetId = asset.Id, Active = true, AddedAt = time.GetUtcNow() })],
            };
            db.Strategies.Add(strategy);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            // Two requests with the same name at once: the unique running name index catches the second.
            catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                return TypedResults.Conflict("You already have a running strategy with that name.");
            }
            return TypedResults.Created($"/strategies/{strategy.Id}", Summary(strategy, user.Locale));
        })
        .WithName("StartStrategy")
        .WithSummary("Starts a strategy from a template, in simulation or live, with the user's budget and stocks.");
    }

    // Until auth exists, every request runs as the dev user (Ernest).
    static Task<User> CurrentUser(AppDbContext db, CancellationToken cancellationToken) =>
        db.Users.SingleAsync(user => user.Id == DevSeed.DevUserId, cancellationToken);

    /// <summary>The user's local date; UTC if their time zone isn't known to the server.</summary>
    static DateOnly Today(User user, TimeProvider time)
    {
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(user.TimeZone); }
        catch (Exception error) when (error is TimeZoneNotFoundException or InvalidTimeZoneException) { zone = TimeZoneInfo.Utc; }
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), zone).DateTime);
    }

    static StrategySummary Summary(Strategy strategy, string locale)
    {
        var template = TemplateCatalog.Find(strategy.TemplateKey)!;
        var texts = TemplateTexts.For(template, locale);
        var past = template.Backtest?.LastOrDefault() is { } all && texts.Backtest is { } words
            ? new PastResult(words.Years, all.Template, all.Index)
            : null;
        // No trades are recorded yet, so nothing is used and there are no results of its own.
        return new StrategySummary(strategy.Id, strategy.Name, strategy.TemplateKey, texts.Name, strategy.Mode,
            strategy.StartedOn, strategy.Budget.Amount, 0, null, past);
    }
}
