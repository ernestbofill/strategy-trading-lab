using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StrategyTradingLab.Api.Assets;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.MarketData;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Tests;

/// <summary>Adding an asset by its symbol at run time, with a fake price source (no network).</summary>
public class AssetCatalogTests
{
    /// <summary>Answers a search with <see cref="Matches"/>, and 30 days of prices for any symbol unless <see cref="PricesFail"/>.</summary>
    sealed class FakeSource : IMarketDataSource
    {
        public List<SymbolMatch> Matches { get; } = [];
        /// <summary>The euro by default, so no exchange rate is fetched (a fake one must never land on a real currency).</summary>
        public string Currency { get; set; } = PriceRefresher.BaseCurrency;
        public bool PricesFail { get; set; }
        public int Searches { get; private set; }

        public Task<IReadOnlyList<Candle>> DailyAsync(string symbol, DateOnly? since, bool adjusted, CancellationToken cancellationToken)
        {
            if (PricesFail) throw new MarketDataException($"Yahoo answered HTTP 429 for {symbol}.");
            IReadOnlyList<Candle> days = [.. Enumerable.Range(since is not null ? 0 : 20, since is not null ? 30 : 10)
                .Select(day => new Candle(new DateOnly(2026, 9, 1).AddDays(day), 100 + day, 101 + day, 99 + day, 100 + day, 1000))];
            return Task.FromResult(days);
        }

        public Task<IReadOnlyList<SymbolMatch>> SearchAsync(string query, CancellationToken cancellationToken)
        {
            Searches++;
            return Task.FromResult<IReadOnlyList<SymbolMatch>>(Matches);
        }

        public Task<string> CurrencyAsync(string symbol, CancellationToken cancellationToken) => Task.FromResult(Currency);
    }

    static string NewSymbol() => $"T{Guid.NewGuid():N}"[..10].ToUpperInvariant();

    /// <summary>A catalog on the test database; disposing it deletes the assets and rates it added.</summary>
    sealed class Catalog : IAsyncDisposable
    {
        readonly AccountsApi api = new();
        readonly IServiceScope scope;
        readonly List<string> currencies = [];
        public AppDbContext Db { get; }
        public FakeSource Source { get; } = new();
        public AssetCatalog Assets { get; }

        public Catalog()
        {
            scope = api.Services.CreateScope();
            Db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assets = new AssetCatalog(Db, Source, new PriceRefresher(Db, Source, NullLogger<PriceRefresher>.Instance) { Pause = TimeSpan.Zero });
        }

        public string NewCurrency()
        {
            // XQ…: no real currency's code.
            var code = $"XQ{(char)('A' + Random.Shared.Next(26))}";
            currencies.Add(code);
            return code;
        }

        public async ValueTask DisposeAsync()
        {
            var symbols = Source.Matches.Select(m => m.Symbol.ToUpperInvariant()).ToList();
            await Db.Assets.Where(a => symbols.Contains(a.Symbol)).ExecuteDeleteAsync();
            await Db.FxRates.Where(r => currencies.Contains(r.Quote)).ExecuteDeleteAsync();
            scope.Dispose();
            await api.DisposeAsync();
        }
    }

    [NeonFact]
    public async Task A_name_longer_than_the_edit_limit_is_cut_so_the_asset_can_still_be_edited()
    {
        await using var catalog = new Catalog();
        var symbol = NewSymbol();
        catalog.Source.Matches.Add(new(symbol, new string('N', 150), "NMS", "ETF", null, null));

        var added = await catalog.Assets.AddAsync(symbol, null, default);

        Assert.True(added.Created);
        var asset = await catalog.Db.Assets.AsNoTracking().Include(a => a.Texts).SingleAsync(a => a.Id == added.Asset.Id);
        Assert.True(asset.Name.Length <= AssetCatalog.MaxNameLength);
        Assert.True(asset.Texts.Single().What.Length <= AssetCatalog.MaxWhatLength);
    }

    [NeonFact]
    public async Task A_new_stock_is_stored_with_its_details_history_and_exchange_rate()
    {
        await using var catalog = new Catalog();
        var symbol = NewSymbol();
        catalog.Source.Currency = catalog.NewCurrency();
        catalog.Source.Matches.Add(new(symbol, "Example Corporation", "NMS", "EQUITY", "Technology", "Software—Infrastructure"));

        var added = await catalog.Assets.AddAsync(symbol.ToLowerInvariant(), null, default);

        Assert.True(added.Created);
        Assert.Null(added.Problem);
        var asset = await catalog.Db.Assets.AsNoTracking().Include(a => a.Texts).SingleAsync(a => a.Id == added.Asset.Id);
        Assert.Equal(symbol, asset.Symbol);
        Assert.Equal("XNAS", asset.Exchange);
        Assert.Equal("Example Corporation", asset.Name);
        Assert.Equal(AssetKind.Stock, asset.Kind);
        Assert.Equal(catalog.Source.Currency, asset.Currency);
        Assert.Equal("Technology", asset.Sector);
        Assert.Equal("Software—Infrastructure", asset.Industry);
        Assert.Equal("Technology · Software infrastructure", Assert.Single(asset.Texts, t => t.Locale == TemplateTexts.DefaultLocale).What);
        Assert.Equal(30, await catalog.Db.Prices.CountAsync(p => p.AssetId == asset.Id));
        Assert.True(await catalog.Db.FxRates.AnyAsync(r => r.Base == PriceRefresher.BaseCurrency && r.Quote == asset.Currency));
    }

