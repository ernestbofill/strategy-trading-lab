using Microsoft.EntityFrameworkCore;
using Npgsql;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.MarketData;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Assets;

/// <summary>
/// What adding a symbol did: <c>Created</c> is false when the asset was already there. <c>Problem</c> says why its
/// prices or its currency's exchange rate didn't load; the row is kept, and adding it again tries again.
/// </summary>
public record AddedAsset(Asset Asset, bool Created, string? Problem);

/// <summary>The price source has no stock, fund or index with that symbol.</summary>
public class AssetNotFoundException(string message) : Exception(message);

/// <summary>
/// Adds a stock, fund or index by its Yahoo symbol at run time, as a plain row: no migration, no deploy (ROADMAP item 13).
/// The search gives the name, exchange, type, sector and industry, the chart the currency. The whole price history, and
/// the exchange rate of a currency no asset had yet, are fetched at once, outside the daily refresh.
/// </summary>
public sealed class AssetCatalog(AppDbContext db, IMarketDataSource source, PriceRefresher refresher)
{
    public const int MaxSymbolLength = 20;

    public async Task<AddedAsset> AddAsync(string symbol, Guid? addedBy, CancellationToken cancellationToken)
    {
        var wanted = symbol.Trim().ToUpperInvariant();
        var asset = await FindAsync(wanted, cancellationToken);
        var created = false;
        if (asset is null)
        {
            // Only the exact symbol: the search also answers names and near matches, and adding one of those by mistake
            // would leave a row nobody asked for.
            var match = (await source.SearchAsync(wanted, cancellationToken))
                .FirstOrDefault(m => string.Equals(m.Symbol, wanted, StringComparison.OrdinalIgnoreCase))
                ?? throw new AssetNotFoundException($"Yahoo has no stock, fund or index with the symbol {wanted}.");
            var kind = KindOf(match.Type)
                ?? throw new AssetNotFoundException($"Yahoo lists {match.Symbol} as {match.Type.ToLowerInvariant()}: only stocks, funds and indexes can be added.");
            var currency = await source.CurrencyAsync(match.Symbol, cancellationToken);

            asset = new Asset
            {
                Symbol = match.Symbol.ToUpperInvariant(),
                Exchange = Mic(match.Exchange),
                Name = match.Name,
                Kind = kind,
                Currency = currency,
                Sector = kind == AssetKind.Stock ? match.Sector : null,
                Industry = kind == AssetKind.Stock ? match.Industry : null,
                AddedBy = addedBy,
            };
            asset.Texts.Add(new AssetText { Locale = TemplateTexts.DefaultLocale, What = What(asset) });
            db.Assets.Add(asset);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                created = true;
            }
            catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // Added by someone else meanwhile: use theirs.
                db.ChangeTracker.Clear();
                asset = await FindAsync(wanted, cancellationToken) ?? throw new InvalidOperationException($"{wanted} clashed with a row that isn't there.");
            }
        }
        return new AddedAsset(asset, created, await LoadPricesAsync(asset, cancellationToken));
    }

    Task<Asset?> FindAsync(string symbol, CancellationToken cancellationToken) =>
        db.Assets.AsNoTracking().Include(a => a.Texts).FirstOrDefaultAsync(a => a.Symbol.ToUpper() == symbol, cancellationToken);

    /// <summary>The history if none is stored, and the exchange rate if its currency has none. Null when both are in.</summary>
    async Task<string?> LoadPricesAsync(Asset asset, CancellationToken cancellationToken)
    {
        var problems = new List<string>();
        if (!await db.Prices.AnyAsync(p => p.AssetId == asset.Id, cancellationToken))
            await TryAsync(() => refresher.RefreshAssetAsync(asset, cancellationToken), problems);
        if (asset.Currency != PriceRefresher.BaseCurrency
            && !await db.FxRates.AnyAsync(r => r.Base == PriceRefresher.BaseCurrency && r.Quote == asset.Currency, cancellationToken))
            await TryAsync(() => refresher.RefreshRateAsync(asset.Currency, cancellationToken), problems);
        return problems.Count == 0 ? null : string.Join(" ", problems);
    }

    /// <summary>Any failure but a cancellation is a problem to report, as in the daily refresh: the row is already saved.</summary>
    async Task TryAsync(Func<Task<RefreshResult>> load, List<string> problems)
    {
        try
        {
            await load();
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            db.ChangeTracker.Clear();
            problems.Add(error is MarketDataException ? error.Message : $"The prices couldn't be stored: {error.Message}");
        }
    }

    /// <summary>Yahoo's type, or null for one the app doesn't follow (a currency, a future, a crypto coin…).</summary>
    public static AssetKind? KindOf(string type) => type.ToUpperInvariant() switch
    {
        "EQUITY" => AssetKind.Stock,
        "ETF" or "MUTUALFUND" => AssetKind.Fund,
        "INDEX" => AssetKind.Index,
        _ => null,
    };

    /// <summary>Yahoo's exchange code as the MIC the seeded assets use (template-assets.md); an unknown one is kept as Yahoo writes it.</summary>
    public static string Mic(string exchange) => exchange.ToUpperInvariant() switch
    {
        "NMS" or "NGM" or "NCM" or "NAS" => "XNAS",
        "NYQ" => "XNYS",
        "PCX" => "ARCX",
        "ASE" => "XASE",
        "BTS" => "BATS",
        "LSE" => "XLON",
        "PAR" => "XPAR",
        "AMS" => "XAMS",
        "BRU" => "XBRU",
        "LIS" => "XLIS",
        "GER" => "XETR",
        "FRA" => "XFRA",
        "MIL" => "XMIL",
        "MCE" => "XMAD",
        "EBS" => "XSWX",
        "VIE" => "XWBO",
        "CPH" => "XCSE",
        "STO" => "XSTO",
        "HEL" => "XHEL",
        "OSL" => "XOSL",
        "HKG" => "XHKG",
        "JPX" or "OSA" => "XJPX",
        "TOR" => "XTSE",
        "ASX" => "XASX",
        var code => code,
    };

    /// <summary>
    /// "What it does" for an added asset: a stock's sector and industry ("Technology · Software infrastructure"), or its
    /// name when the source has neither; a fund's or an index's name.
    /// </summary>
    public static string What(Asset asset)
    {
        if (asset.Kind != AssetKind.Stock) return asset.Name;
        var parts = new[] { asset.Sector, asset.Industry }.OfType<string>().Select(SentenceCase).Distinct().ToList();
        return parts.Count == 0 ? asset.Name : string.Join(" · ", parts);
    }

    /// <summary>"Software—Infrastructure" as "Software infrastructure": a capitalised word after the first is lowered, an acronym (REIT) kept.</summary>
    static string SentenceCase(string text)
    {
        var words = text.Replace('—', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', words.Select((word, i) =>
            i > 0 && word.Length > 1 && char.IsUpper(word[0]) && word[1..].All(c => !char.IsUpper(c)) ? word.ToLowerInvariant() : word));
    }
}
