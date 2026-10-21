using Microsoft.Extensions.Caching.Memory;

namespace StrategyTradingLab.Api.Auth;

/// <summary>
/// 5 wrong passwords for one email within 15 minutes make it wait 15 minutes. Counted in memory per email, for
/// emails with and without an account alike, so the lock never tells which emails have one. A restart forgets it.
/// </summary>
public class LogInThrottle(IMemoryCache cache, TimeProvider time)
{
    public const int MaxFailures = 5;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    sealed class Entry
    {
        public int Failures;
        public DateTimeOffset FirstFailureAt;
        public DateTimeOffset? LockedUntil;
    }

    static string Key(string email) => "log-in:" + email.ToLowerInvariant();

    public bool IsLocked(string email) =>
        cache.TryGetValue<Entry>(Key(email), out var entry) && entry!.LockedUntil > time.GetUtcNow();

    public void Failed(string email)
    {
        var now = time.GetUtcNow();
        var entry = cache.GetOrCreate(Key(email), item =>
        {
            item.AbsoluteExpirationRelativeToNow = Window * 2;
            return new Entry { FirstFailureAt = now };
        })!;
        lock (entry)
        {
            if (now - entry.FirstFailureAt > Window) (entry.Failures, entry.FirstFailureAt) = (0, now);
            if (++entry.Failures >= MaxFailures) (entry.LockedUntil, entry.Failures) = (now + Window, 0);
        }
    }

    public void Succeeded(string email) => cache.Remove(Key(email));
}
