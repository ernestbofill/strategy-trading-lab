using System.Text;

namespace StrategyTradingLab.Api.Backtest;

/// <summary>
/// A member that may be drawn on a period's start: in the index that day, with prices from the period's warm-up on. Its size is
/// <paramref name="TradedValue"/>, the average daily traded value (close × volume) over the 60 trading days before the start.
/// </summary>
public record DrawCandidate(string Ticker, string Symbol, string Sector, decimal TradedValue);

/// <summary>A drafted stock: its fja ticker, Yahoo symbol, sector and size third (1 low, 2 middle, 3 high) in its seat.</summary>
public record DraftStock(string Ticker, string Symbol, string Sector, int Third);

/// <summary>
/// A drafted stock that can't stay at a period's start, and the member that took its seat (null: none could). Usually it left the index;
/// <c>StillInIndex</c> when it is still a member but can't be drawn (its prices stopped, say), so it can't be bought that period either.
/// </summary>
public record DraftReplacement(DraftStock Left, DraftStock? Replacement, bool StillInIndex = false);

/// <summary>The test draft in one period: its stocks, and for a rolled period those kept and replaced. <c>Notes</c> say where a rule couldn't be followed.</summary>
public record DraftPeriod(BacktestPeriod Period, IReadOnlyList<DraftStock> Stocks, IReadOnlyList<DraftStock> Kept,
    IReadOnlyList<DraftReplacement> Replaced, IReadOnlyList<string> Notes);

/// <summary>
/// Draws the test draft (docs/backtest.md, *How it is drawn*): at the first period's start, seats per sector in proportion to its share
/// of the eligible members, split across the sector's size thirds, filled at random with a fixed seed; at each later start, the stocks
/// still in the index stay and each one that left is replaced from its sector and size third, measured that day. A stock is known by
/// its Yahoo symbol, so a renamed ticker (FB, then META) stays the same stock. Pure: the same members and seed give the same draft.
/// </summary>
public static class DraftDraw
{
    public const int DefaultSize = 60;

    /// <summary>The published seed (docs/backtest.md). Changing it to get nicer numbers would be cherry-picking.</summary>
    public const ulong DefaultSeed = 20261125;

    /// <param name="members">Each period's members on its start, by Yahoo symbol, eligible or not: a drafted stock among them that can't
    /// be drawn is replaced as still in the index, not as gone. Null: every stock that can't be drawn left.</param>
    public static IReadOnlyList<DraftPeriod> Draw(IReadOnlyList<(BacktestPeriod Period, IReadOnlyList<DrawCandidate> Eligible)> periods,
        int size = DefaultSize, ulong seed = DefaultSeed, Func<BacktestPeriod, IReadOnlySet<string>>? members = null)
    {
        var drafts = new List<DraftPeriod>();
        foreach (var (period, eligible) in periods)
            drafts.Add(drafts.Count == 0 ? First(period, eligible, size, seed) : Roll(drafts[^1], period, eligible, seed, members?.Invoke(period)));
        return drafts;
    }

    /// <summary>Each eligible member's size third within its sector, by traded value (ties by symbol): 1 low, 2 middle, 3 high.</summary>
    public static IReadOnlyDictionary<string, int> Thirds(IReadOnlyList<DrawCandidate> eligible)
    {
        var thirds = new Dictionary<string, int>();
        foreach (var sector in eligible.GroupBy(c => c.Sector))
        {
            var sorted = sector.OrderBy(c => c.TradedValue).ThenBy(c => c.Symbol, StringComparer.Ordinal).ToList();
            for (var i = 0; i < sorted.Count; i++) thirds[sorted[i].Symbol] = 1 + i * 3 / sorted.Count;
        }
        return thirds;
    }

    /// <summary>
    /// Seats per sector, in proportion to its share of the eligible members, by largest remainder (ties: the larger sector, then by name),
    /// never more than the sector has.
    /// </summary>
    public static IReadOnlyDictionary<string, int> SectorSeats(IReadOnlyList<DrawCandidate> eligible, int size)
    {
        var counts = eligible.GroupBy(c => c.Sector).ToDictionary(g => g.Key, g => g.Count());
        var total = counts.Values.Sum();
        if (total <= size) return counts;
        var seats = counts.ToDictionary(c => c.Key, c => size * c.Value / total);
        var order = counts.OrderByDescending(c => (double)size * c.Value / total - seats[c.Key]).ThenByDescending(c => c.Value)
            .ThenBy(c => c.Key, StringComparer.Ordinal).Select(c => c.Key).ToList();
        for (var i = 0; seats.Values.Sum() < size; i = (i + 1) % order.Count)
            if (seats[order[i]] < counts[order[i]]) seats[order[i]]++;
        return seats;
    }

