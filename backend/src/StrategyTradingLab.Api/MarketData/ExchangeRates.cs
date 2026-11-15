using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Signals;

namespace StrategyTradingLab.Api.MarketData;

/// <summary>The euro against each currency by day, and the rate between any two through it.</summary>
public class ExchangeRates(Dictionary<string, List<(DateOnly Date, decimal Rate)>> byQuote)
{
    public const string Base = "EUR";

    /// <summary>How far before a range prices and rates are loaded, so its first days still find the last trading day before them.</summary>
    public const int LookBackDays = 7;

    /// <summary>The stored rates from <see cref="LookBackDays"/> before <paramref name="from"/> on (all of them when null): a rate older than that isn't used.</summary>
    public static async Task<ExchangeRates> LoadAsync(AppDbContext db, DateOnly? from, CancellationToken cancellationToken)
    {
        var since = from?.AddDays(-LookBackDays) ?? DateOnly.MinValue;
        var rows = await db.FxRates.AsNoTracking().Where(r => r.Base == Base && r.Date >= since).OrderBy(r => r.Date)
            .Select(r => new { r.Quote, r.Date, r.Rate }).ToListAsync(cancellationToken);
        return new ExchangeRates(rows.GroupBy(r => r.Quote).ToDictionary(g => g.Key, g => g.Select(r => (r.Date, r.Rate)).ToList()));
    }

    /// <summary>Units of <paramref name="quote"/> per euro on the latest day on or before <paramref name="date"/>; null when unknown.</summary>
    decimal? PerEuro(string currency, DateOnly date)
    {
        if (currency == Base) return 1;
        if (!byQuote.TryGetValue(currency, out var days)) return null;
        var at = days.FindLastIndex(d => d.Date <= date);
        return at < 0 ? null : days[at].Rate;
    }

    /// <summary>Whether <paramref name="currency"/> has a rate stored for that very day (the euro always has).</summary>
    public bool StoredOn(string currency, DateOnly date) =>
        currency == Base || (byQuote.TryGetValue(currency, out var days) && days.Exists(d => d.Date == date));

    /// <summary>
    /// Rates for a pair that needs one: none to load when the currencies match (<see cref="Between"/> answers 1 then).
    /// </summary>
    public static async Task<ExchangeRates> ForPairAsync(AppDbContext db, string strategyCurrency, string assetCurrency, DateOnly? from,
        CancellationToken cancellationToken) =>
        strategyCurrency == assetCurrency ? new ExchangeRates([]) : await LoadAsync(db, from, cancellationToken);

    /// <summary>
    /// The days a trade form offers a reference rate, from the look-back before <paramref name="from"/> to <paramref name="to"/>:
    /// weekdays with both rates stored, since another day (a weekend, a holiday, today before the rates are in) would repeat an
    /// earlier rate under its own date. The form picks the latest on or before its date, as the server does.
    /// </summary>
    public IReadOnlyList<RateDay> ReferenceDays(string strategyCurrency, string assetCurrency, DateOnly from, DateOnly to)
    {
        var days = new List<RateDay>();
        for (var day = from.AddDays(-LookBackDays); day <= to; day = day.AddDays(1))
            if (StoredOn(strategyCurrency, day) && StoredOn(assetCurrency, day)
                && day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && Between(strategyCurrency, assetCurrency, day) is { } rate)
                days.Add(new RateDay(day, rate));
        return days;
    }

    /// <summary>Units of <paramref name="assetCurrency"/> per one unit of <paramref name="strategyCurrency"/> on a day (USD per EUR, a trade's fx rate).</summary>
    public decimal? Between(string strategyCurrency, string assetCurrency, DateOnly date)
    {
        if (strategyCurrency == assetCurrency) return 1;
        return PerEuro(assetCurrency, date) is { } asset && PerEuro(strategyCurrency, date) is { } strategy ? asset / strategy : null;
    }
}
