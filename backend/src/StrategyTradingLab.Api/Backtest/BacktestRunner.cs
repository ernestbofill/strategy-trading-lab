using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Engine;
using StrategyTradingLab.Api.Evaluation;
using StrategyTradingLab.Api.MarketData;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Backtest;

/// <summary>Which stocks a test uses (docs/backtest.md, *Stocks*).</summary>
public enum TestStocks { Draft, List, Stock }

/// <summary>
/// A test to run. Everything but the template may be left out: the rules are then the template's defaults in use, the stocks the test
/// draft, and the money and years the test defaults (docs/backtest.md, *What is tested*); see <see cref="BacktestRunner.Resolve"/>.
/// </summary>
/// <param name="Params">Settings over the template's defaults in use, by the params' names (a strategy's changed settings); null: the defaults.</param>
/// <param name="Symbols">The strategy's list, or the one stock, by Yahoo symbol.</param>
/// <param name="Index">The index fund <i>Bear markets</i> and <i>Index trend</i> trade; null: SPY.</param>
/// <param name="Currency">The test's currency: EUR, USD or GBP.</param>
public record BacktestRequest(string TemplateKey, JsonElement? Params = null, TestStocks Stocks = TestStocks.Draft,
    IReadOnlyList<string>? Symbols = null, string? Index = null, int? Years = null, int? PeriodYears = null, decimal? Budget = null,
    decimal? AmountPerTrade = null, int? MaxTrades = null, decimal? MonthlyLimit = null, decimal? FeePct = null, decimal? SpreadPct = null,
    string Currency = "EUR");

/// <summary>A test as run: every default filled in. <c>AmountPerTrade</c> is null for all in (<i>Index trend</i>).</summary>
public record BacktestSetup(string TemplateKey, JsonElement Params, TestStocks Stocks, IReadOnlyList<string> Symbols, string Index,
    int Years, int PeriodYears, decimal Budget, decimal? AmountPerTrade, int MaxTrades, decimal MonthlyLimit, decimal FeePct, decimal SpreadPct,
    string Currency);

/// <summary>A period's pool as tested: its stocks, and those that sat it out for want of history.</summary>
public record TestedPool(string Label, IReadOnlyList<string> Stocks, IReadOnlyList<string> SatOut);

/// <summary>
/// A test's answer: its key (the same setup and data give the same key, and the result kept for it), when it ran, the snapshot it read
/// (its month), the setup with its defaults, the data's first and last day, and the results.
/// </summary>
public record BacktestRun(string Key, DateTimeOffset RanAt, string Snapshot, BacktestSetup Setup, DateOnly DataFrom, DateOnly DataTo,
    IReadOnlyList<TestedPool> Pools, BacktestResult Result);

/// <summary>A test that can't run: the message says why, for the owner.</summary>
public class BacktestRunException(string message, bool missingData = false) : Exception(message)
{
    /// <summary>The data isn't there (no snapshot, a period outside the draft), as opposed to a request out of range.</summary>
    public bool MissingData { get; } = missingData;
}

/// <summary>
/// Runs one backtest in the request (ROADMAP item 54): reads the latest snapshot (the draft per period, SPY, the starter lists),
/// fetches from Yahoo any other stock, fund or exchange rate the test needs (kept for the month in object storage), runs
/// <see cref="BacktestSimulator"/> and keeps the result by its key.
/// </summary>
public class BacktestRunner(BacktestStore store, BacktestPrices prices, TemplateStore templates, AppDbContext db, TimeProvider time)
{
    /// <summary>The test defaults (docs/backtest.md, *What is tested*).</summary>
    public static readonly TestMoney DefaultMoney = new();
    public const decimal DefaultAmountPerTrade = 100;
    public const int DefaultMaxTrades = 5, MaxStocks = 60, MaxYears = 30;
    static readonly string[] Currencies = ["EUR", "USD", "GBP"];

