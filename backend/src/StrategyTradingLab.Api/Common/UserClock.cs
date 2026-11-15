using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Common;

/// <summary>A user's clock: their time zone, now and today there. UTC for a zone this server doesn't know, as everywhere.</summary>
public static class UserClock
{
    /// <summary>The user's zone; UTC for a name this server doesn't know.</summary>
    public static TimeZoneInfo Zone(string timeZone) =>
        TimeZoneInfo.TryFindSystemTimeZoneById(timeZone, out var zone) ? zone : TimeZoneInfo.Utc;

    /// <summary><paramref name="now"/> on the zone's clock.</summary>
    public static DateTimeOffset LocalNow(DateTimeOffset now, string timeZone) => TimeZoneInfo.ConvertTime(now, Zone(timeZone));

    /// <summary>The date in a time zone at <paramref name="now"/>.</summary>
    public static DateOnly Today(string timeZone, DateTimeOffset now) => DateOnly.FromDateTime(LocalNow(now, timeZone).DateTime);

    /// <summary>The user's local date now.</summary>
    public static DateOnly Today(User user, TimeProvider time) => Today(user.TimeZone, time.GetUtcNow());

    /// <summary>The user's local date the strategy ended; null while it runs.</summary>
    public static DateOnly? EndedOn(Strategy strategy, User user) => strategy.EndedAt is { } at ? Today(user.TimeZone, at) : null;
}
