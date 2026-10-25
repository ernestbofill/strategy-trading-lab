using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.MarketData;

public record AssetLookupResult(Guid Id, string Symbol, string Exchange, string Name, AssetKind Kind, string Currency,
    string What, bool Created, DateOnly? LatestPriceDate);

/// <summary>
/// Resolves a symbol to an asset row, creating the row and a first history when it is new. The asset text stays in the
/// default locale, so a later user-facing screen can show the same description in the active language.
/// </summary>
public sealed class AssetCatalog(AppDbContext db, IMarketDataSource source, PriceRefresher refresher)
{
    public async Task<IReadOnlyList<MarketQuote>> SearchAsync(string query, CancellationToken cancellationToken)
        => await source.SearchAsync(query, cancellationToken);

    public async Task<AssetLookupResult> EnsureAsync(string symbol, AssetKind? kind = null, CancellationToken cancellationToken = default)
    {
        var requested = (symbol ?? "").Trim();
        if (requested.Length == 0) throw new ValidationException("Type a symbol or company name.");

        var lookup = requested.ToUpperInvariant();
        var matches = await source.SearchAsync(requested, cancellationToken);
        var match = matches.FirstOrDefault(item => string.Equals(item.Symbol, requested, StringComparison.OrdinalIgnoreCase))
            ?? matches.FirstOrDefault();
        if (match is not null)
        {
            var exchange = NormalizeExchange(match.Exchange);
            var existing = await db.Assets.FirstOrDefaultAsync(asset => asset.Symbol.ToUpper() == lookup && asset.Exchange == exchange, cancellationToken);
            if (existing is not null)
            {
                await EnsureHistoryAsync(existing, cancellationToken);
                var latestPriceDate = await db.Prices.Where(price => price.AssetId == existing.Id).MaxAsync(price => (DateOnly?)price.Date, cancellationToken);
                var existingWhat = await db.AssetTexts.Where(text => text.AssetId == existing.Id && text.Locale == TemplateTexts.DefaultLocale)
                    .Select(text => text.What)
                    .FirstOrDefaultAsync(cancellationToken) ?? BuildWhat(existing.Kind, existing.Name, null);
                return new AssetLookupResult(existing.Id, existing.Symbol, existing.Exchange, existing.Name, existing.Kind, existing.Currency,
                    existingWhat, false, latestPriceDate);
            }

            var sameSymbol = await db.Assets.Where(asset => asset.Symbol.ToUpper() == lookup).ToListAsync(cancellationToken);
            if (sameSymbol.Count > 0)
            {
                var existingBySymbol = sameSymbol.FirstOrDefault(asset => string.Equals(asset.Exchange, exchange, StringComparison.OrdinalIgnoreCase));
                if (existingBySymbol is not null)
                {
                    await EnsureHistoryAsync(existingBySymbol, cancellationToken);
                    var latestPriceDate = await db.Prices.Where(price => price.AssetId == existingBySymbol.Id).MaxAsync(price => (DateOnly?)price.Date, cancellationToken);
                    var existingWhat = await db.AssetTexts.Where(text => text.AssetId == existingBySymbol.Id && text.Locale == TemplateTexts.DefaultLocale)
                        .Select(text => text.What)
                        .FirstOrDefaultAsync(cancellationToken) ?? BuildWhat(existingBySymbol.Kind, existingBySymbol.Name, null);
                    return new AssetLookupResult(existingBySymbol.Id, existingBySymbol.Symbol, existingBySymbol.Exchange, existingBySymbol.Name, existingBySymbol.Kind, existingBySymbol.Currency,
                        existingWhat, false, latestPriceDate);
                }

                throw new MarketDataException($"Multiple assets use {requested} on different exchanges; choose the exact exchange or symbol.");
            }
        }

        var existingSymbolOnly = await db.Assets.FirstOrDefaultAsync(asset => asset.Symbol.ToUpper() == lookup, cancellationToken);
        if (existingSymbolOnly is not null)
        {
            await EnsureHistoryAsync(existingSymbolOnly, cancellationToken);
            var latestPriceDate = await db.Prices.Where(price => price.AssetId == existingSymbolOnly.Id).MaxAsync(price => (DateOnly?)price.Date, cancellationToken);
            var existingWhat = await db.AssetTexts.Where(text => text.AssetId == existingSymbolOnly.Id && text.Locale == TemplateTexts.DefaultLocale)
                .Select(text => text.What)
                .FirstOrDefaultAsync(cancellationToken) ?? BuildWhat(existingSymbolOnly.Kind, existingSymbolOnly.Name, null);
            return new AssetLookupResult(existingSymbolOnly.Id, existingSymbolOnly.Symbol, existingSymbolOnly.Exchange, existingSymbolOnly.Name, existingSymbolOnly.Kind, existingSymbolOnly.Currency,
                existingWhat, false, latestPriceDate);
        }

        if (match is null) throw new MarketDataException($"Yahoo found no asset for {requested}.");

        var resolvedKind = ResolveKind(kind, match.QuoteType);
        var normalizedSymbol = match.Symbol.Trim();
        var asset = new Asset
        {
            Symbol = normalizedSymbol.ToUpperInvariant(),
            Exchange = NormalizeExchange(match.Exchange),
            Name = match.Name.Trim(),
            Kind = resolvedKind,
            Currency = (match.Currency ?? "USD").Trim().ToUpperInvariant(),
            Active = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Assets.Add(asset);
        await db.SaveChangesAsync(cancellationToken);

        var what = BuildWhat(resolvedKind, asset.Name, match);
        db.AssetTexts.Add(new AssetText
        {
            AssetId = asset.Id,
            Locale = TemplateTexts.DefaultLocale,
            What = what,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);

        await EnsureHistoryAsync(asset, cancellationToken);
        var latest = await db.Prices.Where(price => price.AssetId == asset.Id).MaxAsync(price => (DateOnly?)price.Date, cancellationToken);
        return new AssetLookupResult(asset.Id, asset.Symbol, asset.Exchange, asset.Name, asset.Kind, asset.Currency, what, true, latest);
    }

    async Task EnsureHistoryAsync(Asset asset, CancellationToken cancellationToken)
    {
        var hasHistory = await db.Prices.AnyAsync(price => price.AssetId == asset.Id, cancellationToken);
        if (!hasHistory)
        {
            await refresher.RefreshAssetAsync(asset, cancellationToken);
        }

        if (!string.Equals(asset.Currency, PriceRefresher.BaseCurrency, StringComparison.OrdinalIgnoreCase))
        {
            var hasRate = await db.FxRates.AnyAsync(rate => rate.Base == PriceRefresher.BaseCurrency && rate.Quote == asset.Currency, cancellationToken);
            if (!hasRate)
            {
                await refresher.RefreshRateAsync(asset.Currency, cancellationToken);
            }
        }
    }

    static string NormalizeExchange(string? exchange)
    {
        var normalized = (exchange ?? "").Trim();
        if (normalized.Length == 0) return normalized;

        var code = normalized.Trim().ToUpperInvariant();
        return code switch
        {
            "NMS" or "NASD" or "NASDAQ" => "XNAS",
            "NYS" or "NYSE" => "XNYS",
            "AMS" or "AEX" => "XAMS",
            "PAR" or "EPA" => "XPAR",
            "HKG" or "HKSE" or "HKEX" => "XHKG",
            "LSE" or "LON" => "XLON",
            "CSE" or "CPH" => "XCSE",
            "XETR" or "ETR" or "FRA" => "XETR",
            "JPX" or "TSE" or "TYO" => "XJPX",
            _ => code,
        };
    }

    static AssetKind ResolveKind(AssetKind? kind, string? quoteType) => kind ?? quoteType?.Trim() switch
    {
        "INDEX" => AssetKind.Index,
        "ETF" => AssetKind.Fund,
        "MUTUALFUND" => AssetKind.Fund,
        "FUND" => AssetKind.Fund,
        _ => AssetKind.Stock,
    };

    static string BuildWhat(AssetKind kind, string name, MarketQuote? match)
    {
        if (kind == AssetKind.Fund || kind == AssetKind.Index) return name;

        if (match is not null)
        {
            var parts = new[] { match.Sector, match.Industry }.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part!.Trim()).ToArray();
            if (parts.Length > 0) return string.Join(" · ", parts);
        }

        return name;
    }
}
