using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Strategies;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Admin;

/// <summary>
/// A template on /admin/templates: what a new strategy starts with now, next to the tested values (the code's). <c>Changed</c>
/// when any of it differs. <c>ClosePct</c> is null for the AI template, whose stocks have no buy rule to be close to.
/// </summary>
public record AdminTemplate(string Key, string Name, InvestsIn InvestsIn, bool Changed, IReadOnlyList<AdminTemplateSetting> Settings,
    IReadOnlyList<AdminTemplateAsset> Assets, IReadOnlyList<Guid> TestedAssetIds, decimal? ClosePct, decimal TestedClosePct);

/// <summary>A setting of Advanced settings, with its words (as the screen's), range, tested default and the default in use.</summary>
public record AdminTemplateSetting(string Key, string Sentence, string Value, decimal Step, decimal Min, decimal Max, string? Below,
    decimal Tested, decimal Current);

/// <summary>A stock of the starter list, or the fund. A hidden one stays listed, so the owner sees it and takes it out.</summary>
public record AdminTemplateAsset(Guid Id, string Symbol, string Name, bool Active);

/// <summary>What a new strategy should start with: every setting's default, the starter list or the one fund, and "getting close".</summary>
public record TemplateChange(IReadOnlyDictionary<string, decimal>? Defaults, IReadOnlyList<Guid>? AssetIds, decimal? ClosePct);

/// <summary>
/// /admin/templates: the owner changes what new strategies start with (spec: Admin, Templates). A strategy already running
/// keeps the values it started with. Only the differences from the tested values are stored; none left removes the row.
/// </summary>
public static class AdminTemplates
{
    /// <summary>The range "getting close" can be set to on the page, in steps of <see cref="CloseStep"/>.</summary>
    public const decimal MinClosePct = 0.5m, MaxClosePct = 10, CloseStep = 0.5m;

    public static void MapTemplates(RouteGroupBuilder group)
    {
        var templates = group.MapGroup("/templates");

        templates.MapGet("/", async (AppDbContext db, TemplateStore store, CancellationToken cancellationToken) =>
        {
            var inUse = await store.AllAsync(cancellationToken);
            var assets = await Assets(db, inUse.SelectMany(t => t.Watches.AssetIds), cancellationToken);
            return inUse.Select(template => Dto(TemplateCatalog.Find(template.Key)!, template, assets)).ToList();
        })
        .WithSummary("Every template: the defaults, starter list or fund and \"getting close\" new strategies start with, next to the tested ones.");

        templates.MapPut("/{key}", async Task<Results<Ok<AdminTemplate>, NotFound, ValidationProblem>> (string key, TemplateChange change,
            AppDbContext db, CancellationToken cancellationToken) =>
        {
            if (TemplateCatalog.Find(key) is not { } tested) return TypedResults.NotFound();
            var errors = new Dictionary<string, string[]>();
            void Fail(string field, string message) => errors[field] = [message];

            TemplateSettings.Apply(tested, change.Defaults, Fail);
            var ids = change.AssetIds ?? tested.Watches.AssetIds;
            var found = await db.Assets.AsNoTracking().Where(a => ids.Contains(a.Id)).ToListAsync(cancellationToken);
            if (NewStrategyValidation.AssetsError(ids, tested, found, TemplateTexts.DefaultLocale) is { } invalid) Fail("assetIds", invalid);
            var closePct = change.ClosePct ?? tested.ClosePct;
            if (tested.Key == TemplateCatalog.Ai.Key ? closePct != tested.ClosePct
                : closePct < MinClosePct || closePct > MaxClosePct || closePct % CloseStep != 0)
                Fail("closePct", $"Use {MinClosePct} to {MaxClosePct}, in steps of {CloseStep}.");
            if (errors.Count > 0) return TypedResults.ValidationProblem(errors);

            // Only what differs from the tested values: a default retuned in code later still reaches the others.
            var testedValues = Values(tested);
            var defaults = (change.Defaults ?? new Dictionary<string, decimal>()).Where(d => d.Value != testedValues[d.Key])
                .ToDictionary(d => d.Key, d => d.Value);
            var row = new TemplateOverride
            {
                TemplateKey = tested.Key,
                Defaults = defaults.Count > 0 ? JsonSerializer.SerializeToDocument(defaults, JsonSerializerOptions.Web) : null,
                AssetIds = ids.ToHashSet().SetEquals(tested.Watches.AssetIds) ? null : [.. ids],
                ClosePct = closePct == tested.ClosePct ? null : closePct,
            };
            var saved = await db.TemplateOverrides.FindAsync([tested.Key], cancellationToken);
            if (row.Defaults is null && row.AssetIds is null && row.ClosePct is null)
            {
                if (saved is not null) db.TemplateOverrides.Remove(saved);
            }
            else if (saved is null)
            {
                db.TemplateOverrides.Add(row);
            }
            else
            {
                (saved.Defaults, saved.AssetIds, saved.ClosePct) = (row.Defaults, row.AssetIds, row.ClosePct);
            }
            await db.SaveChangesAsync(cancellationToken);

            var inUse = TemplateStore.Effective(tested, row);
            return TypedResults.Ok(Dto(tested, inUse, await Assets(db, inUse.Watches.AssetIds, cancellationToken)));
        })
        .WithSummary("Sets what new strategies of a template start with. Values equal to the tested ones aren't stored.");

        templates.MapDelete("/{key}", async Task<Results<NoContent, NotFound>> (string key, AppDbContext db, CancellationToken cancellationToken) =>
        {
            if (TemplateCatalog.Find(key) is null) return TypedResults.NotFound();
            await db.TemplateOverrides.Where(row => row.TemplateKey == key).ExecuteDeleteAsync(cancellationToken);
            return TypedResults.NoContent();
        })
        .WithSummary("Back to the tested values: new strategies of the template start with the code's defaults, list or fund and \"getting close\".");
    }

