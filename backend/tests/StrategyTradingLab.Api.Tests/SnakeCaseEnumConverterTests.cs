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
    public void Lists_every_value_for_the_check()
    {
        Assert.Equal(["waiting", "confirmed", "skipped", "missed"], SnakeCaseEnumConverter<SignalStatus>.StoredValues);
    }
}
