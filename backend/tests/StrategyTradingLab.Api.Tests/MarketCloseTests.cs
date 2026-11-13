using StrategyTradingLab.Api.MarketData;

namespace StrategyTradingLab.Api.Tests;

public class MarketCloseTests
{
    [Fact]
    public void Returns_regular_close_in_utc_for_known_exchanges()
    {
        // Nov 12, 2026 is EST (UTC-5): 16:00 New York = 21:00 UTC.
        Assert.Equal(new DateTimeOffset(2026, 11, 12, 21, 0, 0, TimeSpan.Zero), MarketClose.At("XNAS", new DateOnly(2026, 11, 12)));
        Assert.Equal(new DateTimeOffset(2026, 11, 12, 21, 0, 0, TimeSpan.Zero), MarketClose.At("xnys", new DateOnly(2026, 11, 12)));
        // London is GMT (UTC+0) in Nov: 16:30 London = 16:30 UTC.
        Assert.Equal(new DateTimeOffset(2026, 11, 12, 16, 30, 0, TimeSpan.Zero), MarketClose.At("XLON", new DateOnly(2026, 11, 12)));
        // Summer DST: Jul 15, 2026 is EDT (UTC-4): 16:00 New York = 20:00 UTC.
        Assert.Equal(new DateTimeOffset(2026, 7, 15, 20, 0, 0, TimeSpan.Zero), MarketClose.At("XNAS", new DateOnly(2026, 7, 15)));
    }

    [Fact]
    public void Returns_null_when_the_exchange_is_unknown()
    {
        Assert.Null(MarketClose.At("UNKNOWN", new DateOnly(2026, 11, 12)));
        Assert.Null(MarketClose.At(null, new DateOnly(2026, 11, 12)));
    }
}