    DateOnly Today => DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);

    public async Task<BacktestRun> RunAsync(BacktestRequest request, CancellationToken cancellationToken)
    {
        if (!store.Configured) throw new BacktestRunException("Object storage isn't configured on this server.", missingData: true);
        var snapshot = await store.SnapshotAsync(cancellationToken)
            ?? throw new BacktestRunException("No snapshot yet: draw the test draft on the Backtest data page first.", missingData: true);
        var manifest = await store.ManifestAsync(snapshot, cancellationToken)
            ?? throw new BacktestRunException("The latest snapshot has no manifest: take this month's snapshot again.", missingData: true);
        var template = await templates.FindAsync(request.TemplateKey, cancellationToken);
        if (template is null || !TemplateRules.Supports(template.Key)) throw new BacktestRunException($"No rules to test for '{request.TemplateKey}'.");

        var setup = Resolve(request, template);
        var periods = BacktestPeriods.For(Today, setup.Years, setup.PeriodYears);
        var pools = PoolSymbols(setup, periods, manifest);
        var symbols = pools.SelectMany(p => p.Symbols).Append(BacktestData.IndexSymbol).Distinct().ToList();
        var since = periods[0].WarmUpStart.AddDays(-BacktestPeriods.WarmUpSlackDays);
        var month = Month();
        var key = Key(setup, periods, pools, snapshot.Key, month);
        if (await store.ResultAsync(key, cancellationToken) is { } kept && JsonSerializer.Deserialize<BacktestRun>(kept, BacktestStore.Json) is { } done)
            return done;

        var assets = new Dictionary<string, TestAsset>();
        foreach (var symbol in symbols)
            assets[symbol] = await prices.AssetAsync(snapshot, month, symbol, since, cancellationToken);
        var fx = await prices.RatesAsync(month, setup.Currency, assets.Values.Select(a => a.Currency), since, cancellationToken);
        FxRate rate = (currency, day) => fx.Between(setup.Currency, currency, day)
            ?? throw new BacktestRunException($"No exchange rate from {setup.Currency} to {currency} on {day:yyyy-MM-dd}.", missingData: true);

        using var parameters = JsonDocument.Parse(setup.Params.GetRawText());
        var rules = TestRules.For(template.Key, parameters, new Budget(setup.Budget, setup.AmountPerTrade, setup.MaxTrades));
        var money = new TestMoney(setup.Budget, setup.MonthlyLimit, setup.FeePct, setup.SpreadPct);
        var result = BacktestSimulator.Run(rules, money, [.. pools.Select(p => new PeriodPool(p.Period, [.. p.Symbols.Select(s => assets[s])]))],
            assets[BacktestData.IndexSymbol], rate);
        var bars = assets.Values.SelectMany(a => a.Bars.Take(1).Concat(a.Bars.TakeLast(1))).Select(b => b.Date).ToList();
        var run = new BacktestRun(key, time.GetUtcNow(), snapshot.Month, setup, bars.Min(), bars.Max(),
            [.. result.Periods.Select(p => new TestedPool(p.Period.Label, [.. pools.Single(x => x.Period == p.Period).Symbols.Except(p.SatOut)], p.SatOut))],
            result);
        await store.SaveResultAsync(key, JsonSerializer.SerializeToUtf8Bytes(run, BacktestStore.Json), cancellationToken);
        return run;
    }

    string Month() => time.GetUtcNow().ToString("yyyy-MM", CultureInfo.InvariantCulture);

    /// <summary>
    /// The request with its defaults (docs/backtest.md, *What is tested* and *Stocks*): the template's defaults in use; the test draft
    /// over 16 years in 4-year periods, a list or one stock over the latest period only; €6,000, €100 a trade, up to 5 a stock, at most
    /// €400 a month; one stock's budget is its max trades × the amount per trade. <i>Bear markets</i> and <i>Index trend</i> trade one
    /// fund with the template's budget and no monthly limit unless one is given, as the template's own figures were tested.
    /// </summary>
    public static BacktestSetup Resolve(BacktestRequest request, Template template)
    {
        var fund = template.InvestsIn != InvestsIn.StockList;
        var stocks = fund ? TestStocks.Stock : request.Stocks;
        var symbols = fund ? [(request.Index ?? BacktestData.IndexSymbol).Trim().ToUpperInvariant()]
            : stocks == TestStocks.Draft ? [] : (request.Symbols ?? []).Select(s => s.Trim().ToUpperInvariant()).Where(s => s.Length > 0).Distinct().ToList();
        if (stocks == TestStocks.List && symbols.Count is 0 or > MaxStocks) throw new BacktestRunException($"A list needs 1 to {MaxStocks} stocks.");
        if (stocks == TestStocks.Stock && symbols.Count != 1) throw new BacktestRunException("One stock needs one symbol.");
        if (symbols.Any(s => s.Length > 20)) throw new BacktestRunException("A symbol has at most 20 characters.");

        // A list or one stock is chosen knowing how it did: the latest period only, by default.
        var latestOnly = !fund && stocks != TestStocks.Draft;
        var periodYears = request.PeriodYears ?? BacktestPeriods.DefaultLength;
        var years = request.Years ?? (latestOnly ? periodYears : BacktestPeriods.DefaultYears);
        if (periodYears < 1 || years < periodYears || years % periodYears != 0 || years > MaxYears)
            throw new BacktestRunException($"The years must be a multiple of the period's, up to {MaxYears}.");

        var allIn = template.Budget.Step is null;
        decimal? amount = allIn ? null : request.AmountPerTrade ?? (fund ? template.Budget.Step : DefaultAmountPerTrade);
        var maxTrades = allIn ? 1 : request.MaxTrades ?? (fund ? template.Budget.MaxSteps ?? DefaultMaxTrades : DefaultMaxTrades);
        var budget = request.Budget ?? (fund ? template.Budget.Amount : stocks == TestStocks.Stock ? maxTrades * amount!.Value : DefaultMoney.Budget);
        var limit = request.MonthlyLimit ?? (fund ? budget : DefaultMoney.MonthlyLimit);
        var (fee, spread) = (request.FeePct ?? DefaultMoney.FeePct, request.SpreadPct ?? DefaultMoney.SpreadPct);
        if (budget is <= 0 or > 10_000_000 || amount is <= 0 || amount > budget || maxTrades is < 1 or > 20 || limit <= 0
            || fee is < 0 or > 5 || spread is < 0 or > 5)
            throw new BacktestRunException("The money is out of range: a budget above 0, an amount per trade up to the budget, 1 to 20 trades a stock, a monthly limit above 0, fee and spread 0–5%.");
        if (!Currencies.Contains(request.Currency)) throw new BacktestRunException($"The test's currency is one of {string.Join(", ", Currencies)}.");

        var parameters = Merged(template.ParamsJson().RootElement, request.Params);
        return new(template.Key, parameters, stocks, symbols, fund ? symbols[0] : BacktestData.IndexSymbol, years, periodYears,
            budget, amount, maxTrades, Math.Min(limit, budget), fee, spread, request.Currency);
    }

    /// <summary>
    /// The template's defaults in use with the given settings over them (a strategy's changed settings, or Advanced settings' values on
    /// screen), so a setting left out keeps the template's value, not the code's.
    /// </summary>
    static JsonElement Merged(JsonElement defaults, JsonElement? given)
    {
        var merged = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(defaults.GetRawText())!;
        if (given is { ValueKind: JsonValueKind.Object } settings)
            foreach (var setting in settings.EnumerateObject())
            {
                // The names as the params store them (camelCase); a key the rules don't know is refused, not ignored.
                var key = merged.Keys.FirstOrDefault(k => string.Equals(k, setting.Name, StringComparison.OrdinalIgnoreCase))
                    ?? throw new BacktestRunException($"The rules have no setting '{setting.Name}'.");
                merged[key] = setting.Value.Clone();
            }
        return JsonSerializer.SerializeToElement(merged);
    }

    sealed record Pool(BacktestPeriod Period, IReadOnlyList<string> Symbols);

    /// <summary>
    /// Each period's symbols: the draft of the drawn period it starts in (the test draft can't reach past the dataset's years), or the
    /// list, the stock or the fund in every period.
    /// </summary>
    static List<Pool> PoolSymbols(BacktestSetup setup, IReadOnlyList<BacktestPeriod> periods, SnapshotManifest manifest)
    {
        if (setup.Stocks != TestStocks.Draft) return [.. periods.Select(p => new Pool(p, setup.Symbols))];
        return [.. periods.Select(p => manifest.Periods.FirstOrDefault(d => d.Start <= p.Start && p.Start <= d.End) is { } drawn ? new Pool(p, drawn.Draft)
            : p.Start > manifest.Periods[^1].End
                ? throw new BacktestRunException("The years moved past the test draft: draw it again on the Backtest data page.", missingData: true)
                : throw new BacktestRunException($"The test draft covers {manifest.Periods[0].Start.Year}–{manifest.Periods[^1].End.Year}: {p.Label} is outside it."))];
    }

    /// <summary>The result's key: a hash of everything that decides it, the data included (the snapshot's folder and the month's fetches).</summary>
    static string Key(BacktestSetup setup, IReadOnlyList<BacktestPeriod> periods, IReadOnlyList<Pool> pools, string snapshot, string month)
    {
        var text = JsonSerializer.Serialize(new
        {
            Version = 1, setup.TemplateKey, Params = Canonical(setup.Params), setup.Stocks, setup.Index, setup.Budget, setup.AmountPerTrade,
            setup.MaxTrades, setup.MonthlyLimit, setup.FeePct, setup.SpreadPct, setup.Currency, Snapshot = snapshot, Month = month,
            Pools = pools.Select(p => new { p.Period.Start, p.Period.End, p.Symbols }),
        }, BacktestStore.Json);
        return Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)))[..32];
    }

    /// <summary>The params with their keys in order, so the same settings give the same key whatever order they came in.</summary>
    static string Canonical(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => "{" + string.Join(",", element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal)
            .Select(p => JsonSerializer.Serialize(p.Name) + ":" + Canonical(p.Value))) + "}",
        JsonValueKind.Array => "[" + string.Join(",", element.EnumerateArray().Select(Canonical)) + "]",
        _ => element.GetRawText(),
    };
}

