namespace StrategyTradingLab.Api.MarketData;

/// <summary>When the daily price refresh runs: the admin page's PRICES_* settings, falling back to the defaults for a bad value.</summary>
public record PriceSchedule(bool Enabled, TimeZoneInfo Zone, TimeOnly At)
{
    public static PriceSchedule From(IReadOnlyDictionary<string, string?> values)
    {
        var zone = values.GetValueOrDefault("PRICES_TIME_ZONE") is { } name && TimeZoneInfo.TryFindSystemTimeZoneById(name, out var found)
            ? found
            : TimeZoneInfo.FindSystemTimeZoneById("Europe/Madrid");
        var at = TimeOnly.TryParseExact(values.GetValueOrDefault("PRICES_DAILY_TIME"), "HH:mm", out var time) ? time : new TimeOnly(22, 30);
        return new PriceSchedule(values.GetValueOrDefault("PRICES_SCHEDULE_ENABLED") != "false", zone, at);
    }

    /// <summary>The wait until the next daily time in the zone: today's if it hasn't passed, else tomorrow's.</summary>
    public TimeSpan UntilNext(DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, Zone).DateTime);
        var next = AtOn(today);
        if (next <= now) next = AtOn(today.AddDays(1));
        return next - now;
    }

    DateTimeOffset AtOn(DateOnly day)
    {
        var local = day.ToDateTime(At, DateTimeKind.Unspecified);
        if (Zone.IsInvalidTime(local)) local = local.AddHours(1); // The hour skipped when the clocks go forward.
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, Zone), TimeSpan.Zero);
    }
}
