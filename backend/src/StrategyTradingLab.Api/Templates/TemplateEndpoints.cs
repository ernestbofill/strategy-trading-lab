using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Data;

namespace StrategyTradingLab.Api.Templates;

public static class TemplateEndpoints
{
    public static void MapTemplates(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/templates").WithTags("Templates");

        group.MapGet("/", async (AppDbContext db, CancellationToken cancellationToken) =>
        {
            var locale = await UserLocale(db, cancellationToken);
            return TemplateCatalog.All.Select(template => Summary(template, TemplateCopy.For(template, locale))).ToList();
        })
        .WithName("GetTemplates")
        .WithSummary("The templates for the pick screen, in catalogue order.");

        group.MapGet("/{key}", async Task<Results<Ok<TemplateDetail>, NotFound>> (string key, AppDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (TemplateCatalog.Find(key) is not { } template) return TypedResults.NotFound();

            var locale = await UserLocale(db, cancellationToken);
            var securities = await Securities(db, template, locale, cancellationToken);
            return TypedResults.Ok(Detail(template, TemplateCopy.For(template, locale), securities));
        })
        .WithName("GetTemplate")
        .WithSummary("One template with its rules, starter list or fund, backtest and tested defaults.");
    }

    // Until auth exists, every request runs as the dev user.
    static Task<string> UserLocale(AppDbContext db, CancellationToken cancellationToken) =>
        db.Users.Where(user => user.Id == DevSeed.DevUserId).Select(user => user.Locale).SingleAsync(cancellationToken);

    /// <summary>The template's assets, sorted by name, with what they do in the locale (English when missing).</summary>
    static async Task<List<SecurityDto>> Securities(AppDbContext db, Template template, string locale,
        CancellationToken cancellationToken)
    {
        var ids = template.Assets.Select(asset => asset.Id).ToList();
        var rows = await db.Assets
            .Where(asset => ids.Contains(asset.Id))
            .Select(asset => new
            {
                asset.Id,
                asset.Symbol,
                asset.Name,
                What = asset.Texts.Where(t => t.Locale == locale).Select(t => t.What).FirstOrDefault()
                    ?? asset.Texts.Where(t => t.Locale == TemplateCopy.DefaultLocale).Select(t => t.What).First(),
            })
            .ToListAsync(cancellationToken);
        return [.. rows.Select(r => new SecurityDto(r.Id, r.Symbol, r.Name, r.What)).OrderBy(s => s.Name, StringComparer.InvariantCultureIgnoreCase)];
    }

    static TemplateSummary Summary(Template template, TemplateCopy copy) => new(
        template.Key, template.EngineKey, copy.Name, copy.Tagline, copy.Idea, template.InvestsIn, template.Signals,
        AiRound(template));

    static TemplateDetail Detail(Template template, TemplateCopy copy, List<SecurityDto> securities) => new(
        template.Key, template.EngineKey, copy.Name, copy.Tagline, copy.Idea, template.InvestsIn, template.Signals,
        AiRound(template),
        copy.Rationale,
        copy.Rules,
        template.WatchesFund ? new FundWatches(securities.Single()) : new StockWatches(securities),
        Backtest(template, copy),
        new TemplateDefaults(template.DefaultsVersion, template.Budget, template.DefaultParamsJson()));

    static AiRoundDto? AiRound(Template template) =>
        template.DefaultParams is AiParams ai ? new AiRoundDto(ai.EveryWeeks, ai.TopN) : null;

    static BacktestDto? Backtest(Template template, TemplateCopy copy) =>
        template.Backtest is { } periods && copy.Backtest is { } words
            ? new BacktestDto(words.Scope, periods, words.Provider, words.Years, words.Universe, words.TestedDefaults,
                template.Fee.Fixed, template.Fee.Percent)
            : null;
}
