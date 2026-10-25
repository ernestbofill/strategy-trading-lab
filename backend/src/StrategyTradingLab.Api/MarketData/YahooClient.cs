using System.Globalization;
using System.Text.Json;

namespace StrategyTradingLab.Api.MarketData;

/// <summary>One trading day. Prices are in the asset's currency; a stock's are adjusted for splits and dividends.</summary>
public record Candle(DateOnly Date, decimal Open, decimal High, decimal Low, decimal Close, long Volume);

/// <summary>One quote from Yahoo Finance's search endpoint.</summary>
public record MarketQuote(string Symbol, string Exchange, string Name, string Currency, string? QuoteType, string? Sector, string? Industry);

/// <summary>Where daily candles come from, so the refresh can be tested without the network.</summary>
public interface IMarketDataSource
{
    /// <summary>The last month, or the whole history. Oldest first; days without prices are left out.</summary>
    Task<IReadOnlyList<Candle>> DailyAsync(string symbol, bool fullHistory, bool adjusted, CancellationToken cancellationToken);

    /// <summary>The best matches for a symbol, company name or keyword from Yahoo Finance's search endpoint.</summary>
    Task<IReadOnlyList<MarketQuote>> SearchAsync(string query, CancellationToken cancellationToken);
}

public class MarketDataException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Daily candles from Yahoo's chart endpoint, the POC's source (yfinance reads the same one). It is unofficial, so a
/// failure is an expected thing: the refresh logs it and keeps what it has (ROADMAP → Open decisions: price provider).
/// </summary>
public class YahooClient(HttpClient http) : IMarketDataSource
{
    public const string BaseAddress = "https://query1.finance.yahoo.com/";

