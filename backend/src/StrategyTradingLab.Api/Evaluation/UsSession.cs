namespace StrategyTradingLab.Api.Evaluation;

/// <summary>The US market's day: rules are checked once a day after its close (spec: How the two modes run).</summary>
public static class UsSession
{
    /// <summary>The market's time zone.</summary>
    public static readonly TimeZoneInfo NewYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
    /// <summary>The close is 16:00 in New York; the bar is final a little later.</summary>
    static readonly TimeOnly FinalTime = new(16, 30);

    /// <summary>When a session's bar is final (16:30 in New York that day): its check can run, and what it finds can go out.</summary>
    public static DateTimeOffset FinalAt(DateOnly session) =>
        new(TimeZoneInfo.ConvertTimeToUtc(session.ToDateTime(FinalTime), NewYork), TimeSpan.Zero);

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
        if (TimeOnly.FromDateTime(local.DateTime) < FinalTime) day = day.AddDays(-1);
        while (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) day = day.AddDays(-1);
        return day;
    }

    /// <summary>The weekday session before <paramref name="session"/> (holidays aren't known: a holiday simply has no check).</summary>
    public static DateOnly Before(DateOnly session)
    {
        var day = session.AddDays(-1);
        while (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) day = day.AddDays(-1);
        return day;
    }

    /// <summary>The weekdays after <paramref name="from"/>, up to and including <paramref name="to"/>: how many sessions a price is behind, holidays aside.</summary>
    public static int WeekdaysBetween(DateOnly from, DateOnly to)
    {
        var count = 0;
        for (var day = from.AddDays(1); day <= to; day = day.AddDays(1))
            if (day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) count++;
        return count;
    }
}
