using StrategyTradingLab.Api.Evaluation;

namespace StrategyTradingLab.Api.Strategies;

/// <summary>A buy in a strategy's results: the shares bought, and the amount and fee paid, in the strategy's currency.</summary>
public record Buy(Guid AssetId, DateOnly Date, decimal Shares, decimal Amount, decimal Fee)
{
    public decimal Cost => Amount + Fee;
}

/// <summary>One trading day of an asset, as far as results need it.</summary>
public readonly record struct DayPrice(DateOnly Date, decimal Open, decimal Close);

/// <summary>Assets' daily prices, each in its own currency, and the exchange rates to value them in a strategy's currency.</summary>
public sealed class PriceBook(IReadOnlyDictionary<Guid, (string Currency, IReadOnlyList<DayPrice> Days)> assets, ExchangeRates rates)
{
    /// <summary>The trading days of any of <paramref name="assetIds"/> from <paramref name="from"/> to <paramref name="to"/>, in order.</summary>
    public IReadOnlyList<DateOnly> Days(IEnumerable<Guid> assetIds, DateOnly from, DateOnly to) =>
        [.. assetIds.Distinct().Where(assets.ContainsKey).SelectMany(id => assets[id].Days).Select(d => d.Date)
            .Where(d => d >= from && d <= to).Distinct().Order()];

    /// <summary>What <paramref name="shares"/> are worth at the last close on or before <paramref name="date"/>; null when the price or rate is unknown.</summary>
    public decimal? Value(Guid assetId, decimal shares, DateOnly date, string currency)
    {
        if (!assets.TryGetValue(assetId, out var asset)) return null;
        var at = LastOnOrBefore(asset.Days, date);
        if (at < 0 || rates.Between(currency, asset.Currency, date) is not { } fx || fx == 0) return null;
        return shares * asset.Days[at].Close / fx;
    }

    /// <summary>
    /// The price a buy on <paramref name="date"/> gets: the opening price of the first trading day on or after it, or, when
    /// that day isn't stored yet (a trade dated today), the last close before it. With the rate of that day; null when the
    /// asset has no price or rate at all.
    /// </summary>
    public (DateOnly Date, decimal Price, decimal Fx)? BuyPrice(Guid assetId, DateOnly date, string currency)
    {
        if (!assets.TryGetValue(assetId, out var asset)) return null;
        var before = LastOnOrBefore(asset.Days, date.AddDays(-1));
        var (day, price) = before + 1 < asset.Days.Count ? (asset.Days[before + 1].Date, asset.Days[before + 1].Open)
            : before >= 0 ? (asset.Days[before].Date, asset.Days[before].Close)
            : (default, 0m);
        if (price <= 0) return null;
        return rates.Between(currency, asset.Currency, day) is { } fx ? (day, price, fx) : null;
    }

    /// <summary>The index of the last day on or before <paramref name="date"/>, -1 when there is none.</summary>
    static int LastOnOrBefore(IReadOnlyList<DayPrice> days, DateOnly date)
    {
        int low = 0, high = days.Count - 1, found = -1;
        while (low <= high)
        {
            var middle = (low + high) / 2;
            if (days[middle].Date <= date) { found = middle; low = middle + 1; }
            else high = middle - 1;
        }
        return found;
    }
}

/// <summary>A point of the results chart: each line's gain in percent on that day.</summary>
public record ResultPoint(DateOnly Date, decimal You, decimal? Rules, decimal? Index);

/// <summary>
/// The results maths of the strategy page (spec: Strategy page, Metrics). Buys only: sales come with <i>Ride the long
/// trend</i>, which the engine doesn't run yet. Pure: buys and prices in, figures out.
/// </summary>
public static class Performance
{
    /// <summary>The most points a chart gets; a longer history is thinned, keeping the first and last day.</summary>
    public const int MaxPoints = 120;

    /// <summary>
    /// The gain or loss of the buys made by <paramref name="date"/>, in percent of what they cost (fees included), valued at
    /// that day's close. A buy whose price or exchange rate is unknown is left out, cost and all. Null before the first buy,
    /// or when no buy can be valued.
    /// </summary>
    public static decimal? GainPct(IEnumerable<Buy> buys, PriceBook prices, DateOnly date, string currency)
    {
        decimal cost = 0, value = 0;
        foreach (var buy in buys.Where(b => b.Date <= date))
        {
            if (prices.Value(buy.AssetId, buy.Shares, date, currency) is not { } worth) continue;
            cost += buy.Cost;
            value += worth;
        }
        return cost == 0 ? null : Math.Round((value - cost) / cost * 100, 1);
    }

    /// <summary>
    /// The same money put into the index fund on the same days (spec: Versus the index): each buy's cost, fee included,
    /// buys the fund at its opening price that day, or the next day it trades (the last close while that day isn't stored
    /// yet). Null when the fund has no prices or rates at all.
    /// </summary>
    public static IReadOnlyList<Buy>? IndexBuys(IEnumerable<Buy> buys, PriceBook prices, Guid indexId, string currency)
    {
        var result = new List<Buy>();
        foreach (var buy in buys)
        {
            if (prices.BuyPrice(indexId, buy.Date, currency) is not { } day) return null;
            // Counted from the day it is bought, and never before the buy it mirrors.
            result.Add(new Buy(indexId, day.Date > buy.Date ? day.Date : buy.Date, buy.Cost * day.Fx / day.Price, buy.Cost, 0));
        }
        return result;
    }

    /// <summary>
    /// Each line's gain on the trading days from the first buy of <paramref name="you"/> to <paramref name="today"/>,
    /// thinned to <see cref="MaxPoints"/>. A day whose own line can't be valued is left out.
    /// </summary>
    public static IReadOnlyList<ResultPoint> History(IReadOnlyList<Buy> you, IReadOnlyList<Buy>? rules, IReadOnlyList<Buy>? index,
        PriceBook prices, DateOnly today, string currency)
    {
        if (you.Count == 0) return [];
        var points = new List<ResultPoint>();
        // Thinned first, so only the days kept are valued.
        // The days of this strategy's own assets and the fund only: a price book can hold other strategies' too.
        var held = you.Concat(rules ?? []).Concat(index ?? []).Select(b => b.AssetId);
        foreach (var day in Thin(prices.Days(held, you.Min(b => b.Date), today), MaxPoints))
        {
            if (GainPct(you, prices, day, currency) is not { } gain) continue;
            points.Add(new ResultPoint(day, gain,
                rules is null ? null : GainPct(rules, prices, day, currency),
                index is null ? null : GainPct(index, prices, day, currency)));
        }
        return points;
    }

    /// <summary>At most <paramref name="max"/> evenly spread items, always with the first and the last.</summary>
    public static IReadOnlyList<T> Thin<T>(IReadOnlyList<T> items, int max)
    {
        if (items.Count <= max) return items;
        return [.. Enumerable.Range(0, max).Select(i => items[(int)Math.Round(i * (items.Count - 1) / (double)(max - 1))])];
    }
}
