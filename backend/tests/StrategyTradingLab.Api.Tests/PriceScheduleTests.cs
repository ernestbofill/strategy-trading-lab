using StrategyTradingLab.Api.Admin;
using StrategyTradingLab.Api.MarketData;

namespace StrategyTradingLab.Api.Tests;

public class PriceScheduleTests
{
    static PriceSchedule Madrid(string at = "22:30") => PriceSchedule.From(new Dictionary<string, string?>
        { ["PRICES_SCHEDULE_ENABLED"] = "true", ["PRICES_TIME_ZONE"] = "Europe/Madrid", ["PRICES_DAILY_TIME"] = at });

    static DateTimeOffset Utc(int month, int day, int hour, int minute = 0) => new(2026, month, day, hour, minute, 0, TimeSpan.Zero);

    [Fact]
    public void The_next_run_is_today_if_the_time_has_not_passed_else_tomorrow()
    {
        // October 23rd is summer time in Madrid (UTC+2): 22:30 there is 20:30 UTC.
        Assert.Equal(TimeSpan.FromHours(1), Madrid().UntilNext(Utc(10, 23, 19, 30)));
        Assert.Equal(TimeSpan.FromHours(24), Madrid().UntilNext(Utc(10, 23, 20, 30)));
        Assert.Equal(TimeSpan.FromHours(23), Madrid().UntilNext(Utc(10, 23, 21, 30)));
    }

    [Fact]
    public void The_time_follows_the_zones_winter_time()
    {
        // December: UTC+1, so 22:30 in Madrid is 21:30 UTC.
        Assert.Equal(TimeSpan.FromHours(1), Madrid().UntilNext(Utc(12, 1, 20, 30)));
    }

    [Fact]
    public void A_time_skipped_when_the_clocks_go_forward_runs_an_hour_later()
    {
        // 2026-03-29, 02:30 doesn't exist in Madrid (02:00 jumps to 03:00): it runs at 03:30 local, 01:30 UTC.
        Assert.Equal(TimeSpan.FromHours(1), Madrid("02:30").UntilNext(Utc(3, 29, 0, 30)));
    }

    [Fact]
    public void Missing_or_bad_values_fall_back_to_the_defaults_and_false_turns_it_off()
    {
        var bad = PriceSchedule.From(new Dictionary<string, string?> { ["PRICES_TIME_ZONE"] = "Mars/Base", ["PRICES_DAILY_TIME"] = "25:99" });
        var off = PriceSchedule.From(new Dictionary<string, string?> { ["PRICES_SCHEDULE_ENABLED"] = "false" });

        Assert.True(bad.Enabled);
        Assert.Equal("Europe/Madrid", bad.Zone.Id);
        Assert.Equal(new TimeOnly(22, 30), bad.At);
        Assert.False(off.Enabled);
    }

    [Theory]
    [InlineData("PRICES_DAILY_TIME", "22:30", true)]
    [InlineData("PRICES_DAILY_TIME", "7:30", false)]
    [InlineData("PRICES_DAILY_TIME", "24:00", false)]
    [InlineData("PRICES_TIME_ZONE", "Europe/Madrid", true)]
    [InlineData("PRICES_TIME_ZONE", "Mars/Base", false)]
    [InlineData("PRICES_SCHEDULE_ENABLED", "true", true)]
    [InlineData("PRICES_SCHEDULE_ENABLED", "yes", false)]
    public void The_settings_check_what_is_typed(string key, string value, bool valid) =>
        Assert.Equal(valid, SettingDefinitions.Find(key)!.Validate!(value) is null);
}
