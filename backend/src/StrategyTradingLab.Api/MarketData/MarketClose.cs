namespace StrategyTradingLab.Api.MarketData;

/// <summary>
/// Regular market close times for known exchange MICs, used to timestamp a close price in the user's local time.
/// Unknown exchanges return <c>null</c> so the UI omits the time instead of guessing.
/// </summary>
public static class MarketClose
{
    static readonly IReadOnlyDictionary<string, (string TimeZoneId, TimeOnly Close)> Exchanges =
        new Dictionary<string, (string, TimeOnly)>(StringComparer.OrdinalIgnoreCase)
        {
            // US exchanges
            ["XNAS"] = ("America/New_York", new TimeOnly(16, 0)),
            ["XNYS"] = ("America/New_York", new TimeOnly(16, 0)),
            ["ARCX"] = ("America/New_York", new TimeOnly(16, 0)),
            ["XASE"] = ("America/New_York", new TimeOnly(16, 0)),
            ["BATS"] = ("America/New_York", new TimeOnly(16, 0)),
            // Canada
            ["XTSE"] = ("America/Toronto", new TimeOnly(16, 0)),
            // UK & Europe
            ["XLON"] = ("Europe/London", new TimeOnly(16, 30)),
            ["XPAR"] = ("Europe/Paris", new TimeOnly(17, 30)),
            ["XAMS"] = ("Europe/Amsterdam", new TimeOnly(17, 30)),
            ["XBRU"] = ("Europe/Brussels", new TimeOnly(17, 30)),
            ["XLIS"] = ("Europe/Lisbon", new TimeOnly(16, 30)),
            ["XETR"] = ("Europe/Berlin", new TimeOnly(17, 30)),
            ["XMIL"] = ("Europe/Rome", new TimeOnly(17, 30)),
            ["XMAD"] = ("Europe/Madrid", new TimeOnly(17, 30)),
            ["XSWX"] = ("Europe/Zurich", new TimeOnly(17, 30)),
            ["XWBO"] = ("Europe/Vienna", new TimeOnly(17, 30)),
            ["XCSE"] = ("Europe/Copenhagen", new TimeOnly(17, 0)),
            ["XSTO"] = ("Europe/Stockholm", new TimeOnly(17, 30)),
            ["XHEL"] = ("Europe/Helsinki", new TimeOnly(18, 30)),
            ["XOSL"] = ("Europe/Oslo", new TimeOnly(16, 25)),
            // Asia-Pacific
            ["XHKG"] = ("Asia/Hong_Kong", new TimeOnly(16, 0)),
            ["XJPX"] = ("Asia/Tokyo", new TimeOnly(15, 30)),
            ["XASX"] = ("Australia/Sydney", new TimeOnly(16, 0)),
        };

    /// <summary>The UTC instant of <paramref name="exchange"/>'s regular close on <paramref name="day"/>, or <c>null</c> if unknown.</summary>
    public static DateTimeOffset? At(string? exchange, DateOnly day)
    {
        if (string.IsNullOrWhiteSpace(exchange) || !Exchanges.TryGetValue(exchange.Trim(), out var info)) return null;
        var zone = TimeZoneInfo.FindSystemTimeZoneById(info.TimeZoneId);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(info.Close), zone), TimeSpan.Zero);
    }
}