    static DraftPeriod First(BacktestPeriod period, IReadOnlyList<DrawCandidate> eligible, int size, ulong seed)
    {
        var thirds = Thirds(eligible);
        var stocks = new List<DraftStock>();
        foreach (var (sector, seats) in SectorSeats(eligible, size).OrderBy(s => s.Key, StringComparer.Ordinal))
        {
            var groups = Enumerable.Range(1, 3).ToDictionary(t => t, t => Shuffled(eligible.Where(c => c.Sector == sector && thirds[c.Symbol] == t),
                seed, $"{period.Start:yyyy-MM-dd}|{sector}|{t}"));
            // An even split, the remainder to thirds in a seeded order; a third with too few members passes its seats on.
            var order = Shuffle(new List<int> { 1, 2, 3 }, seed, $"{period.Start:yyyy-MM-dd}|{sector}|thirds");
            var want = new Dictionary<int, int> { [1] = seats / 3, [2] = seats / 3, [3] = seats / 3 };
            for (var k = 0; k < seats % 3; k++) want[order[k]]++;
            var take = want.ToDictionary(w => w.Key, w => Math.Min(w.Value, groups[w.Key].Count));
            foreach (var t in order)
                take[t] += Math.Min(seats - take.Values.Sum(), groups[t].Count - take[t]);
            foreach (var t in new[] { 1, 2, 3 })
                stocks.AddRange(groups[t].Take(take[t]).Select(c => new DraftStock(c.Ticker, c.Symbol, sector, t)));
        }
        var notes = stocks.Count < size ? new[] { $"Only {stocks.Count} members could be drawn, not {size}." } : [];
        return new(period, stocks, [], [], notes);
    }

    static DraftPeriod Roll(DraftPeriod previous, BacktestPeriod period, IReadOnlyList<DrawCandidate> eligible, ulong seed, IReadOnlySet<string>? members)
    {
        var bySymbol = eligible.ToDictionary(c => c.Symbol);
        var thirds = Thirds(eligible);
        // A kept stock keeps its seat's sector; its ticker and size third are this period's (a later replacement draws from that third).
        var kept = previous.Stocks.Where(s => bySymbol.ContainsKey(s.Symbol))
            .Select(s => s with { Ticker = bySymbol[s.Symbol].Ticker, Third = thirds[s.Symbol] }).ToList();
        var used = kept.Select(s => s.Symbol).ToHashSet();
        var stocks = new List<DraftStock>(kept);
        var replaced = new List<DraftReplacement>();
        var notes = new List<string>();
        foreach (var left in previous.Stocks.Where(s => !bySymbol.ContainsKey(s.Symbol)))
        {
            // The seat's own sector and third first; with none left there, the nearest third of the sector.
            DrawCandidate? pick = null;
            foreach (var third in new[] { left.Third }.Concat(new[] { 1, 2, 3 }.Where(t => t != left.Third).OrderBy(t => Math.Abs(t - left.Third)).ThenBy(t => t)))
            {
                pick = Shuffled(eligible.Where(c => c.Sector == left.Sector && thirds[c.Symbol] == third), seed, $"{period.Start:yyyy-MM-dd}|{left.Sector}|{third}")
                    .FirstOrDefault(c => !used.Contains(c.Symbol));
                if (pick is null) continue;
                if (third != left.Third) notes.Add($"{left.Symbol}'s seat ({left.Sector}, third {left.Third}) went to third {third}: none left in its own.");
                break;
            }
            if (pick is null) notes.Add($"No member of {left.Sector} was left to replace {left.Symbol}.");
            var replacement = pick is null ? null : new DraftStock(pick.Ticker, pick.Symbol, left.Sector, thirds[pick.Symbol]);
            if (replacement is not null)
            {
                used.Add(replacement.Symbol);
                stocks.Add(replacement);
            }
            var stillIn = members?.Contains(left.Symbol) ?? false;
            if (stillIn) notes.Add($"{left.Symbol} is still in the index but can't be drawn (see the members that can't be drawn): its seat was refilled.");
            replaced.Add(new(left, replacement, stillIn));
        }
        return new(period, stocks, kept, replaced, notes);
    }

    /// <summary>The members in a seeded order: sorted by symbol, then shuffled with the stream for <paramref name="key"/>.</summary>
    static List<DrawCandidate> Shuffled(IEnumerable<DrawCandidate> members, ulong seed, string key) =>
        Shuffle(members.OrderBy(c => c.Symbol, StringComparer.Ordinal).ToList(), seed, key);

    /// <summary>A Fisher–Yates shuffle with SplitMix64, seeded by the seed and the key, so each group has its own stream.</summary>
    internal static List<T> Shuffle<T>(List<T> items, ulong seed, string key)
    {
        var random = new SplitMix64(seed ^ Fnv1a(key));
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = (int)(random.Next() % (ulong)(i + 1));
            (items[i], items[j]) = (items[j], items[i]);
        }
        return items;
    }

    static ulong Fnv1a(string text)
    {
        var hash = 14695981039346656037UL;
        foreach (var b in Encoding.UTF8.GetBytes(text)) hash = (hash ^ b) * 1099511628211UL;
        return hash;
    }

    /// <summary>SplitMix64: a small generator whose output is fixed by its definition, unlike <see cref="Random"/>'s across .NET versions.</summary>
    sealed class SplitMix64(ulong state)
    {
        public ulong Next()
        {
            var z = state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
