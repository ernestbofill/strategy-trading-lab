using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StrategyTradingLab.Api.Backtest;
using StrategyTradingLab.Api.MarketData;
using StrategyTradingLab.Api.Neon;

namespace StrategyTradingLab.Api.Tests;

/// <summary>
/// A whole draw and a snapshot on the owner page, with synthetic prices for every member (no network): the real membership, the real
/// engine for the candidates, and the bucket under a folder of the test's own, deleted afterwards.
/// </summary>
public class BacktestDataDrawTests
{
    /// <summary>A seeded random walk per symbol; one in 20 symbols has no prices and one in 20 starts in 2016.</summary>
    sealed class SyntheticSource : IMarketDataSource
    {
        static readonly string[] Sectors = ["Technology", "Healthcare", "Financial Services", "Industrials", "Consumer Cyclical",
            "Consumer Defensive", "Energy", "Utilities", "Real Estate", "Basic Materials", "Communication Services"];

        static int Hash(string symbol) => symbol.Aggregate(17, (h, c) => unchecked(h * 31 + c)) & int.MaxValue;

        public Task<DailyHistory> HistoryAsync(string symbol, DateOnly since, CancellationToken cancellationToken)
        {
            var hash = Hash(symbol);
            // An exchange rate (EURUSD=X) always has prices, as SPY does.
            var always = symbol == BacktestData.IndexSymbol || symbol.EndsWith("=X", StringComparison.Ordinal);
            if (!always && hash % 20 == 0) throw new MarketDataException($"Yahoo answered HTTP 404 for {symbol}.") { Status = 404 };
            var start = !always && hash % 20 == 1 ? new DateOnly(2016, 3, 1) : since;
            var random = new Random(hash);
            var price = 20 + hash % 80m;
            var candles = new List<Candle>();
            for (var day = start; day <= DateOnly.FromDateTime(DateTime.UtcNow); day = day.AddDays(1))
            {
                if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
                var open = price;
                price = Math.Max(1, Math.Round(price * (decimal)Math.Exp(0.0003 + 0.018 * (random.NextDouble() * 2 - 1) * 1.7), 4));
                candles.Add(new Candle(day, open, Math.Max(open, price), Math.Min(open, price), price, 100_000 + hash % 900_000));
            }
            return Task.FromResult(new DailyHistory(candles, candles, null));
        }

        public Task<IReadOnlyList<SymbolMatch>> SearchAsync(string query, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SymbolMatch>>([new SymbolMatch(query, query, "NYQ", "EQUITY", Sectors[Hash(query) % Sectors.Length], null)]);

        public Task<IReadOnlyList<Candle>> DailyAsync(string symbol, DateOnly? since, bool adjusted, CancellationToken cancellationToken) =>
            throw new MarketDataException("Not used.");

        public Task<string> CurrencyAsync(string symbol, CancellationToken cancellationToken) => Task.FromResult("USD");

        public Task<LatestPrice> LatestAsync(string symbol, CancellationToken cancellationToken) => throw new MarketDataException("Not used.");
    }