/// <summary>
/// The prices a test reads: a snapshot's file when it has the symbol, else this month's fetched copy, else Yahoo's (then kept for the
/// month). Parsed files are held in memory by their key, as a snapshot's never change.
/// </summary>
public class BacktestPrices(BacktestStore store, IMarketDataSource yahoo, AppDbContext db, ILogger<BacktestPrices> logger)
{
    static readonly Dictionary<string, IReadOnlyList<PriceBar>> Parsed = [];
    static readonly Lock Gate = new();

    public async Task<TestAsset> AssetAsync(SnapshotReport snapshot, string month, string symbol, DateOnly since, CancellationToken cancellationToken)
    {
        var known = snapshot.Tickers.FirstOrDefault(t => t.Symbol == symbol && t.Days > 0);
        // A snapshot holds the draft's US stocks and SPY from the draw's first warm-up, and the starter lists: enough unless the test
        // reaches further back than the snapshot's history.
        if (known is not null && known.First <= since.AddDays(BacktestPeriods.WarmUpSlackDays)
            && await ParsedAsync(BacktestStore.PriceKey(snapshot.Key, symbol), () => store.PricesAsync(snapshot.Key, symbol, cancellationToken)) is { } bars)
            return new(symbol, await CurrencyAsync(symbol, known.Roles, cancellationToken), bars);

        var fetched = await store.FetchedAsync(month, symbol, cancellationToken);
        if (fetched is null || fetched.Since > since)
        {
            DailyHistory history;
            try
            {
                history = await yahoo.HistoryAsync(symbol, since, cancellationToken);
            }
            catch (MarketDataException error)
            {
                throw new BacktestRunException($"Yahoo gave no prices for {symbol}: {error.Message}", missingData: true);
            }
            if (history.Adjusted.Count == 0) throw new BacktestRunException($"Yahoo has no prices for {symbol}.", missingData: true);
            var currency = await CurrencyAsync(symbol, [], cancellationToken);
            var file = PriceFile.Write([.. history.Adjusted.Select(c => new PriceBar(c.Date, c.Open, c.High, c.Low, c.Close, c.Volume))]);
            fetched = new(symbol, currency, history.Name, since, history.Adjusted[0].Date, history.Adjusted[^1].Date);
            await store.SaveFetchedAsync(month, fetched, file, cancellationToken);
            logger.LogInformation("Fetched {Symbol} for a backtest, from {Since}.", symbol, since);
            Forget(BacktestStore.PriceKey(store.FetchedFolder(month), symbol));
        }
        var folder = store.FetchedFolder(month);
        var prices = await ParsedAsync(BacktestStore.PriceKey(folder, symbol), () => store.PricesAsync(folder, symbol, cancellationToken))
            ?? throw new BacktestRunException($"The prices kept for {symbol} are missing.", missingData: true);
        return new(symbol, fetched.Currency, prices);
    }

