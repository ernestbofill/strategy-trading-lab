namespace StrategyTradingLab.Api.Auth;

/// <summary>
/// 5 wrong passwords for one email within 15 minutes make it wait 15 minutes. Counted in memory per email, for
/// emails with and without an account alike, so the lock never tells which emails have one. A restart forgets it.
/// At most <see cref="MaxEmails"/> emails are tracked; the oldest entries go first when it is full.
/// </summary>
public class LogInThrottle(TimeProvider time)
{
    public const int MaxFailures = 5, MaxEmails = 100_000;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    sealed record Entry(int Failures, DateTimeOffset FirstFailureAt, DateTimeOffset? LockedUntil)
    {
        /// <summary>When the entry stops mattering: its lock is over and its failures are out of the window.</summary>
        public DateTimeOffset Expires => LockedUntil is { } until && until > FirstFailureAt + Window ? until : FirstFailureAt + Window;
    }

    readonly Dictionary<string, Entry> entries = new();
    readonly object gate = new();

    static string Key(string email) => email.Trim().ToLowerInvariant();

    public bool IsLocked(string email)
    {
        lock (gate) return entries.TryGetValue(Key(email), out var entry) && entry.LockedUntil > time.GetUtcNow();
    }

    public void Failed(string email)
    {
        var now = time.GetUtcNow();
        lock (gate)
        {
            if (!entries.TryGetValue(Key(email), out var entry) || now - entry.FirstFailureAt > Window)
            {
                entry = new Entry(0, now, entry?.LockedUntil > now ? entry.LockedUntil : null);
                if (entries.Count >= MaxEmails) Prune(now);
            }
            var failures = entry.Failures + 1;
            entries[Key(email)] = failures >= MaxFailures ? new Entry(0, now, now + Window) : entry with { Failures = failures };
        }
    }

    /// <summary>A good log-in or a password reset clears the email's count and lock.</summary>
    public void Succeeded(string email)
    {
        lock (gate) entries.Remove(Key(email));
    }

    void Prune(DateTimeOffset now)
    {
        foreach (var (key, _) in entries.Where(e => e.Value.Expires <= now).ToList()) entries.Remove(key);
        if (entries.Count < MaxEmails) return;
        foreach (var (key, _) in entries.OrderBy(e => e.Value.FirstFailureAt).Take(entries.Count / 10).ToList()) entries.Remove(key);
    }
}
