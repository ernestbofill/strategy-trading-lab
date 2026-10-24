using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Auth;
using StrategyTradingLab.Api.Data;

namespace StrategyTradingLab.Api.Templates;

public static class TemplateEndpoints
{
    public static void MapTemplates(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/templates").WithTags("Templates");

        group.MapGet("/", async (Sessions sessions, CancellationToken cancellationToken) =>
        {
            var locale = await UserLocale(sessions, cancellationToken);
            return TemplateCatalog.All.Select(template => Summary(template, TemplateTexts.For(template, locale))).ToList();
        })
        .WithName("GetTemplates")
        .WithSummary("The templates for the pick screen, in catalogue order.");

        group.MapGet("/{key}", async Task<Results<Ok<TemplateDetail>, NotFound>> (string key, AppDbContext db,
            Sessions sessions, CancellationToken cancellationToken) =>
        {
            if (TemplateCatalog.Find(key) is not { } template) return TypedResults.NotFound();

            var locale = await UserLocale(sessions, cancellationToken);
            var securities = await Securities(db, template, locale, cancellationToken);
            return TypedResults.Ok(Detail(template, TemplateTexts.For(template, locale), securities));
        })
        .WithName("GetTemplate")
        .WithSummary("One template with its rules, starter list or fund, backtest and tested defaults.");
    }

    /// <summary>The logged-in user's language; visitors get the default (the templates are public).</summary>
    static async Task<string> UserLocale(Sessions sessions, CancellationToken cancellationToken) =>
        (await sessions.CurrentAsync(cancellationToken))?.Locale ?? TemplateTexts.DefaultLocale;

    /// <summary>The template's assets, sorted by name, with what they do in the locale (English when missing).</summary>
    static async Task<List<SecurityDto>> Securities(AppDbContext db, Template template, string locale,
        CancellationToken cancellationToken)
    {
        var ids = template.Watches.Assets.Select(asset => asset.Id).ToList();
        var securities = await db.Assets
            .Where(asset => ids.Contains(asset.Id))
            .Select(asset => new SecurityDto(asset.Id, asset.Symbol, asset.Name,
                asset.Texts.Where(t => t.Locale == locale).Select(t => t.What).FirstOrDefault()
                    ?? asset.Texts.Where(t => t.Locale == TemplateTexts.DefaultLocale).Select(t => t.What).First()))
            .ToListAsync(cancellationToken);
        return [.. securities.OrderBy(security => security.Name, StringComparer.InvariantCultureIgnoreCase)];
    }

    static TemplateSummary Summary(Template template, TemplateTexts texts) => new(
        template.Key, template.EngineKey, texts.Name, texts.Tagline, texts.Idea, template.InvestsIn, template.Signals,
        template.Params is AiParams ai ? new AiRoundDto(ai.EveryWeeks, ai.TopN) : null);

    static TemplateDetail Detail(Template template, TemplateTexts texts, List<SecurityDto> securities) =>
        new(Summary(template, texts))
        {
            Rationale = texts.Rationale,
            Rules = texts.Rules,
            Watches = template.Watches is DefaultFund ? new FundWatches(securities.Single()) : new StockWatches(securities),
            Backtest = template.Backtest is { } periods && texts.Backtest is { } words
                ? new BacktestDto(words.Scope, periods, TemplateCatalog.PriceProvider, words.Years, words.Universe,
                    words.TestedDefaults, TemplateCatalog.Fee.Fixed, TemplateCatalog.Fee.Percent)
                : null,
            Defaults = new TemplateDefaults(template.DefaultsVersion, template.Budget, template.Params),
            Settings = [.. template.Settings.Select(setting =>
            {
                var words = texts.Settings![setting.Key];
                return new SettingDto(setting.Key, words.Sentence, words.Value, setting.Step, setting.Min, setting.Max, setting.Below);
            })],
        };
}
