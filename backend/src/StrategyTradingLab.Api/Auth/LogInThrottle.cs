namespace StrategyTradingLab.Api.Auth;

/// <summary>
/// 5 log-in tries for one email within 15 minutes make it wait 15 minutes. Counted in memory per email, for emails
/// with and without an account alike, so the lock never tells which emails have one. A restart forgets it.
/// A try is counted before its password is checked, so tries sent all at once can't get past the lock; a good log-in
/// clears the count. At most <see cref="MaxEmails"/> emails are tracked (see <see cref="Prune"/>).
/// </summary>
public class LogInThrottle(TimeProvider time)
{
    public const int MaxTries = 5, MaxEmails = 100_000;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    /// <summary>A lock starts a new window, so it ends when its window does.</summary>
    sealed record Entry(int Tries, DateTimeOffset WindowStart, bool Locked)
    {
        public DateTimeOffset Expires => WindowStart + Window;
    }

    readonly Dictionary<string, Entry> entries = new();
    readonly object gate = new();

    static string Key(string email) => email.Trim().ToLowerInvariant();

    /// <summary>For tests: the endpoint uses <see cref="TryAttempt"/>, which checks and counts in one step.</summary>
    internal bool IsLocked(string email)
    {
        lock (gate) return entries.TryGetValue(Key(email), out var entry) && entry.Locked && entry.Expires > time.GetUtcNow();
    }

    /// <summary>Counts a log-in try for the email, or answers false, without counting, while the email is locked.</summary>
    public bool TryAttempt(string email)
    {
        var now = time.GetUtcNow();
        var key = Key(email);
        lock (gate)
        {
            if (!entries.TryGetValue(key, out var entry) || entry.Expires <= now)
            {
                if (entry is null && entries.Count >= MaxEmails) Prune(now);
                entry = new Entry(0, now, false);
            }
            if (entry.Locked) return false;
            var tries = entry.Tries + 1;
            entries[key] = tries >= MaxTries ? new Entry(0, now, true) : entry with { Tries = tries };
            return true;
        }
    }

    /// <summary>A good log-in or a password reset clears the email's count and lock.</summary>
    public void Succeeded(string email)
    {
        lock (gate) entries.Remove(Key(email));
    }

    /// <summary>
    /// Makes room down to 90% full, so it runs at most once per 10,000 new emails: first the entries that no longer
    /// matter, then the oldest counts, and locks only when nothing else is left, so a flood of single wrong tries
    /// can't clear a lock.
    /// </summary>
    void Prune(DateTimeOffset now)
    {
        var target = MaxEmails * 9 / 10;
        foreach (var key in entries.Where(e => e.Value.Expires <= now).Select(e => e.Key).ToList()) entries.Remove(key);
        if (entries.Count <= target) return;
        var oldestFirst = entries.OrderBy(e => e.Value.Locked).ThenBy(e => e.Value.WindowStart).Select(e => e.Key)
            .Take(entries.Count - target).ToList();
        foreach (var key in oldestFirst) entries.Remove(key);
    }
}
