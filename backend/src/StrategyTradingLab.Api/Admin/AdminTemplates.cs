using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Strategies;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Admin;

/// <summary>
/// A template on /admin/templates: its settings' words and ranges, and every version, newest first. <c>HasClosePct</c> is
/// false for the AI template, whose stocks have no buy rule to be close to.
/// </summary>
public record AdminTemplate(string Key, string Name, InvestsIn InvestsIn, bool HasClosePct, IReadOnlyList<AdminTemplateSetting> Settings,
    IReadOnlyList<AdminTemplateVersion> Versions);

/// <summary>A setting of Advanced settings, with its words (as the screen's) and range.</summary>
public record AdminTemplateSetting(string Key, string Sentence, string Value, decimal Step, decimal Min, decimal Max, string? Below);

/// <summary>
/// A version: every setting's default, the starter list or the fund (sorted by name), "getting close", when it was made and how
/// many strategies started from it. Version 1 is the catalogue's tested values.
/// </summary>
public record AdminTemplateVersion(int Version, bool Active, DateTimeOffset CreatedAt, IReadOnlyDictionary<string, decimal> Defaults,
    IReadOnlyList<AdminTemplateAsset> Assets, decimal ClosePct, int Strategies);

/// <summary>A stock of the starter list, or the fund. A hidden one stays listed, so the owner sees it and takes it out.</summary>
public record AdminTemplateAsset(Guid Id, string Symbol, string Name, bool Active);

/// <summary>A new version: every setting's default, the starter list or the one fund, and "getting close".</summary>
public record TemplateChange(IReadOnlyDictionary<string, decimal>? Defaults, IReadOnlyList<Guid>? AssetIds, decimal? ClosePct);

/// <summary>The version to make active.</summary>
public record ActiveVersion(int Version);

/// <summary>
/// /admin/templates: the owner makes new versions of what new strategies start with, and picks which version is active (spec:
/// Admin, Templates). A strategy keeps the version it started from; versions are never edited or deleted.
/// </summary>
public static class AdminTemplates
{
    /// <summary>The range "getting close" can be set to on the page, in steps of <see cref="CloseStep"/>.</summary>
    public const decimal MinClosePct = 0.5m, MaxClosePct = 10, CloseStep = 0.5m;

