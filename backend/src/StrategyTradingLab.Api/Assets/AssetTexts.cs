using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Assets;

public static class AssetTexts
{
    /// <summary>What the asset does in <paramref name="locale"/>, else in English (loaded texts; a query spells it out).</summary>
    public static string? WhatIn(this Asset asset, string locale) =>
        asset.Texts.FirstOrDefault(t => t.Locale == locale)?.What ?? asset.Texts.FirstOrDefault(t => t.Locale == TemplateTexts.DefaultLocale)?.What;
}
