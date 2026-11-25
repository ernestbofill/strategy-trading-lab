using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Engine;
using StrategyTradingLab.Api.Evaluation;
using StrategyTradingLab.Api.MarketData;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Backtest;

public class BacktestDataOptions
{
    /// <summary>The pause after each Yahoo request: a draw asks about 1,500 times, and the endpoint is unofficial.</summary>
    public TimeSpan Pause { get; set; } = TimeSpan.FromMilliseconds(300);

    /// <summary>The waits before asking again after Yahoo answered 429 (too many requests) or a server error.</summary>
    public TimeSpan[] Retries { get; set; } = [TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(90)];

    /// <summary>The folder in the bucket. The tests use their own, so they never touch a real draw.</summary>
    public string StorageRoot { get; set; } = "backtest/";
}

/// <summary>Where a draw or a snapshot is, for the owner page.</summary>
public record JobProgress(string Step, int Done, int Total);

/// <summary>A draw or a snapshot that can't go on: the message says why, for the owner page.</summary>
public class BacktestDataException(string message) : Exception(message);

/// <summary>
/// The backtest's data (docs/backtest.md, *The dataset and the draft* and *Data*). <see cref="DrawAsync"/> downloads every S&amp;P 500 member
/// of the periods' starts from Yahoo, draws the test draft, runs its two checks and takes a snapshot of the draft's stocks, SPY and the
/// starter lists; only those are kept. <see cref="SnapshotAsync"/> takes the month's snapshot of the same tickers again. Both are
/// started by the owner (<see cref="BacktestDataJob"/>) and write to object storage (<see cref="BacktestStore"/>).
/// </summary>
public class BacktestData(IMarketDataSource yahoo, BacktestStore store, TemplateStore templates, AppDbContext db, TimeProvider time,
    IOptions<BacktestDataOptions> options, ILogger<BacktestData> logger)
{
    /// <summary>The representative check passes when the draft's rate is within 2 points a year of all eligible members'.</summary>
    public const double RepresentativeGap = 0.02;

    /// <summary>The S&amp;P 500 fund: baseline 1, the default index, and the trading calendar the checks count days on.</summary>
    public const string IndexSymbol = "SPY";

    const int TradedDays = 60, MinTradedDays = 40, WarmUpSlackDays = 7;

    /// <summary>Under 60% of a period's members drawable, the run stops: in 2011 about 15% have no prices on Yahoo (survivorship).</summary>
    const double MinDrawable = 0.6;

    static readonly TestMoney Money = new();

    /// <summary>What the draw keeps of one symbol: no full history, except the compressed file until the draft is known.</summary>
    sealed class Member(string symbol)
    {
        public string Symbol { get; } = symbol;
        public string? Name, Sector, Problem;
        public DateOnly First, Last;
        public int Days;
        public byte[]? File;
        public IReadOnlyList<PriceWarning> Warnings = [];
        public readonly Dictionary<DateOnly, decimal?> Traded = [];
        public readonly Dictionary<DateOnly, PeriodPrices> Prices = [];
    }

    DateOnly Today => DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);

    public async Task<DrawReport> DrawAsync(Action<JobProgress> progress, CancellationToken cancellationToken)
    {
        var periods = BacktestPeriods.For(Today);
        var membership = Sp500Membership.Embedded;
        var since = periods[0].WarmUpStart.AddDays(-WarmUpSlackDays);

        progress(new("Fetching SPY", 0, 1));
        var (indexBars, _, indexProblem) = await HistoryAsync(IndexSymbol, since, cancellationToken);
        if (indexBars.Count == 0) throw new BacktestDataException($"Yahoo gave no prices for {IndexSymbol}: {indexProblem}");
        var calendar = indexBars.Select(b => b.Date).ToList();
        var buyDays = periods.ToDictionary(p => p.Start, p => SameStocksMonthly.BuyDays(calendar, p, Money));

        // Every member of every period's start, as the symbol Yahoo knows it by on that day.
        var entries = periods.SelectMany(p => membership.On(p.Start).Select(t => (Period: p, Ticker: t, Yahoo: membership.Resolve(t, p.Start)))).ToList();
        var symbols = entries.Select(e => e.Yahoo.Symbol).OfType<string>().Distinct().Order(StringComparer.Ordinal).ToList();
        var members = new Dictionary<string, Member>();
        for (var i = 0; i < symbols.Count; i++)
        {
            progress(new("Fetching the members' prices", i, symbols.Count));
            members[symbols[i]] = await MemberAsync(symbols[i], since, periods, calendar, buyDays, membership, cancellationToken);
        }
        progress(new("Drawing the test draft", 0, 1));

        var missing = new Dictionary<(string Ticker, MissingReason Reason), (string? Symbol, List<string> Periods, string Detail)>();
        var eligible = new List<(BacktestPeriod Period, IReadOnlyList<DrawCandidate> Eligible)>();
        var noNameCheck = new Dictionary<(string Ticker, string Symbol), List<string>>();
        // Each company's share class, chosen the first time it is drawable and kept after, so a drafted class never loses its seat
        // to another stock because its other class traded more in a later period.
        var classOf = new Dictionary<string, string>();
        foreach (var period in periods)
        {
            var candidates = new List<DrawCandidate>();
            foreach (var (_, ticker, resolved) in entries.Where(e => e.Period == period))
            {
                var member = resolved.Symbol is { } symbol ? members[symbol] : null;
                if (Unfit(period, ticker, resolved, member, membership) is { } unfit)
                {
                    var key = (ticker, unfit.Reason);
                    if (!missing.TryGetValue(key, out var seen)) missing[key] = seen = (resolved.Symbol, [], unfit.Detail);
                    seen.Periods.Add(period.Label);
                }
                else if (candidates.FirstOrDefault(c => c.Symbol == member!.Symbol) is { } same)
                {
                    // Two tickers for one Yahoo symbol on the same day (a map row each): the stock counts once.
                    var key = (ticker, MissingReason.OtherClass);
                    if (!missing.TryGetValue(key, out var seen)) missing[key] = seen = (same.Symbol, [], $"Same Yahoo symbol as {same.Ticker}, which is drawn instead.");
                    seen.Periods.Add(period.Label);
                }
                else candidates.Add(new(ticker, member!.Symbol, member.Sector!, member.Traded[period.Start]!.Value));
            }
            // One share class per company (GOOG and GOOGL, FOX and FOXA), so no company holds two seats: the class chosen before if it is
            // still drawable, else the one most traded now.
            foreach (var company in candidates.GroupBy(c => membership.Company(c.Ticker, period.Start)).ToList())
            {
                var kept = company.FirstOrDefault(c => classOf.GetValueOrDefault(company.Key) == c.Symbol)
                    ?? company.OrderByDescending(c => c.TradedValue).ThenBy(c => c.Symbol, StringComparer.Ordinal).First();
                classOf[company.Key] = kept.Symbol;
                foreach (var other in company.Where(c => c != kept))
                {
                    candidates.Remove(other);
                    var key = (other.Ticker, MissingReason.OtherClass);
                    if (!missing.TryGetValue(key, out var seen)) missing[key] = seen = (other.Symbol, [], $"Another share class of the company, {kept.Ticker}, is drawn instead.");
                    seen.Periods.Add(period.Label);
                }
            }
            // Drawable without a name check: listed, so a reused ticker among them can be caught by eye.
            foreach (var c in candidates.Where(c => membership.Resolve(c.Ticker, period.Start).Note is null
                && (membership.WikipediaName(c.Ticker, period.Start) is null || members[c.Symbol].Name is null)))
            {
                if (!noNameCheck.TryGetValue((c.Ticker, c.Symbol), out var labels)) noNameCheck[(c.Ticker, c.Symbol)] = labels = [];
                labels.Add(period.Label);
            }
            // A pool this small means Yahoo failed in a way it didn't say, not that the members are gone: draw nothing.
            var asked = entries.Count(e => e.Period == period);
            if (candidates.Count < asked * MinDrawable)
                throw new BacktestDataException($"Only {candidates.Count} of {asked} members of {period.Label} can be drawn, so Yahoo may be refusing: nothing was saved. See the server log, and try again later.");
            eligible.Add((period, candidates));
        }

        var memberSymbols = periods.ToDictionary(p => p, p => (IReadOnlySet<string>)entries.Where(e => e.Period == p).Select(e => e.Yahoo.Symbol).OfType<string>().ToHashSet());
        var drafts = DraftDraw.Draw(eligible, members: p => memberSymbols[p]);
        var drafted = drafts.SelectMany(d => d.Stocks).Select(s => s.Symbol).Distinct().Order(StringComparer.Ordinal).ToList();
        var histories = drafted.ToDictionary(s => s, s => PriceFile.Read(members[s].File!));

        var reports = new List<DrawPeriodReport>();
        for (var i = 0; i < periods.Count; i++)
        {
            var (period, draft, pool) = (periods[i], drafts[i], eligible[i].Eligible);
            var days = buyDays[period.Start];
            var draftRate = SameStocksMonthly.Rate([.. draft.Stocks.Select(s => members[s.Symbol].Prices[period.Start])], days, period, Money);
            var allRate = SameStocksMonthly.Rate([.. pool.Select(c => members[c.Symbol].Prices[period.Start])], days, period, Money);
            var fja = membership.On(period.Start);
            var wikipedia = membership.WikipediaOn(period.Start);
            reports.Add(new(period.Label, period.Start, period.End, period.Partial, fja.Count, pool.Count,
                new(fja.Count, wikipedia.Count, [.. fja.Except(wikipedia)], [.. wikipedia.Except(fja)]),
                [.. pool.GroupBy(c => c.Sector).OrderBy(g => g.Key, StringComparer.Ordinal)
                    .Select(g => new SectorSeats(g.Key, g.Count(), draft.Stocks.Count(s => s.Sector == g.Key)))],
                draft.Stocks, draft.Kept.Count, draft.Replaced, draft.Notes,
                new(draftRate, allRate, draftRate is { } d && allRate is { } a && Math.Abs(d - a) <= RepresentativeGap)));
        }

        progress(new("Checking the candidates", 0, 1));
        var candidatesPerYear = new List<CandidateYear>();
        foreach (var template in (await templates.AllAsync(cancellationToken)).Where(t => t.InvestsIn == InvestsIn.StockList && TemplateRules.Supports(t.Key)))
        {
            using var parameters = template.ParamsJson();
            var stocks = periods.Select((p, i) => (p, (IReadOnlyList<DraftHistory>)drafts[i].Stocks
                .Select(s => new DraftHistory(s.Symbol, histories[s.Symbol].Where(b => b.Date >= p.WarmUpStart).ToList())).ToList())).ToList();
            candidatesPerYear.AddRange(CandidateCheck.Count(template.Key, parameters, template.Budget.MaxSteps ?? 1, stocks));
        }

        var now = time.GetUtcNow();
        var month = Month(now);
        var folder = store.SnapshotFolder(month, now);
        var draw = new DrawReport(now, DraftDraw.DefaultSeed, DraftDraw.DefaultSize, membership.Hash, membership.LastRow, reports,
            [.. missing.OrderBy(m => m.Key.Ticker, StringComparer.Ordinal)
                .Select(m => new MissingMember(m.Key.Ticker, m.Value.Symbol, m.Value.Periods, m.Key.Reason, m.Value.Detail))],
            // A map row that only vouches for a name (IBM stays IBM) isn't a rename.
            [.. entries.Where(e => e.Yahoo.Note is not null && e.Yahoo.Symbol != e.Ticker).Select(e => new RenamedTicker(e.Ticker, e.Yahoo.Symbol, e.Yahoo.Note!)).Distinct()
                .OrderBy(r => r.Ticker, StringComparer.Ordinal)],
            [.. noNameCheck.OrderBy(u => u.Key.Ticker, StringComparer.Ordinal)
                .Select(u => new UncheckedName(u.Key.Ticker, u.Key.Symbol, members[u.Key.Symbol].Name, u.Value))],
            candidatesPerYear, [.. drafted.SelectMany(s => members[s].Warnings)], symbols.Count, folder);

        var known = drafted.ToDictionary(s => s, s => (Name: members[s].Name, Bars: histories[s]));
        known[IndexSymbol] = (null, indexBars);
        var (snapshot, manifest) = await TakeSnapshotAsync(draw, month, known, progress, cancellationToken);
        await store.SaveDrawAsync(draw, cancellationToken);
        await store.SaveSnapshotAsync(snapshot, manifest, cancellationToken);
        logger.LogInformation("Drew the backtest's test draft: {Symbols} symbols asked, {Missing} members missing; snapshot {Month}.",
            symbols.Count, draw.Missing.Count, snapshot.Month);
        return draw;
    }

    /// <summary>The month's snapshot of the latest draw's stocks, SPY and the starter lists.</summary>
    public async Task<SnapshotReport> SnapshotAsync(Action<JobProgress> progress, CancellationToken cancellationToken)
    {
        var draw = await store.DrawAsync(cancellationToken) ?? throw new BacktestDataException("Draw the test draft first: a snapshot holds its stocks.");
        var started = time.GetUtcNow();
        var month = Month(started);
        var (snapshot, manifest) = await TakeSnapshotAsync(draw with { Snapshot = store.SnapshotFolder(month, started) }, month, [], progress, cancellationToken);
        await store.SaveSnapshotAsync(snapshot, manifest, cancellationToken);
        logger.LogInformation("Took the backtest's snapshot {Month}: {Tickers} tickers.", snapshot.Month, snapshot.Tickers.Count);
        return snapshot;
    }

    static string Month(DateTimeOffset at) => at.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    /// <summary>
    /// Writes a price file per ticker to the draw's snapshot folder and returns the report and the manifest, for the caller to save last.
    /// <paramref name="known"/> are histories the draw already holds; the others are fetched now.
    /// </summary>
    async Task<(SnapshotReport, object)> TakeSnapshotAsync(DrawReport draw, string month, Dictionary<string, (string? Name, IReadOnlyList<PriceBar> Bars)> known,
        Action<JobProgress> progress, CancellationToken cancellationToken)
    {
        // From the draw's own first warm-up: in a later year today's periods start a year later.
        var since = draw.Periods[0].Start.AddMonths(-BacktestPeriods.WarmUpMonths).AddDays(-WarmUpSlackDays);
        var roles = new SortedDictionary<string, List<SnapshotRole>>(StringComparer.Ordinal);
        void Add(string symbol, SnapshotRole role)
        {
            if (!roles.TryGetValue(symbol, out var list)) roles[symbol] = list = [];
            if (!list.Contains(role)) list.Add(role);
        }
        foreach (var symbol in draw.Periods.SelectMany(p => p.Draft).Select(s => s.Symbol)) Add(symbol, SnapshotRole.Draft);
        Add(IndexSymbol, SnapshotRole.Index);
        var starterIds = (await templates.AllAsync(cancellationToken)).SelectMany(t => t.Watches.AssetIds).Distinct().ToList();
        foreach (var symbol in await db.Assets.AsNoTracking().Where(a => starterIds.Contains(a.Id)).Select(a => a.Symbol).ToListAsync(cancellationToken))
            Add(symbol, SnapshotRole.Starter);

        var calendar = known.TryGetValue(IndexSymbol, out var index) ? index.Bars.Select(b => b.Date).ToList() : null;
        var tickers = new List<SnapshotTicker>();
        var warnings = new List<PriceWarning>();
        var histories = new List<(string Symbol, IReadOnlyList<PriceBar> Bars)>();
        var n = 0;
        // SPY first: its days are the calendar the others' gaps are counted on.
        foreach (var (symbol, symbolRoles) in roles.OrderBy(r => r.Key != IndexSymbol))
        {
            progress(new("Taking the snapshot", n++, roles.Count));
            var (name, bars, problem) = known.TryGetValue(symbol, out var held) ? (held.Name, held.Bars, (string?)null) : await FetchAsync(symbol, since, cancellationToken);
            if (symbol == IndexSymbol && bars.Count == 0) throw new BacktestDataException($"Yahoo gave no prices for {IndexSymbol}: {problem}");
            calendar ??= [.. bars.Select(b => b.Date)];
            if (bars.Count > 0)
            {
                await store.PutPricesAsync(draw.Snapshot, symbol, PriceFile.Write(bars), cancellationToken);
                histories.Add((symbol, bars));
            }
            tickers.Add(new(symbol, name, symbolRoles, bars.Count > 0 ? bars[0].Date : null, bars.Count > 0 ? bars[^1].Date : null, bars.Count, problem));
        }
        foreach (var (symbol, bars) in histories) warnings.AddRange(PriceQuality.Check(symbol, bars, calendar!));

        // The month the caller named the folder by, read once: a run that crosses midnight at a month's end stays in one month.
        var snapshot = new SnapshotReport(month, time.GetUtcNow(), draw.Snapshot, draw.DrawnAt, draw.MembershipHash, tickers, warnings);
        var manifest = new
        {
            snapshot.Month, snapshot.TakenAt, draw.DrawnAt, draw.Seed, draw.Size, draw.MembershipHash, draw.MembershipLastRow,
            Sources = Sp500Membership.Sources(),
            Periods = draw.Periods.Select(p => new { p.Label, p.Start, p.End, p.Partial, Draft = p.Draft.Select(d => d.Symbol) }),
            draw.Renamed,
            Missing = draw.Missing.Select(m => new { m.Ticker, m.Symbol, m.Periods, m.Reason }),
            Files = tickers.Where(t => t.Days > 0).Select(t => new { t.Symbol, t.Roles, t.First, t.Last, t.Days, Key = BacktestStore.PriceKey(draw.Snapshot, t.Symbol) }),
            Problems = tickers.Where(t => t.Problem is not null).Select(t => new { t.Symbol, t.Problem }),
        };
        return (snapshot, manifest);
    }

    /// <summary>
    /// One symbol for the draw: its history, name and sector, and from them its first and last day, its warnings, and per period the
    /// traded value before the start and the prices the representative check needs. Then the history is kept only compressed.
    /// </summary>
    async Task<Member> MemberAsync(string symbol, DateOnly since, IReadOnlyList<BacktestPeriod> periods, IReadOnlyList<DateOnly> calendar,
        IReadOnlyDictionary<DateOnly, IReadOnlyList<DateOnly>> buyDays, Sp500Membership membership, CancellationToken cancellationToken)
    {
        var member = new Member(symbol);
        var (bars, raw, problem) = await HistoryAsync(symbol, since, cancellationToken);
        member.Problem = problem;
        if (bars.Count == 0) return member;
        member.Name = raw.Name;
        member.Sector = await SectorAsync(symbol, cancellationToken) ?? membership.FallbackSector(symbol);
        (member.First, member.Last, member.Days) = (bars[0].Date, bars[^1].Date, bars.Count);
        member.Warnings = PriceQuality.Check(symbol, bars, calendar);
        foreach (var period in periods)
        {
            member.Traded[period.Start] = TradedValue(raw.Raw, calendar, period.Start);
            member.Prices[period.Start] = PeriodPrices.From(bars, buyDays[period.Start], period.End);
        }
        member.File = PriceFile.Write(bars);
        return member;
    }

    /// <summary>Why a member can't be drawn on the period's start, or null when it can.</summary>
    static (MissingReason Reason, string Detail)? Unfit(BacktestPeriod period, string ticker, YahooSymbol resolved, Member? member, Sp500Membership membership)
    {
        if (member is null) return (MissingReason.NoSymbol, resolved.Note ?? "No Yahoo symbol.");
        if (member.Days == 0) return (MissingReason.NoPrices, member.Problem ?? "Yahoo has no prices.");
        // A row of the symbol map is someone's check that the symbol is the company; without one, the names must agree.
        if (resolved.Note is null && membership.WikipediaName(ticker, period.Start) is { } wikipedia && member.Name is { } yahooName && !NamesMatch(wikipedia, yahooName))
            return (MissingReason.NameDiffers, $"Yahoo: {yahooName}; Wikipedia: {wikipedia}");
        if (member.Sector is null) return (MissingReason.NoSector, "Yahoo gives no sector.");
        if (member.First > period.WarmUpStart.AddDays(WarmUpSlackDays)) return (MissingReason.StartsLate, $"Prices from {member.First:yyyy-MM-dd}.");
        if (member.Last < period.Start || member.Traded[period.Start] is null)
            return (MissingReason.NoPricesAtStart, $"Prices {member.First:yyyy-MM-dd} to {member.Last:yyyy-MM-dd}.");
        return null;
    }

    /// <summary>The average daily close × volume (split-adjusted, not dividend-adjusted: the money traded) over the 60 trading days before the start.</summary>
    static decimal? TradedValue(IReadOnlyList<Candle> raw, IReadOnlyList<DateOnly> calendar, DateOnly start)
    {
        var window = calendar.Where(d => d < start).TakeLast(TradedDays).ToList();
        if (window.Count == 0) return null;
        var days = raw.Where(c => c.Date >= window[0] && c.Date <= window[^1]).ToList();
        return days.Count >= MinTradedDays ? days.Average(c => c.Close * c.Volume) : null;
    }

    static readonly HashSet<string> NameNoise = ["inc", "incorporated", "corp", "corporation", "co", "company", "the", "plc", "ltd", "limited",
        "group", "holdings", "holding", "class", "sa", "nv", "and", "com", "trust"];

    /// <summary>
    /// Whether two names are plausibly the same company: after dropping punctuation and words like "Inc." or "Corporation", one starts
    /// with the other's first word ("C. R. Bard, Inc." and "CR Bard"). Loose on purpose: it is there to catch a ticker reused by another
    /// company (Sprint's S, SentinelOne's today), not spelling.
    /// </summary>
    internal static bool NamesMatch(string a, string b)
    {
        static List<string> Words(string name)
        {
            var text = new StringBuilder();
            foreach (var c in name.Normalize(NormalizationForm.FormD).ToLowerInvariant())
                if (char.IsLetterOrDigit(c)) text.Append(c);
                else if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) text.Append(c == '&' ? " and " : c == '.' ? "" : " ");
            return [.. text.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => !NameNoise.Contains(w))];
        }
        var (x, y) = (Words(a), Words(b));
        if (x.Count == 0 || y.Count == 0) return true;
        var (joinedX, joinedY) = (string.Concat(x), string.Concat(y));
        // The first word, or the first 3 letters when it is shorter: "A. O. Smith" mustn't match every name that starts with "a".
        static string Key(List<string> words, string joined) => words[0].Length >= 3 ? words[0] : joined[..Math.Min(3, joined.Length)];
        return joinedX.StartsWith(Key(y, joinedY), StringComparison.Ordinal) || joinedY.StartsWith(Key(x, joinedX), StringComparison.Ordinal);
    }

    async Task<(IReadOnlyList<PriceBar> Bars, DailyHistory Raw, string? Problem)> HistoryAsync(string symbol, DateOnly since, CancellationToken cancellationToken)
    {
        var empty = new DailyHistory([], [], null);
        try
        {
            var history = await WithRetriesAsync(() => yahoo.HistoryAsync(symbol, since, cancellationToken), symbol, cancellationToken);
            return history.Adjusted.Count == 0
                ? ([], empty, "Yahoo has no prices.")
                : ([.. history.Adjusted.Select(c => new PriceBar(c.Date, c.Open, c.High, c.Low, c.Close, c.Volume))], history, null);
        }
        catch (MarketDataException error)
        {
            // Yahoo's answer for this symbol (no data, not found); a failure of Yahoo itself stopped the run in WithRetriesAsync.
            return ([], empty, error.Message);
        }
    }

    async Task<(string? Name, IReadOnlyList<PriceBar> Bars, string? Problem)> FetchAsync(string symbol, DateOnly since, CancellationToken cancellationToken)
    {
        var (bars, raw, problem) = await HistoryAsync(symbol, since, cancellationToken);
        return (raw.Name, bars, problem);
    }

    /// <summary>The stock's sector as Yahoo's search gives it, as for added assets; null without one (a fund, or no exact match).</summary>
    async Task<string?> SectorAsync(string symbol, CancellationToken cancellationToken)
    {
        try
        {
            var matches = await WithRetriesAsync(() => yahoo.SearchAsync(symbol, cancellationToken), symbol, cancellationToken);
            return matches.FirstOrDefault(m => string.Equals(m.Symbol, symbol, StringComparison.OrdinalIgnoreCase))?.Sector;
        }
        catch (MarketDataException)
        {
            return null;
        }
    }

    /// <summary>
    /// Whether Yahoo failed rather than answered: refused us (401, 403 when it blocks, 429 when asked too often), timed out (408, or no
    /// answer), failed itself (5xx, 999) or couldn't be reached. Any other answer (404, 400 or 422 for a symbol or range it has no data for,
    /// its own "no data", an answer we can't read) says something about the member, and the same symbol would fail every draw.
    /// </summary>
    static bool YahooFailed(MarketDataException error) =>
        error.Status is 401 or 403 or 408 or 429 or >= 500 || error.InnerException is HttpRequestException or TaskCanceledException;

    /// <summary>
    /// One request to Yahoo, with the pause after it, asked again after Yahoo failed. Still failing after the retries, the run
    /// stops: counting the member as missing would draw from a smaller pool, so a throttled stretch would change the draft.
    /// </summary>
    async Task<T> WithRetriesAsync<T>(Func<Task<T>> request, string symbol, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await request();
            }
            catch (MarketDataException error) when (YahooFailed(error) && attempt < options.Value.Retries.Length)
            {
                logger.LogInformation("Yahoo failed for {Symbol} ({Reason}): asking again in {Wait}.", symbol, error.Message, options.Value.Retries[attempt]);
                await Task.Delay(options.Value.Retries[attempt], time, cancellationToken);
            }
            catch (MarketDataException error) when (YahooFailed(error))
            {
                throw new BacktestDataException($"Yahoo kept failing at {symbol} ({error.Message}), so it stopped; the stored draw and snapshot are unchanged. Try again later.");
            }
            finally
            {
                await Task.Delay(options.Value.Pause, time, cancellationToken);
            }
        }
    }
}