    sealed class Api(string root) : AccountsApi
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IMarketDataSource>(new SyntheticSource());
                services.Configure<BacktestDataOptions>(o => (o.Pause, o.Retries, o.StorageRoot) = (TimeSpan.Zero, [], root));
            });
        }
    }

    static async Task<JsonElement> FinishedAsync(HttpClient admin)
    {
        for (var i = 0; i < 300; i++)
        {
            var status = await admin.GetFromJsonAsync<JsonElement>("/admin/backtest-data");
            if (status.GetProperty("running").ValueKind == JsonValueKind.Null) return status;
            await Task.Delay(500);
        }
        throw new TimeoutException("The draw didn't finish in 150 s.");
    }

    [NeonFact]
    public async Task The_owner_draws_the_test_draft_and_gets_its_report_and_snapshot()
    {
        var root = $"test-backtest-{Guid.NewGuid():N}/";
        using var api = new Api(root);
        try
        {
            using var admin = await api.OwnerAsync();
            Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync("/admin/backtest-data/snapshot", null)).StatusCode);

            Assert.Equal(HttpStatusCode.Accepted, (await admin.PostAsync("/admin/backtest-data/draw", null)).StatusCode);
            var status = await FinishedAsync(admin);

            Assert.Equal(JsonValueKind.Null, status.GetProperty("last").GetProperty("error").ValueKind);
            var draw = status.GetProperty("draw");
            var periods = draw.GetProperty("periods").EnumerateArray().ToList();
            // The draw runs on the real clock: the periods move each 1 January.
            Assert.Equal(BacktestPeriods.For(DateOnly.FromDateTime(DateTime.UtcNow)).Select(p => p.Label), periods.Select(p => p.GetProperty("label").GetString()));
            Assert.All(periods, p =>
            {
                Assert.Equal(60, p.GetProperty("draft").GetArrayLength());
                Assert.InRange(p.GetProperty("eligible").GetInt32(), 300, 500);
                Assert.Equal(JsonValueKind.Number, p.GetProperty("representative").GetProperty("draftRate").ValueKind);
            });
            Assert.True(periods[1].GetProperty("kept").GetInt32() > 40);
            var missing = draw.GetProperty("missing").EnumerateArray().ToList();
            Assert.Contains(missing, m => m.GetProperty("ticker").GetString() == "S" && m.GetProperty("reason").GetString() == "noSymbol");
            Assert.Contains(missing, m => m.GetProperty("reason").GetString() == "noPrices");
            Assert.Contains(missing, m => m.GetProperty("reason").GetString() == "startsLate");
            Assert.Contains(missing, m => m.GetProperty("reason").GetString() == "otherClass" && m.GetProperty("ticker").GetString() is "GOOG" or "GOOGL");
            Assert.Contains(draw.GetProperty("renamed").EnumerateArray(), r => r.GetProperty("ticker").GetString() == "IR" && r.GetProperty("symbol").GetString() == "TT");
            var candidates = draw.GetProperty("candidates").EnumerateArray().ToList();
            Assert.Equal(16, candidates.Count(c => c.GetProperty("template").GetString() == "dip"));
            Assert.Equal(16, candidates.Count(c => c.GetProperty("template").GetString() == "comeback"));

            var snapshot = status.GetProperty("snapshot");
            var tickers = snapshot.GetProperty("tickers").EnumerateArray().ToList();
            Assert.Contains(tickers, t => t.GetProperty("symbol").GetString() == "SPY" && t.GetProperty("roles")[0].GetString() == "index");
            Assert.Contains(tickers, t => t.GetProperty("symbol").GetString() == "ETN" && t.GetProperty("roles").EnumerateArray().Any(r => r.GetString() == "starter"));
            Assert.False(status.GetProperty("membership").GetProperty("changedSinceDraw").GetBoolean());
            Assert.False(status.GetProperty("periodsChangedSinceDraw").GetBoolean());

            // The files are there and read back as the draw wrote them.
            var store = api.Services.CreateScope().ServiceProvider.GetRequiredService<BacktestStore>();
            var first = periods[0].GetProperty("draft")[0].GetProperty("symbol").GetString()!;
            var file = await store.PricesAsync(snapshot.GetProperty("key").GetString()!, first, CancellationToken.None);
            Assert.True(PriceFile.Read(file!).Count > 4000);

            // A backtest on the test draft, with the defaults: every period, the three baselines, kept by its key.
            var run = await admin.PostAsJsonAsync("/admin/backtest", new { templateKey = "dip" });
            Assert.True(run.StatusCode == HttpStatusCode.OK, await run.Content.ReadAsStringAsync());
            var result = await run.Content.ReadFromJsonAsync<JsonElement>();
            var tested = result.GetProperty("result").GetProperty("periods").EnumerateArray().ToList();
            Assert.Equal(periods.Count, tested.Count);
            Assert.All(tested, p =>
            {
                Assert.Equal(60, p.GetProperty("stocks").GetInt32());
                Assert.Equal(JsonValueKind.Number, p.GetProperty("index").GetProperty("rate").ValueKind);
                Assert.Equal(JsonValueKind.Object, p.GetProperty("sameDays").ValueKind);
            });
            var setup = result.GetProperty("setup");
            Assert.Equal((6000m, 100m, 400m, "EUR"), (setup.GetProperty("budget").GetDecimal(), setup.GetProperty("amountPerTrade").GetDecimal(),
                setup.GetProperty("monthlyLimit").GetDecimal(), setup.GetProperty("currency").GetString()));
            var again = await (await admin.PostAsJsonAsync("/admin/backtest", new { templateKey = "dip" })).Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(result.GetProperty("key").GetString(), again.GetProperty("key").GetString());
            Assert.Equal(result.GetProperty("ranAt").GetString(), again.GetProperty("ranAt").GetString()); // Kept, not run again.

            // One stock outside the snapshot, fetched and kept for the month; the latest period only, its budget sized for it.
            var one = await (await admin.PostAsJsonAsync("/admin/backtest", new { templateKey = "comeback", stocks = "stock", symbols = new[] { "zzzq" }, currency = "USD" }))
                .Content.ReadFromJsonAsync<JsonElement>();
            Assert.Single(one.GetProperty("result").GetProperty("periods").EnumerateArray());
            Assert.Equal(400m, one.GetProperty("setup").GetProperty("budget").GetDecimal()); // The comeback's 2 trades of 200.
            Assert.Equal(JsonValueKind.Null, one.GetProperty("result").GetProperty("periods")[0].GetProperty("sameDays").ValueKind);
            Assert.NotNull(await store.FetchedAsync(DateTime.UtcNow.ToString("yyyy-MM"), "ZZZQ", CancellationToken.None));
            // Index trend on SPY: the template's budget, no monthly limit.
            var trend = await (await admin.PostAsJsonAsync("/admin/backtest", new { templateKey = "trend" })).Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal((3000m, 3000m), (trend.GetProperty("setup").GetProperty("budget").GetDecimal(), trend.GetProperty("setup").GetProperty("monthlyLimit").GetDecimal()));
            // A changed setting goes over the template's defaults; one the rules don't have, or out of range, is refused as a strategy's would be.
            var tuned = await (await admin.PostAsJsonAsync("/admin/backtest", new { templateKey = "dip", settings = new { cooldownDays = 20 } })).Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(20, tuned.GetProperty("setup").GetProperty("params").GetProperty("cooldownDays").GetInt32());
            Assert.True(tuned.GetProperty("setup").GetProperty("params").TryGetProperty("minGainPct", out _));
            Assert.NotEqual(result.GetProperty("key").GetString(), tuned.GetProperty("key").GetString());
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/admin/backtest", new { templateKey = "dip", settings = new { nope = 1 } })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/admin/backtest", new { templateKey = "dip", settings = new { cooldownDays = -5 } })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/admin/backtest", new { templateKey = "ai" })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/admin/backtest", new { templateKey = "dip", years = 6 })).StatusCode);

            Assert.Equal(HttpStatusCode.Accepted, (await admin.PostAsync("/admin/backtest-data/snapshot", null)).StatusCode);
            again = await FinishedAsync(admin);
            Assert.Equal("snapshot", again.GetProperty("last").GetProperty("kind").GetString());
            Assert.Equal(JsonValueKind.Null, again.GetProperty("last").GetProperty("error").ValueKind);
        }
        finally
        {
            var s3 = api.Services.GetRequiredService<IAmazonS3>();
            var bucket = api.Services.GetRequiredService<IOptions<ObjectStorageOptions>>().Value.Bucket;
            var listing = await s3.ListObjectsV2Async(new ListObjectsV2Request { BucketName = bucket, Prefix = root });
            foreach (var item in listing.S3Objects ?? []) await s3.DeleteObjectAsync(bucket, item.Key);
        }
    }
}
