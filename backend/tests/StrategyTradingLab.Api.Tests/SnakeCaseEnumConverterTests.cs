using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Tests;

public class SnakeCaseEnumConverterTests
{
    [Theory]
    [InlineData(SkipReason.PriceRanAway, "price_ran_away")]
    [InlineData(SkipReason.DidntTrustIt, "didnt_trust_it")]
    [InlineData(SkipReason.Other, "other")]
    public void Round_trips_snake_case(SkipReason value, string stored)
    {
        var converter = new SnakeCaseEnumConverter<SkipReason>();

        Assert.Equal(stored, converter.ConvertToProvider(value));
        Assert.Equal(value, converter.ConvertFromProvider(stored));
    }

    [Fact]
    public void Names_the_enum_when_the_database_has_an_unknown_value()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => new SnakeCaseEnumConverter<SkipReason>().ConvertFromProvider("market_closed"));

        Assert.Contains("market_closed", error.Message);
        Assert.Contains(nameof(SkipReason), error.Message);
    }

    [Fact]
    public void Lists_every_value_for_the_check()
    {
        Assert.Equal(["waiting", "confirmed", "skipped", "missed"], SnakeCaseEnumConverter<SignalStatus>.StoredValues);
    }
}
