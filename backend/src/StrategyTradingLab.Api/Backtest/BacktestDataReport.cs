namespace StrategyTradingLab.Api.Backtest;

/// <summary>Why a member on a period's start can't be drawn.</summary>
public enum MissingReason
{
    /// <summary>The symbol map says the ticker is another company's today.</summary>
    NoSymbol,
    /// <summary>Yahoo has no prices for the symbol (usually a delisted company).</summary>
    NoPrices,
    /// <summary>Yahoo's name doesn't match Wikipedia's for the ticker, and no row of the symbol map vouches for it.</summary>
    NameDiffers,
    /// <summary>Yahoo gives no sector, so it can't be seated.</summary>
    NoSector,
    /// <summary>Its prices start after the period's warm-up.</summary>
    StartsLate,
    /// <summary>Its prices stop before the period's start, or it barely traded in the 60 days before it.</summary>
    NoPricesAtStart,
    /// <summary>Another share class of the same company is drawn instead (the one most traded).</summary>
    OtherClass,
}

/// <summary>A member that can't be drawn, in the periods where it was a member.</summary>
public record MissingMember(string Ticker, string? Symbol, IReadOnlyList<string> Periods, MissingReason Reason, string Detail);

/// <summary>A ticker the symbol map sent to another Yahoo symbol, or to none.</summary>
public record RenamedTicker(string Ticker, string? Symbol, string Note);

/// <summary>A member that can be drawn without a name check: Wikipedia doesn't name it and no row of the symbol map vouches for it.</summary>
public record UncheckedName(string Ticker, string Symbol, string? YahooName, IReadOnlyList<string> Periods);

/// <summary>The two point-in-time lists on a period's start: fja's members missing from the Wikipedia list, and the other way round.</summary>
public record SecondListCheck(int Members, int WikipediaMembers, IReadOnlyList<string> OnlyFja, IReadOnlyList<string> OnlyWikipedia);

/// <summary>
/// The representative check: <i>same stocks, monthly</i> on the draft against the same on every eligible member, as yearly
/// money-weighted rates (fractions). It passes within <see cref="BacktestData.RepresentativeGap"/>.
/// </summary>
public record RepresentativeCheck(double? DraftRate, double? AllRate, bool Passed);

/// <summary>A sector's eligible members on a period's start and its seats in the draft that period.</summary>
public record SectorSeats(string Sector, int Eligible, int Seats);

/// <summary>One period of a draw: the members, how many can be drawn, the draft and the checks.</summary>
public record DrawPeriodReport(string Label, DateOnly Start, DateOnly End, bool Partial, int Members, int Eligible, SecondListCheck SecondList,
    IReadOnlyList<SectorSeats> Sectors, IReadOnlyList<DraftStock> Draft, int Kept, IReadOnlyList<DraftReplacement> Replaced,
    IReadOnlyList<string> Notes, RepresentativeCheck Representative);

/// <summary>
/// What a draw found (docs/backtest.md, *Data* and *How it is drawn*): per period the members, the coverage and the draft; the members
/// that can't be drawn and why; the renamed tickers; those drawn without a name check; the candidates per stock template and year; and
/// the drafted stocks' price warnings.
/// </summary>
public record DrawReport(DateTimeOffset DrawnAt, ulong Seed, int Size, string MembershipHash, DateOnly MembershipLastRow,
    IReadOnlyList<DrawPeriodReport> Periods, IReadOnlyList<MissingMember> Missing, IReadOnlyList<RenamedTicker> Renamed,
    IReadOnlyList<UncheckedName> Unchecked, IReadOnlyList<CandidateYear> Candidates, IReadOnlyList<PriceWarning> Warnings, int Symbols,
    string Snapshot);

/// <summary>Why a ticker is in a snapshot: a drafted stock, the S&amp;P 500 fund (baseline 1 and the default index) or a template's starter list.</summary>
public enum SnapshotRole { Draft, Index, Starter }

/// <summary>One ticker in a snapshot: its first and last day and how many days it has, or why Yahoo gave nothing.</summary>
public record SnapshotTicker(string Symbol, string? Name, IReadOnlyList<SnapshotRole> Roles, DateOnly? First, DateOnly? Last, int Days, string? Problem);

/// <summary>
/// A snapshot: the month's prices of the draft's stocks, SPY and the starter lists, one file per ticker under <c>Key</c>, with their
/// quality warnings. Each backtest result names the snapshot it used (its month).
/// </summary>
public record SnapshotReport(string Month, DateTimeOffset TakenAt, string Key, DateTimeOffset DrawnAt, string MembershipHash,
    IReadOnlyList<SnapshotTicker> Tickers, IReadOnlyList<PriceWarning> Warnings);
