using System.Globalization;
using System.Text.Json;

namespace StrategyTradingLab.Api.MarketData;

/// <summary>One trading day. Prices are in the asset's currency; a stock's are adjusted for splits and dividends.</summary>
public record Candle(DateOnly Date, decimal Open, decimal High, decimal Low, decimal Close, long Volume);

/// <summary>Where daily candles come from, so the refresh can be tested without the network.</summary>
public interface IMarketDataSource
{
    /// <summary>The last month, or the whole history. Oldest first; days without prices are left out.</summary>
    Task<IReadOnlyList<Candle>> DailyAsync(string symbol, bool fullHistory, bool adjusted, CancellationToken cancellationToken);
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
            var date = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(timestamps[i].GetInt64() + offset).UtcDateTime);
            var volume = Number(quote, "volume", i) ?? 0;
            var candle = new Candle(date, Price(open.Value * factor), Price(high.Value * factor), Price(low.Value * factor), Price(close.Value * factor), (long)volume);
            // Rounding can push a tiny price to 0, and a bad bar can have its low above its high: neither may reach the table's checks.
            if (candle.Low <= 0 || candle.Open <= 0 || candle.High <= 0 || candle.Close <= 0 || candle.Low > candle.High) continue;
            candles.Add(candle);
        }
        // One bar per day, the last one wins (a day in progress can be listed twice around midnight).
        return [.. candles.GroupBy(c => c.Date).Select(g => g.Last()).OrderBy(c => c.Date)];
    }

    /// <summary>Stored with four decimals, like the column.</summary>
    static decimal Price(decimal value) => Math.Round(value, 4);

    static decimal? Number(JsonElement list, int index) =>
        list.ValueKind == JsonValueKind.Array && index < list.GetArrayLength() && list[index].ValueKind == JsonValueKind.Number
            ? (decimal)list[index].GetDouble()
            : null;

    static decimal? Number(JsonElement quote, string name, int index) =>
        quote.TryGetProperty(name, out var list) ? Number(list, index) : null;
}
