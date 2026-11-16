using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Strategies;

/// <summary>
/// Whether a strategy still runs its template as tested, for the "tuned" mark next to the past results. It compares with the
/// template version the strategy started from (it is passed that version), so it reads as tuned only when the user changed
/// something, whatever the owner made active since on /admin/templates.
/// </summary>
public static class TemplateCustomization
{
    /// <summary>The template's tested results, marked tuned when the strategy's settings or list differ from it.</summary>
    public static PastResult? PastResultOf(Strategy strategy, Template template, TemplateTexts texts, IEnumerable<Guid>? activeAssetIds = null) =>
        template.Backtest?.LastOrDefault() is { TemplatePerYear: { } tmpl, IndexPerYear: { } idx, WorstYear: { } worst, IndexWorstYear: { } idxWorst } && texts.Backtest is { } words
            ? new PastResult(words.Years, tmpl, idx, worst, idxWorst, IsCustomized(strategy, template, activeAssetIds))
            : null;

    /// <summary>A setting changed from the template's, or a stock or fund the template's list doesn't have.</summary>
    public static bool IsCustomized(Strategy strategy, Template template, IEnumerable<Guid>? activeAssetIds = null)
    {
        if (template.Settings.Count > 0)
        {
            var keys = template.Settings.Select(s => s.Key).ToList();
            using var tested = template.ParamsJson();
            var defaults = tested.RootElement.Numbers(keys);
            if (strategy.Params.RootElement.Numbers(keys).Any(own => defaults.TryGetValue(own.Key, out var value) && value != own.Value))
                return true;
        }

        var activeIds = activeAssetIds ?? (strategy.Assets is null ? [] : strategy.Assets.Where(a => a.IsFollowed).Select(a => a.AssetId));
        return template.Watches switch
        {
            StarterList starter => activeIds.Any(id => !starter.StockIds.Contains(id)),
            DefaultFund defaultFund => activeIds.Any(id => id != defaultFund.FundId),
            _ => false,
        };
    }
}
