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
        return templates = [.. TemplateCatalog.All.Select(template => Effective(template, rows.GetValueOrDefault(template.Key)))];
    }

    public async Task<Template?> FindAsync(string key, CancellationToken cancellationToken) =>
        (await AllAsync(cancellationToken)).FirstOrDefault(template => template.Key == key);

    /// <summary>
    /// The template with the owner's changes. A saved default the code no longer accepts (a range narrowed since) leaves all
    /// the defaults as tested, so a new strategy never starts from values the API would refuse.
    /// </summary>
    public static Template Effective(Template template, TemplateOverride? row)
    {
        if (row is null) return template;
        var effective = template;
        if (row.Defaults?.Deserialize<Dictionary<string, decimal>>(JsonSerializerOptions.Web) is { Count: > 0 } defaults
            && TemplateSettings.Apply(template, defaults, (_, _) => { }) is { } json)
            effective = effective with { Params = json.Deserialize(template.Params.GetType(), JsonSerializerOptions.Web)! };
        if (row.AssetIds is { Count: > 0 } ids)
            effective = effective with { Watches = template.Watches is DefaultFund ? new DefaultFund(ids[0]) : new StarterList(ids) };
        if (row.ClosePct is { } close) effective = effective with { ClosePct = close };
        return effective;
    }
}