    static Task<Dictionary<Guid, Asset>> Assets(AppDbContext db, IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var wanted = ids.Distinct().ToList();
        return db.Assets.AsNoTracking().Where(a => wanted.Contains(a.Id)).ToDictionaryAsync(a => a.Id, cancellationToken);
    }

    /// <summary>The template's settings by key, from its params.</summary>
    static Dictionary<string, decimal> Values(Template template)
    {
        var root = template.ParamsJson().RootElement;
        return template.Settings.ToDictionary(s => s.Key, s => root.GetProperty(s.Key).GetDecimal());
    }

    static AdminTemplate Dto(Template tested, Template inUse, IReadOnlyDictionary<Guid, Asset> assets)
    {
        var texts = TemplateTexts.For(tested, TemplateTexts.DefaultLocale);
        var (testedValues, currentValues) = (Values(tested), Values(inUse));
        var settings = tested.Settings.Select(s =>
        {
            var words = texts.Settings![s.Key];
            return new AdminTemplateSetting(s.Key, words.Sentence, words.Value, s.Step, s.Min, s.Max, s.Below, testedValues[s.Key], currentValues[s.Key]);
        }).ToList();
        // An asset row gone from the database can't be shown; the page then lists what is left.
        var listed = inUse.Watches.AssetIds.Where(assets.ContainsKey).Select(id => assets[id])
            .Select(a => new AdminTemplateAsset(a.Id, a.Symbol, a.Name, a.Active))
            .OrderBy(a => a.Name, StringComparer.InvariantCultureIgnoreCase).ToList();
        var changed = settings.Any(s => s.Current != s.Tested) || !inUse.Watches.AssetIds.ToHashSet().SetEquals(tested.Watches.AssetIds)
            || inUse.ClosePct != tested.ClosePct;
        var ai = tested.Key == TemplateCatalog.Ai.Key;
        return new AdminTemplate(tested.Key, texts.Name, tested.InvestsIn, changed, settings, listed, tested.Watches.AssetIds,
            ai ? null : inUse.ClosePct, tested.ClosePct);
    }
}
