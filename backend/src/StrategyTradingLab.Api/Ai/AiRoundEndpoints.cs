using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Auth;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Strategies;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Ai;

/// <summary>
/// <i>Your AI's top picks</i> (spec: Review, AI round): the prompt for the review and its editor, a strategy's round (read the
/// answer, then use its scores), the round history, the prompt of a running strategy, and the AI-sharing choice.
/// </summary>
public static class AiRoundEndpoints
{
    public static void MapAiRounds(this IEndpointRouteBuilder app)
    {
        app.MapPost("/templates/ai/prompt", async Task<Results<Ok<AiPromptPreview>, ValidationProblem>> (AiPromptRequest request, AppDbContext db,
            HttpContext http, TimeProvider time, CancellationToken cancellationToken) =>
        {
            var locale = RequestLocale.From(http.Request);
            var errors = new Dictionary<string, string[]>();
            var setup = new AiSetup(request.News, request.Mood, request.Figures, request.Instructions);
            var weeks = request.EveryWeeks ?? new AiParams().EveryWeeks;
            if (weeks is < 1 or > 8) errors["everyWeeks"] = [Messages.SettingRange.In(locale, 1, 8, 1)];
            var ids = request.AssetIds ?? [];
            if (ids.Count > 100) errors["assetIds"] = ["Too many stocks."];
            if (Apply(TemplateCatalog.Ai.ParamsJson(), setup, (field, message) => errors[field] = [message], locale) is not { } json || errors.Count > 0)
                return TypedResults.ValidationProblem(errors);

            var parameters = json.Deserialize<AiParams>(JsonSerializerOptions.Web)! with { EveryWeeks = weeks };
            var stocks = await db.Assets.AsNoTracking().Where(a => ids.Contains(a.Id)).Select(a => new AiStock(a.Id, a.Symbol, a.Name)).ToListAsync(cancellationToken);
            stocks = [.. stocks.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)];
            return TypedResults.Ok(new AiPromptPreview(AiPrompt.Version, AiPrompt.Build(parameters, DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime), stocks),
                AiPrompt.SuggestedInstructions(AiPrompt.On(parameters))));
        })
        .WithTags("Templates")
        .WithName("PreviewAiPrompt")
        .WithSummary("The AI template's prompt for the sources on, the user's own instructions and the ticked stocks.");

        var group = app.MapGroup("/strategies/{id:guid}").WithTags("AI rounds").RequireUser();

        group.MapGet("/ai-round", async Task<Results<Ok<AiRoundDetail>, NotFound>> (Guid id, AppDbContext db, Sessions sessions, AiRounds rounds,
            TimeProvider time, CancellationToken cancellationToken) =>
        {
            var user = await StrategyEndpoints.CurrentUser(sessions, cancellationToken);
            if (await FindAsync(db, id, user.Id, cancellationToken) is not { } strategy) return TypedResults.NotFound();
            var today = StrategyEndpoints.Today(user, time);
            if (await rounds.CurrentAsync(strategy, today, cancellationToken) is not { } round) return TypedResults.NotFound();

            var parameters = AiRoundRules.Params(strategy);
            var stocks = await rounds.StocksAsync(strategy, cancellationToken);
            var lastModel = await db.AiRounds.AsNoTracking().Where(r => r.StrategyId == strategy.Id && r.Status == AiRoundStatus.Done)
                .OrderByDescending(r => r.DoneAt).Select(r => r.AiModel).FirstOrDefaultAsync(cancellationToken);
            var texts = TemplateTexts.For(TemplateCatalog.Ai, user.Locale);
            return TypedResults.Ok(new AiRoundDetail(strategy.Id, strategy.Name, texts.Name, strategy.Mode, strategy.Currency, round.Id, round.DueOn,
                AiRoundRules.LastDay(round.DueOn, parameters), round.DueOn <= today, today, AiPrompt.Version, parameters.Instructions is not null,
                AiPrompt.Build(parameters, today, stocks), SetupOf(parameters), AiPrompt.SuggestedInstructions(AiPrompt.On(parameters)),
                [.. stocks.Select(s => new AiStockDto(s.AssetId, s.Symbol, s.Name))], parameters.EveryWeeks, parameters.TopN, lastModel, user.AiSharing,
                user.AiSharingChangedAt is not null));
        })
        .WithName("GetAiRound")
        .WithSummary("The strategy's AI round that is due (or the next one), with the prompt filled in for today.");

        group.MapPost("/ai-round/read", async Task<Results<Ok<AiReadingDto>, NotFound>> (Guid id, AiAnswerRequest request, AppDbContext db,
            Sessions sessions, AiRounds rounds, CancellationToken cancellationToken) =>
        {
            var user = await StrategyEndpoints.CurrentUser(sessions, cancellationToken);
            if (await FindAsync(db, id, user.Id, cancellationToken) is not { } strategy) return TypedResults.NotFound();
            var plan = await rounds.PlanAsync(strategy, request.Answer, user.Locale, cancellationToken);
            return TypedResults.Ok(ReadingOf(plan));
        })
        .WithName("ReadAiAnswer")
        .WithSummary("Reads the pasted answer: the scores, what stops them from being used, and the steps they would give. Saves nothing.");

        group.MapPost("/ai-round", async Task<Results<Ok<AiRoundResult>, ValidationProblem, NotFound, Conflict<string>>> (Guid id, AiRoundSubmit request,
            AppDbContext db, Sessions sessions, AiRounds rounds, TimeProvider time, CancellationToken cancellationToken) =>
        {
            var user = await StrategyEndpoints.CurrentUser(sessions, cancellationToken);
            if (await FindAsync(db, id, user.Id, cancellationToken) is not { } strategy) return TypedResults.NotFound();
            var today = StrategyEndpoints.Today(user, time);
            if (await rounds.CurrentAsync(strategy, today, cancellationToken) is not { } round) return TypedResults.NotFound();
            if (round.DueOn > today) return TypedResults.Conflict(Messages.RoundNotDue.In(user.Locale));

            var errors = new Dictionary<string, string[]>();
            var model = request.AiModel?.Trim() ?? "";
            if (model.Length is 0 or > AiRounds.MaxModel || model.Any(char.IsControl)) errors["aiModel"] = [Messages.PickAiModel.In(user.Locale)];
            var plan = await rounds.PlanAsync(strategy, request.Answer, user.Locale, cancellationToken);
            if (!plan.Reading.Ok) errors["answer"] = [.. plan.Reading.Problems];
            if (errors.Count > 0) return TypedResults.ValidationProblem(errors);

            var prompt = AiPrompt.Build(AiRoundRules.Params(strategy), today, plan.Stocks);
            var result = await rounds.SubmitAsync(strategy, user, round, plan, prompt, model, request.Share, cancellationToken);
            return result is null ? TypedResults.Conflict(Messages.RoundDone.In(user.Locale)) : TypedResults.Ok(result);
        })
        .WithName("UseAiScores")
        .WithSummary("\"Use these scores\": the round is done and its top scores get their steps.");

        group.MapGet("/ai-rounds", async Task<Results<Ok<AiRoundHistory>, NotFound>> (Guid id, AppDbContext db, Sessions sessions, AiRounds rounds,
            TimeProvider time, CancellationToken cancellationToken) =>
        {
            var user = await StrategyEndpoints.CurrentUser(sessions, cancellationToken);
            if (await FindAsync(db, id, user.Id, cancellationToken, running: false) is not { } strategy) return TypedResults.NotFound();
            var today = StrategyEndpoints.Today(user, time);
            await rounds.SweepAsync([new(strategy, today)], cancellationToken);
            var (items, total) = await rounds.HistoryAsync(strategy, cancellationToken);
            return TypedResults.Ok(new AiRoundHistory(strategy.Id, strategy.Name, strategy.Mode, today, items, total));
        })
        .WithName("GetAiRounds")
        .WithSummary("Every AI round of the strategy, newest first, with how the picks did against the rest.");

        group.MapPut("/ai-prompt", async Task<Results<NoContent, NotFound, ValidationProblem>> (Guid id, AiSetup request, AppDbContext db,
            Sessions sessions, CancellationToken cancellationToken) =>
        {
            var user = await StrategyEndpoints.CurrentUser(sessions, cancellationToken);
            var strategy = await db.Strategies.SingleOrDefaultAsync(s => s.Id == id && s.UserId == user.Id && s.Status == StrategyStatus.Running
                && s.TemplateKey == TemplateCatalog.Ai.Key, cancellationToken);
            if (strategy is null) return TypedResults.NotFound();
            var errors = new Dictionary<string, string[]>();
            if (Apply(strategy.Params, request, (field, message) => errors[field] = [message], user.Locale) is not { } json) return TypedResults.ValidationProblem(errors);
            strategy.Params = json;
            await db.SaveChangesAsync(cancellationToken);
            return TypedResults.NoContent();
        })
        .WithName("UpdateAiPrompt")
        .WithSummary("What a running AI strategy's prompt asks for: the sources on and the user's own instructions.");

        app.MapPost("/account/ai-sharing", async Task<Results<NoContent, ValidationProblem>> (AiSharingRequest request, AppDbContext db,
            Sessions sessions, TimeProvider time, CancellationToken cancellationToken) =>
        {
            var user = await StrategyEndpoints.CurrentUser(sessions, cancellationToken);
            if (request.Enabled is not { } enabled)
                return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["enabled"] = [Messages.SayTrueOrFalse.In(user.Locale)] });
            var now = time.GetUtcNow();
            await db.Users.Where(u => u.Id == user.Id)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.AiSharing, enabled).SetProperty(x => x.AiSharingChangedAt, now), cancellationToken);
            return TypedResults.NoContent();
        })
        .RequireUser()
        .WithTags("Account")
        .WithName("SetAiSharing")
        .WithSummary("Turns sharing AI rounds anonymously on or off. Off stops future sharing; what was shared can't be traced back to remove.");
    }

    /// <summary>One of the user's AI strategies (running unless <paramref name="running"/> is false); null for anything else.</summary>
    static Task<Strategy?> FindAsync(AppDbContext db, Guid id, Guid userId, CancellationToken cancellationToken, bool running = true) =>
        db.Strategies.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id && s.UserId == userId && s.TemplateKey == TemplateCatalog.Ai.Key
            && (!running || s.Status == StrategyStatus.Running), cancellationToken);

    public static AiReadingDto ReadingOf(AiPlan plan) =>
        new(AiRounds.ScoresOf(plan), plan.Reading.Problems, plan.Reading.Ok, plan.Budget.MaxSteps, plan.Budget.Step, plan.Reading.Ok ? plan.Short : null);

    public static AiSetup SetupOf(AiParams parameters) => new(parameters.News, parameters.Mood, parameters.Figures, parameters.Instructions);

    /// <summary>
    /// <paramref name="current"/> params with what the prompt asks for. Instructions are trimmed; empty ones, or the suggested ones
    /// word for word, count as not edited. Null, with the reasons in <paramref name="locale"/> in <paramref name="fail"/>, when refused.
    /// </summary>
    public static JsonDocument? Apply(JsonDocument current, AiSetup? setup, Action<string, string> fail, string locale = "en")
    {
        if (setup is null) return current;
        var parameters = current.Deserialize<AiParams>(JsonSerializerOptions.Web) ?? new AiParams();
        var instructions = setup.Instructions?.Replace("\r\n", "\n").Trim();
        var merged = parameters with { News = setup.News, Mood = setup.Mood, Figures = setup.Figures };
        if (string.IsNullOrEmpty(instructions) || instructions == AiPrompt.SuggestedInstructions(AiPrompt.On(merged))) instructions = null;

        var ok = true;
        if (!merged.News && !merged.Mood && !merged.Figures)
        {
            fail("sources", Messages.OneSourceOn.In(locale));
            ok = false;
        }
        if (instructions?.Length > AiParams.MaxInstructions)
        {
            fail("instructions", Messages.InstructionsLength.In(locale, AiParams.MaxInstructions));
            ok = false;
        }
        return ok ? JsonSerializer.SerializeToDocument(merged with { Instructions = instructions }, JsonSerializerOptions.Web) : null;
    }
}
