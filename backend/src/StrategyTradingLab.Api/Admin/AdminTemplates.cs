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
/// while the owner has saved changes, even ones no longer in use (a default the code refuses since, an asset hidden since). <c>ClosePct</c> is null for the AI template, whose stocks have no buy rule to be close to.
/// </summary>
public record AdminTemplate(string Key, string Name, InvestsIn InvestsIn, bool Changed, IReadOnlyList<AdminTemplateSetting> Settings,
    IReadOnlyList<AdminTemplateAsset> Assets, decimal? ClosePct, decimal TestedClosePct);

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

        // The saved rows as they are, not TemplateStore's: a hidden asset stays on the page, marked, for the owner to take out.
        templates.MapGet("/", async (AppDbContext db, CancellationToken cancellationToken) =>
        {
            var rows = await db.TemplateOverrides.AsNoTracking().ToDictionaryAsync(row => row.TemplateKey, cancellationToken);
            var inUse = TemplateCatalog.All.Select(t => (Tested: t, Row: rows.GetValueOrDefault(t.Key))).ToList();
            var assets = await Assets(db, inUse.SelectMany(t => t.Row?.AssetIds ?? t.Tested.Watches.AssetIds), cancellationToken);
            return inUse.Select(t => Dto(t.Tested, t.Row, assets)).ToList();
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
            if (tested.Key == TemplateCatalog.Ai.Key && closePct != tested.ClosePct)
                Fail("closePct", "This template has no getting close: its stocks have no buy rule.");
            else if (closePct < MinClosePct || closePct > MaxClosePct || closePct % CloseStep != 0)
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
            var stored = row.Defaults is not null || row.AssetIds is not null || row.ClosePct is not null;
            if (!stored)
                await db.TemplateOverrides.Where(r => r.TemplateKey == tested.Key).ExecuteDeleteAsync(cancellationToken);
            else
                // One statement, so two first saves at once (a double click, two tabs) can't both insert.
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO template_overrides (template_key, defaults, asset_ids, close_pct)
                    VALUES ({row.TemplateKey}, {row.Defaults}, {row.AssetIds}, {row.ClosePct})
                    ON CONFLICT (template_key) DO UPDATE SET defaults = excluded.defaults, asset_ids = excluded.asset_ids,
                        close_pct = excluded.close_pct, updated_at = now()
                    """, cancellationToken);

            // The assets saved are the ones just checked.
            return TypedResults.Ok(Dto(tested, stored ? row : null, found.ToDictionary(a => a.Id)));
        })
        .WithSummary("Sets what new strategies of a template start with. Values equal to the tested ones aren't stored.");

        templates.MapDelete("/{key}", async Task<Results<Ok<AdminTemplate>, NotFound>> (string key, AppDbContext db, CancellationToken cancellationToken) =>
        {
            if (TemplateCatalog.Find(key) is not { } tested) return TypedResults.NotFound();
            await db.TemplateOverrides.Where(row => row.TemplateKey == key).ExecuteDeleteAsync(cancellationToken);
            // The tested template, so the page resets the card without loading every template again.
            return TypedResults.Ok(Dto(tested, null, await Assets(db, tested.Watches.AssetIds, cancellationToken)));
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

    static AdminTemplate Dto(Template tested, TemplateOverride? row, IReadOnlyDictionary<Guid, Asset> assets)
    {
        var inUse = TemplateStore.Effective(tested, row);
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
        var ai = tested.Key == TemplateCatalog.Ai.Key;
        return new AdminTemplate(tested.Key, texts.Name, tested.InvestsIn, row is not null, settings, listed,
            ai ? null : inUse.ClosePct, tested.ClosePct);
    }
}