    [NeonFact]
    public async Task An_asset_already_there_is_returned_without_asking_the_source()
    {
        await using var catalog = new Catalog();
        var symbol = NewSymbol();
        catalog.Source.Matches.Add(new(symbol, "Example Corporation", "NYQ", "EQUITY", null, null));
        var first = await catalog.Assets.AddAsync(symbol, null, default);

        var again = await catalog.Assets.AddAsync($" {symbol.ToLowerInvariant()} ", null, default);

        Assert.False(again.Created);
        Assert.Equal(first.Asset.Id, again.Asset.Id);
        Assert.Equal(1, catalog.Source.Searches);
        Assert.Equal("Example Corporation", Assert.Single(again.Asset.Texts).What); // No sector or industry: its name.
    }

    [NeonFact]
    public async Task Only_the_exact_symbol_is_added()
    {
        await using var catalog = new Catalog();
        var symbol = NewSymbol();
        catalog.Source.Matches.Add(new(symbol + "X", "A near match", "NMS", "EQUITY", null, null));

        var error = await Assert.ThrowsAsync<AssetNotFoundException>(() => catalog.Assets.AddAsync(symbol, null, default));

        Assert.Contains(symbol, error.Message);
        Assert.False(await catalog.Db.Assets.AnyAsync(a => a.Symbol.StartsWith(symbol)));
    }

    [NeonFact]
    public async Task A_type_the_app_does_not_follow_is_refused()
    {
        await using var catalog = new Catalog();
        var symbol = NewSymbol();
        catalog.Source.Matches.Add(new(symbol, "Some coin", "CCC", "CRYPTOCURRENCY", null, null));

        var error = await Assert.ThrowsAsync<AssetNotFoundException>(() => catalog.Assets.AddAsync(symbol, null, default));

        Assert.Equal($"Yahoo lists {symbol} as cryptocurrency: only stocks, funds and indexes can be added.", error.Message);
    }

    [NeonFact]
    public async Task A_fund_is_described_by_its_name()
    {
        await using var catalog = new Catalog();
        var symbol = NewSymbol();
        catalog.Source.Matches.Add(new(symbol, "Vanguard FTSE All-World UCITS ETF", "GER", "ETF", "Ignored", "Ignored"));

        var added = await catalog.Assets.AddAsync(symbol, null, default);

        Assert.Equal(AssetKind.Fund, added.Asset.Kind);
        Assert.Equal("XETR", added.Asset.Exchange);
        Assert.Null(added.Asset.Sector);
        Assert.Equal("Vanguard FTSE All-World UCITS ETF", Assert.Single(added.Asset.Texts).What);
    }

    [NeonFact]
    public async Task Prices_that_do_not_load_keep_the_row_and_adding_it_again_tries_again()
    {
        await using var catalog = new Catalog();
        var symbol = NewSymbol();
        catalog.Source.Matches.Add(new(symbol, "Example Corporation", "NMS", "EQUITY", null, null));
        catalog.Source.PricesFail = true;

        var added = await catalog.Assets.AddAsync(symbol, null, default);

        Assert.True(added.Created);
        Assert.Equal($"Yahoo answered HTTP 429 for {symbol}.", added.Problem);
        Assert.False(await catalog.Db.Prices.AnyAsync(p => p.AssetId == added.Asset.Id));

        catalog.Source.PricesFail = false;
        var again = await catalog.Assets.AddAsync(symbol, null, default);

        Assert.False(again.Created);
        Assert.Null(again.Problem);
        Assert.Equal(30, await catalog.Db.Prices.CountAsync(p => p.AssetId == added.Asset.Id));
    }

    [Theory]
    [InlineData("NMS", "XNAS")]
    [InlineData("NYQ", "XNYS")]
    [InlineData("PCX", "ARCX")]
    [InlineData("LSE", "XLON")]
    [InlineData("GER", "XETR")]
    [InlineData("OSA", "XJPX")]
    [InlineData("xyz", "XYZ")]
    public void Yahoo_exchange_codes_become_the_seeded_MICs(string yahoo, string mic) => Assert.Equal(mic, AssetCatalog.Mic(yahoo));

    [Theory]
    [InlineData("EQUITY", AssetKind.Stock)]
    [InlineData("ETF", AssetKind.Fund)]
    [InlineData("MUTUALFUND", AssetKind.Fund)]
    [InlineData("INDEX", AssetKind.Index)]
    [InlineData("CURRENCY", null)]
    [InlineData("FUTURE", null)]
    public void Yahoo_types_become_kinds(string type, AssetKind? kind) => Assert.Equal(kind, AssetCatalog.KindOf(type));

    [Theory]
    [InlineData("Technology", "Consumer Electronics", "Technology · Consumer electronics")]
    [InlineData("Real Estate", "REIT—Residential", "Real estate · REIT residential")]
    [InlineData(null, "Biotechnology", "Biotechnology")]
    [InlineData(null, null, "Example Corporation")]
    public void A_stock_is_described_by_its_sector_and_industry(string? sector, string? industry, string what) =>
        Assert.Equal(what, AssetCatalog.What(new Asset
            { Symbol = "X", Exchange = "XNAS", Name = "Example Corporation", Currency = "USD", Kind = AssetKind.Stock, Sector = sector, Industry = industry }));
}