    public async Task<IReadOnlyList<Candle>> DailyAsync(string symbol, bool fullHistory, bool adjusted, CancellationToken cancellationToken)
    {
        var range = fullHistory ? "max" : "1mo";
        string json;
        try
        {
            using var response = await http.GetAsync($"v8/finance/chart/{Uri.EscapeDataString(symbol)}?range={range}&interval=1d&events=div%2Csplits", cancellationToken);
            if (!response.IsSuccessStatusCode) throw new MarketDataException($"Yahoo answered HTTP {(int)response.StatusCode} for {symbol}.");
            json = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception error) when (error is HttpRequestException || (error is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // A timeout is a TaskCanceledException that isn't ours: it's a failure of this symbol, not a shutdown.
            throw new MarketDataException($"Couldn't reach Yahoo for {symbol}.", error);
        }
        return Parse(json, symbol, adjusted);
    }

    public async Task<IReadOnlyList<MarketQuote>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var term = query.Trim();
        if (term.Length == 0) return [];
        string json;
        try
        {
            using var response = await http.GetAsync($"v1/finance/search?q={Uri.EscapeDataString(term)}&quotesCount=10&newsCount=0&enableFuzzyQuery=false", cancellationToken);
            if (!response.IsSuccessStatusCode) throw new MarketDataException($"Yahoo answered HTTP {(int)response.StatusCode} for {term}.");
            json = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception error) when (error is HttpRequestException || (error is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw new MarketDataException($"Couldn't reach Yahoo for {term}.", error);
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var quotes = document.RootElement.TryGetProperty("quotes", out var list) && list.ValueKind == JsonValueKind.Array ? list : default;
            var matches = new List<MarketQuote>();
            foreach (var item in quotes.EnumerateArray())
            {
                var symbol = item.TryGetProperty("symbol", out var symbolElement) ? symbolElement.GetString() : null;
                var exchange = item.TryGetProperty("exchange", out var exchangeElement) ? exchangeElement.GetString() : null;
                var name = item.TryGetProperty("longName", out var longName) && longName.ValueKind == JsonValueKind.String ? longName.GetString()
                    : item.TryGetProperty("shortName", out var shortName) && shortName.ValueKind == JsonValueKind.String ? shortName.GetString()
                    : null;
                var currency = item.TryGetProperty("currency", out var currencyElement) ? currencyElement.GetString() : null;
                var quoteType = item.TryGetProperty("quoteType", out var typeElement) ? typeElement.GetString() : null;
                var sector = item.TryGetProperty("sector", out var sectorElement) ? sectorElement.GetString() : null;
                var industry = item.TryGetProperty("industry", out var industryElement) ? industryElement.GetString() : null;
                if (string.IsNullOrWhiteSpace(symbol) || string.IsNullOrWhiteSpace(exchange) || string.IsNullOrWhiteSpace(name)) continue;
                matches.Add(new MarketQuote(symbol, exchange, name, currency ?? "USD", quoteType, sector, industry));
            }
            return matches;
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException)
        {
            throw new MarketDataException($"Yahoo's search answer for {term} wasn't what we expected.", error);
        }
    }

    /// <summary>
    /// Reads Yahoo's chart answer. With <paramref name="adjusted"/> the open, high and low are scaled by adjclose ÷ close,
    /// as yfinance's auto_adjust does. A day's date is the one in the exchange's time zone (<c>gmtoffset</c>).
    /// </summary>
    public static IReadOnlyList<Candle> Parse(string json, string symbol, bool adjusted)
    {
        try
        {
            return ParseChart(json, symbol, adjusted);
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException
            or IndexOutOfRangeException or FormatException or OverflowException)
        {
            throw new MarketDataException($"Yahoo's answer for {symbol} wasn't what we expected.", error);
        }
    }

    static IReadOnlyList<Candle> ParseChart(string json, string symbol, bool adjusted)
    {
        using var document = JsonDocument.Parse(json);
        var chart = document.RootElement.GetProperty("chart");
        if (chart.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
            throw new MarketDataException($"Yahoo has no data for {symbol}: {error.GetProperty("description").GetString()}");
        var result = chart.GetProperty("result")[0];

        if (!result.TryGetProperty("timestamp", out var timestamps)) return []; // A symbol with no days in the range.
        var meta = result.GetProperty("meta");
        var offset = meta.TryGetProperty("gmtoffset", out var gmt) ? gmt.GetInt32() : 0;
        // A bar's date is the day in the exchange's zone at that bar's time: the zone's offset changes with the clocks, while
        // gmtoffset is only today's, which would date a summer-time bar a day early after the clocks go back.
        var zone = meta.TryGetProperty("exchangeTimezoneName", out var zoneName) && zoneName.GetString() is { } id
            && TimeZoneInfo.TryFindSystemTimeZoneById(id, out var found) ? found : null;
        // London stocks are quoted in pence (GBp) while the asset's currency is pounds.
        var scale = meta.TryGetProperty("currency", out var currency) && currency.GetString() == "GBp" ? 0.01m : 1m;
        var indicators = result.GetProperty("indicators");
        var quote = indicators.GetProperty("quote")[0];
        // Adjusted only when the series has adjclose (indexes and rates don't); then a day without it is left out, so raw
        // and adjusted prices never mix in one series.
        JsonElement? adjClose = adjusted && indicators.TryGetProperty("adjclose", out var adj) ? adj[0].GetProperty("adjclose") : null;

        var candles = new List<Candle>();
        for (var i = 0; i < timestamps.GetArrayLength(); i++)
        {
            var open = Number(quote, "open", i);
            var high = Number(quote, "high", i);
            var low = Number(quote, "low", i);
            var close = Number(quote, "close", i);
            if (open is not > 0 || high is not > 0 || low is not > 0 || close is not > 0) continue; // Yahoo can return a day with a volume but no prices.

            var factor = 1m;
            if (adjClose is { } list)
            {
                var adjustedClose = Number(list, i);
                if (adjustedClose is not > 0) continue;
                factor = adjustedClose.Value / close.Value;
            }
            factor *= scale;
            var at = DateTimeOffset.FromUnixTimeSeconds(timestamps[i].GetInt64());
            var date = DateOnly.FromDateTime(zone is null ? at.UtcDateTime.AddSeconds(offset) : TimeZoneInfo.ConvertTime(at, zone).DateTime);
            var volume = Number(quote, "volume", i) ?? 0;
            var candle = new Candle(date, Round(open.Value * factor, adjusted), Round(high.Value * factor, adjusted), Round(low.Value * factor, adjusted), Round(close.Value * factor, adjusted), (long)volume);
            // Rounding can push a tiny price to 0, and a bad bar can have its low above its high: neither may reach the table's checks.
            if (candle.Low <= 0 || candle.Open <= 0 || candle.High <= 0 || candle.Close <= 0 || candle.Low > candle.High) continue;
            candles.Add(candle);
        }
        // One bar per day, the last one wins (a day in progress can be listed twice around midnight).
        return [.. candles.GroupBy(c => c.Date).Select(g => g.Last()).OrderBy(c => c.Date)];
    }

    /// <summary>Four decimals, like the price column; eight for rates, which use the rate column's precision.</summary>
    static decimal Round(decimal value, bool adjusted) => Math.Round(value, adjusted ? 4 : 8);

    static decimal? Number(JsonElement list, int index) =>
        list.ValueKind == JsonValueKind.Array && index < list.GetArrayLength() && list[index].ValueKind == JsonValueKind.Number
            ? (decimal)list[index].GetDouble()
            : null;

    static decimal? Number(JsonElement quote, string name, int index) =>
        quote.TryGetProperty(name, out var list) ? Number(list, index) : null;
}
