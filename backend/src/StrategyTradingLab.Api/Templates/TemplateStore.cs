using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Templates;

/// <summary>
/// The templates as versioned on /admin/templates: each version is the catalogue's template with that version's defaults of
/// Advanced settings, starter list or fund and "getting close". New strategies take the active version; a strategy keeps the
/// number of the one it started from. Every version is read once per request: a few rows per template.
/// </summary>
public class TemplateStore(AppDbContext db)
{
    List<TemplateVersion>? rows;
    HashSet<Guid>? activeAssets;

    async Task<List<TemplateVersion>> Rows(CancellationToken cancellationToken)
    {
        if (rows is not null) return rows;
        var loaded = await db.TemplateVersions.AsNoTracking().ToListAsync(cancellationToken);
        // A stock or fund hidden since (on /admin/assets) can't be picked, so the active versions don't offer it.
        var saved = loaded.Where(r => r.Active).SelectMany(r => r.AssetIds).Distinct().ToList();
        activeAssets = saved.Count == 0 ? [] : (await db.Assets.AsNoTracking().Where(a => saved.Contains(a.Id) && a.Active)
            .Select(a => a.Id).ToListAsync(cancellationToken)).ToHashSet();
        return rows = loaded;
    }

    /// <summary>Every template as its active version, in catalogue order.</summary>
    public async Task<IReadOnlyList<Template>> AllAsync(CancellationToken cancellationToken)
    {
        var all = await Rows(cancellationToken);
        return [.. TemplateCatalog.All.Select(template =>
            Effective(template, all.FirstOrDefault(r => r.TemplateKey == template.Key && r.Active), activeAssets!.Contains))];
    }

    /// <summary>The template as its active version: what a new strategy starts with.</summary>
    public async Task<Template?> FindAsync(string key, CancellationToken cancellationToken) =>
        (await AllAsync(cancellationToken)).FirstOrDefault(template => template.Key == key);

    /// <summary>The template as one of its versions (a strategy's own, for "tuned"); the catalogue's when that version isn't stored.</summary>
    public async Task<Template> VersionAsync(string key, int version, CancellationToken cancellationToken)
    {
        var all = await Rows(cancellationToken);
        return Effective(TemplateCatalog.Find(key)!, all.FirstOrDefault(r => r.TemplateKey == key && r.Version == version));
    }

    /// <summary>
    /// The template as <paramref name="row"/> has it. A saved default the code no longer accepts (a range narrowed since) leaves
    /// all the defaults as the catalogue's, so a new strategy never starts from values the API would refuse. Assets that aren't
    /// <paramref name="available"/> any more are left out, and with none left the list or fund is the catalogue's.
    /// </summary>
    public static Template Effective(Template template, TemplateVersion? row, Func<Guid, bool>? available = null)
    {
        if (row is null) return template;
        var effective = template with { DefaultsVersion = row.Version, ClosePct = row.ClosePct };
        if (row.Defaults.Deserialize<Dictionary<string, decimal>>(JsonSerializerOptions.Web) is { Count: > 0 } defaults
            && TemplateSettings.Apply(template, defaults, (_, _) => { }) is { } json)
            effective = effective with { Params = json.Deserialize(template.Params.GetType(), JsonSerializerOptions.Web)! };
        if (row.AssetIds.Where(available ?? (_ => true)).ToList() is { Count: > 0 } ids)
            effective = effective with { Watches = template.Watches is DefaultFund ? new DefaultFund(ids[0]) : new StarterList(ids) };
        return effective;
    }
}