    public static void MapTemplates(RouteGroupBuilder group)
    {
        var templates = group.MapGroup("/templates");

        templates.MapGet("/", async (AppDbContext db, CancellationToken cancellationToken) =>
        {
            var view = await View.LoadAsync(db, null, cancellationToken);
            return TemplateCatalog.All.Select(view.Dto).ToList();
        })
        .WithSummary("Every template with its versions, newest first, and which one new strategies start with.");

        templates.MapPost("/{key}/versions", async Task<Results<Ok<AdminTemplate>, NotFound, ValidationProblem>> (string key, TemplateChange change,
            AppDbContext db, CancellationToken cancellationToken) =>
        {
            if (TemplateCatalog.Find(key) is not { } tested) return TypedResults.NotFound();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await Lock(db, key, cancellationToken);
            var versions = await db.TemplateVersions.AsNoTracking().Where(v => v.TemplateKey == key).ToListAsync(cancellationToken);
            var active = versions.Single(v => v.Active);

            var errors = new Dictionary<string, string[]>();
            void Fail(string field, string message) => errors[field] = [message];
            // Every setting, so a version stays what it was even when the catalogue's defaults change later. Checked as a whole:
            // a change is checked with the active version's other values, which it is stored with.
            var defaults = Values(tested, active);
            foreach (var (setting, value) in change.Defaults ?? new Dictionary<string, decimal>()) defaults[setting] = value;
            TemplateSettings.Apply(tested, defaults, Fail);
            var ids = change.AssetIds ?? active.AssetIds;
            var found = await db.Assets.AsNoTracking().Where(a => ids.Contains(a.Id)).ToListAsync(cancellationToken);
            // An asset of the active version may have been hidden since: it can stay (new strategies don't get it), so saving
            // something else isn't refused because of it.
            if (NewStrategyValidation.AssetsError(ids, tested, found, TemplateTexts.DefaultLocale, active.AssetIds) is { } invalid) Fail("assetIds", invalid);
            var closePct = change.ClosePct ?? active.ClosePct;
            if (tested.Key == TemplateCatalog.Ai.Key && closePct != active.ClosePct)
                Fail("closePct", "This template has no getting close: its stocks have no buy rule.");
            else if (closePct < MinClosePct || closePct > MaxClosePct || closePct % CloseStep != 0)
                Fail("closePct", $"Use {MinClosePct} to {MaxClosePct}, in steps of {CloseStep}.");
            if (errors.Count > 0) return TypedResults.ValidationProblem(errors);

            var same = Values(tested, active).All(d => defaults[d.Key] == d.Value)
                && ids.ToHashSet().SetEquals(active.AssetIds) && closePct == active.ClosePct;
            if (!same)
            {
                await db.TemplateVersions.Where(v => v.TemplateKey == key && v.Active)
                    .ExecuteUpdateAsync(s => s.SetProperty(v => v.Active, false), cancellationToken);
                db.TemplateVersions.Add(new TemplateVersion
                {
                    TemplateKey = key,
                    Version = versions.Max(v => v.Version) + 1,
                    Defaults = JsonSerializer.SerializeToDocument(defaults, JsonSerializerOptions.Web),
                    AssetIds = [.. ids],
                    ClosePct = closePct,
                    Active = true,
                });
                await db.SaveChangesAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            return TypedResults.Ok((await View.LoadAsync(db, key, cancellationToken)).Dto(tested));
        })
        .WithSummary("Saves a new version and makes it active. Nothing new when it equals the active version.");

        templates.MapPut("/{key}/active", async Task<Results<Ok<AdminTemplate>, NotFound>> (string key, ActiveVersion request,
            AppDbContext db, CancellationToken cancellationToken) =>
        {
            if (TemplateCatalog.Find(key) is not { } tested) return TypedResults.NotFound();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await Lock(db, key, cancellationToken);
            if (!await db.TemplateVersions.AnyAsync(v => v.TemplateKey == key && v.Version == request.Version, cancellationToken))
                return TypedResults.NotFound();
            // Off first, then on: the unique index allows one active version per template at any moment.
            await db.TemplateVersions.Where(v => v.TemplateKey == key && v.Active && v.Version != request.Version)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.Active, false), cancellationToken);
            await db.TemplateVersions.Where(v => v.TemplateKey == key && v.Version == request.Version)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.Active, true), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return TypedResults.Ok((await View.LoadAsync(db, key, cancellationToken)).Dto(tested));
        })
        .WithSummary("Makes a version active: new strategies of the template start with it.");
    }

    /// <summary>
    /// Holds the template's versions for the transaction: saves and activations of one template go one after the other, each
    /// seeing what the one before it committed (a row lock would leave the second with the rows it read before waiting).
    /// </summary>
    static Task Lock(AppDbContext db, string key, CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({"template_versions:" + key}))", cancellationToken);

    /// <summary>The catalogue's settings by key, with <paramref name="version"/>'s saved values over them.</summary>
    static Dictionary<string, decimal> Values(Template template, TemplateVersion version)
    {
        var root = template.ParamsJson().RootElement;
        var values = template.Settings.ToDictionary(s => s.Key, s => root.GetProperty(s.Key).GetDecimal());
        foreach (var (key, value) in version.Defaults.Deserialize<Dictionary<string, decimal>>(JsonSerializerOptions.Web) ?? [])
            if (values.ContainsKey(key)) values[key] = value;
        return values;
    }

    /// <summary>The versions of every template (or one), their assets and how many strategies started from each.</summary>
    sealed record View(List<TemplateVersion> Versions, Dictionary<Guid, Asset> Assets, Dictionary<(string, int), int> Strategies)
    {
        public static async Task<View> LoadAsync(AppDbContext db, string? key, CancellationToken cancellationToken)
        {
            var versions = await db.TemplateVersions.AsNoTracking().Where(v => key == null || v.TemplateKey == key).ToListAsync(cancellationToken);
            var ids = versions.SelectMany(v => v.AssetIds).Distinct().ToList();
            var assets = await db.Assets.AsNoTracking().Where(a => ids.Contains(a.Id)).ToDictionaryAsync(a => a.Id, cancellationToken);
            var strategies = await db.Strategies.AsNoTracking().Where(s => key == null || s.TemplateKey == key)
                .GroupBy(s => new { s.TemplateKey, s.DefaultsVersion }).Select(g => new { g.Key.TemplateKey, g.Key.DefaultsVersion, Count = g.Count() })
                .ToDictionaryAsync(g => (g.TemplateKey, g.DefaultsVersion), g => g.Count, cancellationToken);
            return new View(versions, assets, strategies);
        }

        public AdminTemplate Dto(Template tested)
        {
            var texts = TemplateTexts.For(tested, TemplateTexts.DefaultLocale);
            var settings = tested.Settings.Select(s =>
            {
                var words = texts.Settings![s.Key];
                return new AdminTemplateSetting(s.Key, words.Sentence, words.Value, s.Step, s.Min, s.Max, s.Below);
            }).ToList();
            var versions = Versions.Where(v => v.TemplateKey == tested.Key).OrderByDescending(v => v.Version).Select(v =>
                new AdminTemplateVersion(v.Version, v.Active, v.CreatedAt, Values(tested, v),
                    // An asset row gone from the database can't be shown; the page then lists what is left.
                    [.. v.AssetIds.Where(Assets.ContainsKey).Select(id => Assets[id])
                        .Select(a => new AdminTemplateAsset(a.Id, a.Symbol, a.Name, a.Active))
                        .OrderBy(a => a.Name, StringComparer.InvariantCultureIgnoreCase)],
                    v.ClosePct, Strategies.GetValueOrDefault((tested.Key, v.Version)))).ToList();
            return new AdminTemplate(tested.Key, texts.Name, tested.InvestsIn, tested.Key != TemplateCatalog.Ai.Key, settings, versions);
        }
    }
}
