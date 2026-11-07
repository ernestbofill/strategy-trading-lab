using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Templates;

/// <summary>
/// The templates in use: the catalogue's, with the owner's changes from /admin/templates (the defaults of Advanced settings,
/// the starter list or fund, "getting close"). A new strategy copies these; a running one keeps what it started with. Read
/// once per request: the table has a row per changed template at most.
/// </summary>
public class TemplateStore(AppDbContext db)
{
    IReadOnlyList<Template>? templates;

    /// <summary>Every template, in catalogue order.</summary>
    public async Task<IReadOnlyList<Template>> AllAsync(CancellationToken cancellationToken)
    {
        if (templates is not null) return templates;
        var rows = await db.TemplateOverrides.AsNoTracking().ToDictionaryAsync(row => row.TemplateKey, cancellationToken);
        // A saved stock or fund hidden since (on /admin/assets) can't be picked, so it isn't offered either.
        var saved = rows.Values.SelectMany(row => row.AssetIds ?? []).Distinct().ToList();
        var active = saved.Count == 0 ? [] : (await db.Assets.AsNoTracking().Where(a => saved.Contains(a.Id) && a.Active)
            .Select(a => a.Id).ToListAsync(cancellationToken)).ToHashSet();
        return templates = [.. TemplateCatalog.All.Select(template => Effective(template, rows.GetValueOrDefault(template.Key), active.Contains))];
    }

    public async Task<Template?> FindAsync(string key, CancellationToken cancellationToken) =>
        (await AllAsync(cancellationToken)).FirstOrDefault(template => template.Key == key);

    /// <summary>
    /// The template with the owner's changes. A saved default the code no longer accepts (a range narrowed since) leaves all
    /// the defaults as tested, so a new strategy never starts from values the API would refuse; the admin page still shows the
    /// template as changed. Saved assets that aren't <paramref name="active"/> any more are left out, and with none left the
    /// list or fund is the tested one.
    /// </summary>
    public static Template Effective(Template template, TemplateOverride? row, Func<Guid, bool>? active = null)
    {
        if (row is null) return template;
        var effective = template;
        if (row.Defaults?.Deserialize<Dictionary<string, decimal>>(JsonSerializerOptions.Web) is { Count: > 0 } defaults
            && TemplateSettings.Apply(template, defaults, (_, _) => { }) is { } json)
            effective = effective with { Params = json.Deserialize(template.Params.GetType(), JsonSerializerOptions.Web)! };
        if (row.AssetIds?.Where(active ?? (_ => true)).ToList() is { Count: > 0 } ids)
            effective = effective with { Watches = template.Watches is DefaultFund ? new DefaultFund(ids[0]) : new StarterList(ids) };
        if (row.ClosePct is { } close) effective = effective with { ClosePct = close };
        return effective;
    }
}
