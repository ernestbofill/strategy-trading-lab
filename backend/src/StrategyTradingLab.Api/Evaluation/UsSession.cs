namespace StrategyTradingLab.Api.Evaluation;

/// <summary>The US market's day: rules are checked once a day after its close (spec: How the two modes run).</summary>
public static class UsSession
{
    static readonly TimeZoneInfo NewYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
    /// <summary>The close is 16:00 in New York; the bar is final a little later.</summary>
    static readonly TimeOnly FinalAt = new(16, 30);

    /// <summary>The market's calendar day in New York: the spec's "day" for snoozes and the next day.</summary>
    public static DateOnly Today(DateTimeOffset now) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, NewYork).DateTime);

    /// <summary>
    /// The latest weekday whose session is over and its bar final. Holidays aren't known here: a holiday has no bar, so
    /// whoever reads the prices takes the newest bar on or before this day.
    /// </summary>
    public static DateOnly LastComplete(DateTimeOffset now)
    {
        var local = TimeZoneInfo.ConvertTime(now, NewYork);
        var day = DateOnly.FromDateTime(local.DateTime);
        if (TimeOnly.FromDateTime(local.DateTime) < FinalAt) day = day.AddDays(-1);
        while (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) day = day.AddDays(-1);
        return day;
    }
}
