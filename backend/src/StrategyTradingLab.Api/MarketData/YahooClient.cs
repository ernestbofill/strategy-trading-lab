using System.Globalization;
using System.Text.Json;

namespace StrategyTradingLab.Api.MarketData;

/// <summary>One trading day. Prices are in the asset's currency; a stock's are adjusted for splits and dividends.</summary>
public record Candle(DateOnly Date, decimal Open, decimal High, decimal Low, decimal Close, long Volume);

/// <summary>
/// One answer of the price source's search: the symbol as the source writes it, the name, the source's exchange code
/// (Yahoo's <c>NMS</c>, not the MIC), its type (<c>EQUITY</c>, <c>ETF</c>, <c>INDEX</c>…), and a stock's sector and industry.
/// </summary>
public record SymbolMatch(string Symbol, string Name, string Exchange, string Type, string? Sector, string? Industry);

/// <summary>Where daily candles come from, so the refresh can be tested without the network.</summary>
public interface IMarketDataSource
{
    /// <summary>The last month, or the whole history. Oldest first; days without prices are left out.</summary>
    Task<IReadOnlyList<Candle>> DailyAsync(string symbol, bool fullHistory, bool adjusted, CancellationToken cancellationToken);

    /// <summary>The best matches for a symbol or a name, best first.</summary>
    Task<IReadOnlyList<SymbolMatch>> SearchAsync(string query, CancellationToken cancellationToken);

    /// <summary>The listing's trading currency (ISO code), in which its prices come.</summary>
    Task<string> CurrencyAsync(string symbol, CancellationToken cancellationToken);
}

public class MarketDataException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Yahoo answered, but the asset is quoted in something the app can't use yet (a minor unit it doesn't scale, no currency).</summary>
public class UnsupportedCurrencyException(string message) : MarketDataException(message);

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
        var json = await GetAsync($"v8/finance/chart/{Uri.EscapeDataString(symbol)}?range={range}&interval=1d&events=div%2Csplits", symbol, cancellationToken);
        return Parse(json, symbol, adjusted);
    }

    public async Task<IReadOnlyList<SymbolMatch>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var json = await GetAsync($"v1/finance/search?q={Uri.EscapeDataString(query)}&quotesCount=10&newsCount=0&listsCount=0", query, cancellationToken);
        return ParseSearch(json, query);
    }

    public async Task<string> CurrencyAsync(string symbol, CancellationToken cancellationToken)
    {
        var json = await GetAsync($"v8/finance/chart/{Uri.EscapeDataString(symbol)}?range=5d&interval=1d", symbol, cancellationToken);
        return ParseCurrency(json, symbol);
    }

    async Task<string> GetAsync(string path, string what, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.GetAsync(path, cancellationToken);
            if (!response.IsSuccessStatusCode) throw new MarketDataException($"Yahoo answered HTTP {(int)response.StatusCode} for {what}.");
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception error) when (error is HttpRequestException || (error is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // A timeout is a TaskCanceledException that isn't ours: it's a failure of this symbol, not a shutdown.
            throw new MarketDataException($"Couldn't reach Yahoo for {what}.", error);
        }
    }

    /// <summary>
    /// Reads Yahoo's search answer. Its quotes write the names in lower case (<c>longname</c>, <c>shortname</c>); one
    /// without a symbol, a name or a type (a news item, a private company) is left out.
    /// </summary>
    public static IReadOnlyList<SymbolMatch> ParseSearch(string json, string query)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("quotes", out var quotes) || quotes.ValueKind != JsonValueKind.Array) return [];
            var matches = new List<SymbolMatch>();
            foreach (var quote in quotes.EnumerateArray())
            {
                var symbol = Text(quote, "symbol");
                var name = Text(quote, "longname") ?? Text(quote, "shortname");
                var exchange = Text(quote, "exchange");
                var type = Text(quote, "quoteType");
                if (symbol is null || name is null || exchange is null || type is null) continue;
                matches.Add(new SymbolMatch(symbol, name, exchange, type, Text(quote, "sector"), Text(quote, "industry")));
            }
            return matches;
        }
        catch (JsonException error)
        {
            throw new MarketDataException($"Yahoo's search answer for {query} wasn't what we expected.", error);
        }
    }

    /// <summary>Minor units Yahoo quotes in, with the ISO code of the asset's currency and the scale of its prices: London's pence.</summary>
    static readonly Dictionary<string, (string Iso, decimal Scale)> MinorUnits = new() { ["GBp"] = ("GBP", 0.01m) };

    /// <summary>Minor units that aren't scaled yet, so they are refused: Johannesburg's cents and Tel Aviv's agorot (ILA looks like an ISO code, but isn't one).</summary>
    static readonly HashSet<string> UnscaledMinorUnits = ["ZAc", "ILA"];

    /// <summary>
    /// The currency in a chart answer's meta. London's pence (<c>GBp</c>) are pounds, as <see cref="Parse"/> scales
    /// them; another minor unit (<c>ZAc</c>, <c>ILA</c>) isn't scaled yet, so it is refused rather than stored as a wrong code.
    /// </summary>
    public static string ParseCurrency(string json, string symbol)
    {
        string? currency;
        try
        {
            using var document = JsonDocument.Parse(json);
            currency = Text(ChartResult(document, symbol).GetProperty("meta"), "currency");
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
        {
            throw new MarketDataException($"Yahoo's answer for {symbol} wasn't what we expected.", error);
        }
        if (currency is not null && MinorUnits.TryGetValue(currency, out var minor)) return minor.Iso;
        if (currency is null || UnscaledMinorUnits.Contains(currency) || currency.Length != 3 || !currency.All(char.IsAsciiLetterUpper))
            throw new UnsupportedCurrencyException($"Yahoo quotes {symbol} in {currency ?? "no currency"}, which the app can't use yet.");
        return currency;
    }

    static string? Text(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text
            ? text.Trim()
            : null;

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
        var result = ChartResult(document, symbol);

        if (!result.TryGetProperty("timestamp", out var timestamps)) return []; // A symbol with no days in the range.
        var meta = result.GetProperty("meta");
        var offset = meta.TryGetProperty("gmtoffset", out var gmt) ? gmt.GetInt32() : 0;
        // A bar's date is the day in the exchange's zone at that bar's time: the zone's offset changes with the clocks, while
        // gmtoffset is only today's, which would date a summer-time bar a day early after the clocks go back.
        var zone = meta.TryGetProperty("exchangeTimezoneName", out var zoneName) && zoneName.GetString() is { } id
            && TimeZoneInfo.TryFindSystemTimeZoneById(id, out var found) ? found : null;
        // London stocks are quoted in pence (GBp) while the asset's currency is pounds.
        var scale = meta.TryGetProperty("currency", out var currency) && currency.GetString() is { } code && MinorUnits.TryGetValue(code, out var minor) ? minor.Scale : 1m;
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

    /// <summary>The first result of a chart answer; Yahoo's own error for the symbol becomes a <see cref="MarketDataException"/>.</summary>
    static JsonElement ChartResult(JsonDocument document, string symbol)
    {
        var chart = document.RootElement.GetProperty("chart");
        if (chart.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
            throw new MarketDataException($"Yahoo has no data for {symbol}: {error.GetProperty("description").GetString()}");
        return chart.GetProperty("result")[0];
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
