namespace StrategyTradingLab.Api.Engine;

/// <summary>One trading day of an asset, in its currency: what the rules read. Oldest first in every list.</summary>
public record PriceBar(DateOnly Date, decimal Open, decimal High, decimal Low, decimal Close, long Volume);