    /// <summary>
    /// The exchange rates the test needs: units of each currency per euro, from Yahoo's <c>EUR{X}=X</c> closes, kept for the month as any
    /// fetched symbol (the app's stored rates start two years before the earliest strategy).
    /// </summary>
    public async Task<ExchangeRates> RatesAsync(string month, string testCurrency, IEnumerable<string> currencies, DateOnly since, CancellationToken cancellationToken)
    {
        var quotes = currencies.Append(testCurrency).Where(c => c != ExchangeRates.Base).Distinct().ToList();
        // One currency for everything: no rate needed.
        if (currencies.All(c => c == testCurrency)) return new([]);
        var rates = new Dictionary<string, List<(DateOnly, decimal)>>();
        foreach (var quote in quotes)
        {
            var asset = await AssetAsync(NoSnapshot, month, $"{ExchangeRates.Base}{quote}=X", since, cancellationToken);
            rates[quote] = [.. asset.Bars.Where(b => b.Close > 0).Select(b => (b.Date, b.Close))];
        }
        return new(rates);
    }

    static readonly SnapshotReport NoSnapshot = new("", default, "", default, "", [], []);

    /// <summary>A snapshot's assets are US stocks and SPY, in dollars; a starter list's currency is the asset's in the app.</summary>
    async Task<string> CurrencyAsync(string symbol, IReadOnlyList<SnapshotRole> roles, CancellationToken cancellationToken)
    {
        if (await db.Assets.AsNoTracking().Where(a => a.Symbol == symbol).Select(a => a.Currency).FirstOrDefaultAsync(cancellationToken) is { } stored)
            return stored;
        if (roles.Contains(SnapshotRole.Draft) || roles.Contains(SnapshotRole.Index)) return "USD";
        if (symbol.EndsWith("=X", StringComparison.Ordinal)) return symbol[3..6];
        try
        {
            return await yahoo.CurrencyAsync(symbol, cancellationToken);
        }
        catch (MarketDataException error)
        {
            throw new BacktestRunException($"Yahoo gave no currency for {symbol}: {error.Message}", missingData: true);
        }
    }

    static async Task<IReadOnlyList<PriceBar>?> ParsedAsync(string key, Func<Task<byte[]?>> load)
    {
        lock (Gate)
            if (Parsed.TryGetValue(key, out var held)) return held;
        if (await load() is not { } file) return null;
        var bars = PriceFile.Read(file);
        lock (Gate)
        {
            // A few hundred files at most (a snapshot's 80 or so, and the month's fetches): no eviction needed until a new month.
            if (Parsed.Count > 2000) Parsed.Clear();
            Parsed[key] = bars;
        }
        return bars;
    }

    static void Forget(string key)
    {
        lock (Gate) Parsed.Remove(key);
    }
}
