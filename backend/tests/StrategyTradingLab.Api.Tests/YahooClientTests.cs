using StrategyTradingLab.Api.MarketData;

namespace StrategyTradingLab.Api.Tests;

/// <summary>Reading Yahoo's chart answer. No network: the JSON is what the endpoint returns (a stock, recorded and cut down).</summary>
public class YahooClientTests
{
    // 2026-10-19 13:30 UTC, then the next day, in New York time (gmtoffset -4h): both are the same dates there.
    const string Stock = """
        {"chart":{"result":[{"meta":{"currency":"USD","symbol":"ZS","gmtoffset":-14400},
        "timestamp":[1792416600,1792503000,1792589400],
        "indicators":{"quote":[{"open":[209.91,210.81,214.5],"high":[214.88,215.9,232.06],"low":[208.01,207.23,214.5],
                                "close":[212.53,214.51,231.33],"volume":[1694500,1341500,2764900]}],
                      "adjclose":[{"adjclose":[106.265,214.51,231.33]}]}}],"error":null}}
        """;

    [Fact]
    public void Adjusted_candles_scale_the_prices_by_adjclose_over_close_and_use_the_exchange_date()
    {
        var candles = YahooClient.Parse(Stock, "ZS", adjusted: true);

        Assert.Equal(3, candles.Count);
        Assert.Equal(new DateOnly(2026, 10, 19), candles[0].Date);
        Assert.Equal(106.265m, candles[0].Close);
        Assert.Equal(104.955m, candles[0].Open); // 209.91 × 0.5
        Assert.Equal(214.51m, candles[1].Close); // Factor 1.
        Assert.Equal(1341500, candles[1].Volume);
    }

    [Fact]
    public void Unadjusted_candles_keep_the_raw_prices()
    {
        var candles = YahooClient.Parse(Stock, "ZS", adjusted: false);

        Assert.Equal(212.53m, candles[0].Close);
    }

    [Fact]
    public void A_day_without_prices_is_left_out_and_a_missing_volume_is_zero()
    {
        var json = """
            {"chart":{"result":[{"meta":{"gmtoffset":0},"timestamp":[1792416600,1792503000],
            "indicators":{"quote":[{"open":[1.1,null],"high":[1.2,null],"low":[1.0,null],"close":[1.15,null],"volume":[null,500]}]}}],"error":null}}
            """;

        var candles = YahooClient.Parse(json, "EURUSD=X", adjusted: false);

        var only = Assert.Single(candles);
        Assert.Equal(1.15m, only.Close);
        Assert.Equal(0, only.Volume);
    }

    [Fact]
    public void A_symbol_with_no_days_gives_no_candles() =>
        Assert.Empty(YahooClient.Parse("""{"chart":{"result":[{"meta":{"gmtoffset":0},"indicators":{"quote":[{}]}}],"error":null}}""", "X", true));

    [Theory]
    [InlineData("""{"chart":{"result":null,"error":{"code":"Not Found","description":"No data found, symbol may be delisted"}}}""")]
    [InlineData("not json")]
    [InlineData("""{"unexpected":true}""")]
    public void An_error_or_an_unexpected_answer_is_a_market_data_error(string json) =>
        Assert.Throws<MarketDataException>(() => YahooClient.Parse(json, "BAD", true));

    static string Chart(string currency, string quote, string? adjclose = null)
    {
        var adj = adjclose is null ? "" : ",\"adjclose\":[{\"adjclose\":" + adjclose + "}]";
        return "{\"chart\":{\"result\":[{\"meta\":{\"currency\":\"" + currency + "\",\"gmtoffset\":0},\"timestamp\":[1792416600,1792503000],"
            + "\"indicators\":{\"quote\":[" + quote + "]" + adj + "}}],\"error\":null}}";
    }

    [Fact]
    public void Pence_are_scaled_to_pounds()
    {
        var json = Chart("GBp", """{"open":[1000,1010],"high":[1020,1030],"low":[990,1000],"close":[1010,1020],"volume":[1,2]}""");

        Assert.Equal(10.10m, YahooClient.Parse(json, "VOD.L", adjusted: false)[0].Close);
    }

    [Fact]
    public void A_day_with_a_zero_price_or_without_adjclose_is_left_out()
    {
        var zeroOpen = Chart("USD", """{"open":[0,5],"high":[6,6],"low":[4,4],"close":[5,5],"volume":[1,2]}""");
        var noAdjclose = Chart("USD", """{"open":[5,5],"high":[6,6],"low":[4,4],"close":[5,5],"volume":[1,2]}""", "[null,5]");

        Assert.Single(YahooClient.Parse(zeroOpen, "A", adjusted: false));
        Assert.Single(YahooClient.Parse(noAdjclose, "A", adjusted: true));
    }

    [Fact]
    public void A_malformed_series_is_a_market_data_error() =>
        Assert.Throws<MarketDataException>(() => YahooClient.Parse("""{"chart":{"result":[{"meta":{},"timestamp":[1],"indicators":{}}],"error":null}}""", "X", true));

    [Fact]
    public void A_refresh_asked_for_twice_runs_once()
    {
        var state = new PriceRefreshState();

        Assert.True(state.Request());
        Assert.False(state.Request());
        Assert.True(state.Running);
    }

    [Fact]
    public void A_summer_time_bar_keeps_its_date_after_the_clocks_go_back()
    {
        // 2026-10-22 23:00 UTC is 00:00 on the 23rd in London (BST). The zone's offset today (after 25 October) would be 0,
        // which would date it the 22nd: the bar is dated by the zone's offset at its own time.
        var json = "{\"chart\":{\"result\":[{\"meta\":{\"currency\":\"USD\",\"gmtoffset\":0,\"exchangeTimezoneName\":\"Europe/London\"},"
            + "\"timestamp\":[1792710000],\"indicators\":{\"quote\":[{\"open\":[1.16],\"high\":[1.17],\"low\":[1.15],\"close\":[1.165],\"volume\":[0]}]}}],\"error\":null}}";

        Assert.Equal(new DateOnly(2026, 10, 23), YahooClient.Parse(json, "EURUSD=X", adjusted: false)[0].Date);
    }

    [Fact]
    public void A_request_that_lands_just_before_the_run_starts_does_not_run_it_twice()
    {
        var state = new PriceRefreshState();
        state.Request();
        state.Started(DateTimeOffset.UtcNow);
        state.Finished(DateTimeOffset.UtcNow, []);

        Assert.False(state.WaitForRequestAsync(default).IsCompleted);
        Assert.True(state.Request());
    }
}
