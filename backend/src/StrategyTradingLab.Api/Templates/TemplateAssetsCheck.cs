using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Templates;

/// <summary>
/// Stops the API from starting when a template points at an asset the database doesn't have
/// (a migration not applied), or at the wrong kind: a fund template needs a fund, a stock template stocks.
/// </summary>
public class TemplateAssetsCheck(IServiceScopeFactory scopes) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ids = TemplateCatalog.All.SelectMany(template => template.Watches.Assets).Select(asset => asset.Id).Distinct().ToList();
        var kinds = await db.Assets.AsNoTracking()
            .Where(asset => asset.Active && ids.Contains(asset.Id))
            .Select(asset => new { asset.Id, asset.Kind })
            .ToDictionaryAsync(asset => asset.Id, asset => asset.Kind, cancellationToken);

        var problems = TemplateCatalog.All.SelectMany(template => Problems(template, kinds)).ToList();
        if (problems.Count > 0)
            throw new InvalidOperationException(
                $"Template assets don't match the database (are the migrations applied?): {string.Join("; ", problems)}");
    }

    public static IEnumerable<string> Problems(Template template, IReadOnlyDictionary<Guid, AssetKind> activeKinds)
    {
        var expected = template.Watches is DefaultFund ? AssetKind.Fund : AssetKind.Stock;
        foreach (var asset in template.Watches.Assets)
        {
            if (!activeKinds.TryGetValue(asset.Id, out var kind)) yield return $"{template.Key}: {asset.Symbol} is missing or inactive";
            else if (kind != expected) yield return $"{template.Key}: {asset.Symbol} is a {kind}, not a {expected}";
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
