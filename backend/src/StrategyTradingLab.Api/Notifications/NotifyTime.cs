using StrategyTradingLab.Api.Evaluation;

namespace StrategyTradingLab.Api.Notifications;

/// <summary>
/// When a user's notifications come (spec: Settings, Notifications). The user picks an hour in their own time zone (07:00 by
/// default) or "as soon as they're ready" (no hour). What a daily check finds, the signal alerts and the summary, waits for the
/// first such hour after the session's close; reminders (snoozed signals, AI rounds due) come at that hour on their day.
/// </summary>
public static class NotifyTime
{
    /// <summary>The hour new accounts get, and the reminders' hour for "as soon as they're ready".</summary>
    public const int DefaultHour = 7;

    /// <summary>Whether <paramref name="hour"/> is one a user can pick: null (as soon as ready) or 0 to 23.</summary>
    public static bool IsHour(int? hour) => hour is null or (>= 0 and <= 23);

    /// <summary>
    /// The hour reminders come. Reminders aren't made by the daily check, so "as soon as they're ready" has no moment for them:
    /// they keep the default morning hour.
    /// </summary>
    public static int ReminderHour(int? hour) => hour ?? DefaultHour;

    /// <summary>
    /// When what the check of <paramref name="session"/> found (its signal alerts, its summary) may go out: as soon as the session's
    /// bar is final for "as soon as ready", else the first <paramref name="hour"/>:00 in the user's time zone at or after that.
    /// </summary>
    public static DateTimeOffset DueAt(DateOnly session, int? hour, string timeZone)
    {
        var final = UsSession.FinalAt(session);
        if (hour is not { } h) return final;
        var zone = Zone(timeZone);
        var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(final, zone).DateTime);
        var due = At(day, h, zone);
        return due >= final ? due : At(day.AddDays(1), h, zone);
    }

    /// <summary>The user's zone; UTC for a name this server doesn't know, as the dashboard does.</summary>
    public static TimeZoneInfo Zone(string timeZone) =>
        TimeZoneInfo.TryFindSystemTimeZoneById(timeZone, out var zone) ? zone : TimeZoneInfo.Utc;

    /// <summary><paramref name="hour"/>:00 of a local day; an hour a clock change skips becomes the hour after it.</summary>
    static DateTimeOffset At(DateOnly day, int hour, TimeZoneInfo zone)
    {
        var local = day.ToDateTime(new TimeOnly(hour, 0));
        if (zone.IsInvalidTime(local)) local = local.AddHours(1);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone), TimeSpan.Zero);
    }
}
