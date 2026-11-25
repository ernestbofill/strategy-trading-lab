using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StrategyTradingLab.Api.Backtest;

/// <summary>A ticker as Yahoo knows it on a date: its symbol, or null when the ticker belongs to another company today; the map's note when a row applied.</summary>
public record YahooSymbol(string Ticker, string? Symbol, string? Note);

/// <summary>
/// The S&amp;P 500's members on any date, from the files in Backtest/Data (their README says where each comes from): fja05680/sp500's
/// membership, Wikipedia's list and changes as the second list and for the names, and the symbol map to Yahoo's tickers.
/// </summary>
public sealed partial class Sp500Membership
{
    record Interval(string Ticker, DateOnly Start, DateOnly? End);
    record Change(DateOnly Date, string? Added, string? AddedName, string? Removed, string? RemovedName);
    record MapRow(string Ticker, DateOnly? Until, string? Yahoo, string Note);

    readonly List<Interval> intervals;
    readonly Dictionary<string, string> current;
    readonly List<Change> changes;
    readonly List<MapRow> map;
    readonly Dictionary<string, string> sectors;

    /// <summary>A SHA-256 of the files, so a draw records which membership it used and the page sees when it changed.</summary>
    public string Hash { get; }

    /// <summary>The date of fja's last row: the membership isn't known after it.</summary>
    public DateOnly LastRow { get; }

    Sp500Membership(string membership, string currentList, string changeList, string symbolMap, string sectorList, string hash)
    {
        sectors = Csv.Rows(sectorList).ToDictionary(r => r[0], r => r[1]);
        intervals = [.. Csv.Rows(membership).Select(r => new Interval(r[0], Date(r[1]), r[2].Length == 0 ? null : Date(r[2])))];
        current = Csv.Rows(currentList).ToDictionary(r => r[0], r => r[1]);
        changes = [.. Csv.Rows(changeList).Select(r => new Change(Date(r[0]), Text(r[1]), Text(r[2]), Text(r[3]), Text(r[4])))];
        map = [.. Csv.Rows(symbolMap).Select(r => new MapRow(r[0], r[1].Length == 0 ? null : Date(r[1]), Text(r[2]), r[3]))];
        LastRow = intervals.Max(i => i.End is { } end && end > i.Start ? end : i.Start);
        Hash = hash;
    }

    static DateOnly Date(string text) => DateOnly.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    static string? Text(string text) => text.Length == 0 ? null : text;

    /// <summary>From the CSV files' text, header rows included.</summary>
    public static Sp500Membership Parse(string membership, string currentList, string changeList, string symbolMap, string sectorList = "symbol,sector,note\n") =>
        new(membership, currentList, changeList, symbolMap, sectorList,
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(membership + currentList + changeList + symbolMap + sectorList))));

    static readonly Lazy<Sp500Membership> embedded = new(() =>
        Parse(Resource("sp500-membership.csv"), Resource("wikipedia-current.csv"), Resource("wikipedia-changes.csv"), Resource("symbol-map.csv"),
            Resource("sectors.csv")));

    /// <summary>
    /// The sector of a Yahoo symbol whose search no longer lists it (delisted or renamed lately) while its prices are still there, from
    /// sectors.csv, in Yahoo's sector names; null when the file has none. Used only when Yahoo gives no sector.
    /// </summary>
    public string? FallbackSector(string symbol) => sectors.GetValueOrDefault(symbol);

    /// <summary>The files embedded in the API.</summary>
    public static Sp500Membership Embedded => embedded.Value;

    /// <summary>Backtest/Data/sources.json: what the files were made from.</summary>
    public static JsonElement Sources() => JsonDocument.Parse(Resource("sources.json")).RootElement.Clone();

    static string Resource(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"backtest/{name}")
            ?? throw new InvalidOperationException($"backtest/{name} isn't embedded.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>fja's members on <paramref name="date"/>, sorted.</summary>
    public IReadOnlyList<string> On(DateOnly date) =>
        [.. intervals.Where(i => i.Start <= date && (i.End is not { } end || date < end)).Select(i => i.Ticker).Distinct().Order(StringComparer.Ordinal)];

    /// <summary>The second list on <paramref name="date"/>: Wikipedia's members today, with its changes after that date undone.</summary>
    public IReadOnlyList<string> WikipediaOn(DateOnly date)
    {
        var members = current.Keys.ToHashSet();
        foreach (var change in changes.Where(c => c.Date > date).OrderByDescending(c => c.Date))
        {
            if (change.Added is { } added) members.Remove(added);
            if (change.Removed is { } removed) members.Add(removed);
        }
        return [.. members.Order(StringComparer.Ordinal)];
    }

    /// <summary>
    /// The company's name on <paramref name="date"/> as Wikipedia gives it: the name it had when it was removed after that date, else
    /// its name today if it is a member, else the name it was added with. Null when Wikipedia doesn't list the ticker.
    /// </summary>
    public string? WikipediaName(string ticker, DateOnly date) =>
        changes.Where(c => c.Removed == ticker && c.Date > date).OrderBy(c => c.Date).Select(c => c.RemovedName).FirstOrDefault()
        ?? current.GetValueOrDefault(ticker)
        ?? changes.Where(c => c.Added == ticker && c.Date <= date).OrderByDescending(c => c.Date).Select(c => c.AddedName).FirstOrDefault();

    /// <summary>
    /// Which company the ticker is on <paramref name="date"/>, so two share classes count once: Wikipedia's name without its class
    /// ("Alphabet Inc. (Class A)" and "(Class C)", "Discovery, Inc." twice), lower case; the ticker when Wikipedia doesn't name it.
    /// </summary>
    public string Company(string ticker, DateOnly date) =>
        WikipediaName(ticker, date) is { } name ? ClassSuffix().Replace(name, "").Trim().ToLowerInvariant() : ticker;

    [System.Text.RegularExpressions.GeneratedRegex(@"\s*\(Class [A-Z]\)", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex ClassSuffix();

    /// <summary>
    /// The ticker's Yahoo symbol on <paramref name="date"/>: the symbol map's row for that date, or the ticker with <c>.</c> written <c>-</c>
    /// (BRK.B → BRK-B). A row without a symbol means the ticker is another company's today: the symbol is null.
    /// </summary>
    public YahooSymbol Resolve(string ticker, DateOnly date)
    {
        var row = map.Where(m => m.Ticker == ticker && (m.Until is not { } until || date < until)).OrderBy(m => m.Until ?? DateOnly.MaxValue).FirstOrDefault();
        return row is null ? new(ticker, ticker.Replace('.', '-'), null) : new(ticker, row.Yahoo, row.Note);
    }
}

/// <summary>Reads the data files' CSV: a header row, fields separated by commas, quoted when they hold one ("Okta, Inc.").</summary>
static class Csv
{
    public static IEnumerable<string[]> Rows(string text) =>
        text.Split('\n').Skip(1).Where(line => line.Trim().Length > 0).Select(line => Fields(line.TrimEnd('\r')));

    static string[] Fields(string line)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { field.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else field.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { fields.Add(field.ToString()); field.Clear(); }
            else field.Append(c);
        }
        fields.Add(field.ToString());
        return [.. fields];
    }
}
