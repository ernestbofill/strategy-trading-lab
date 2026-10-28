using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.MarketData;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Tests;

/// <summary>The fund picker's search and add (/assets), for any logged-in user. Each test removes what it adds.</summary>
public class AssetEndpointTests
{
    /// <summary>
    /// Answers every search with a fund on NYSE Arca (supported), a fund on Xetra (not supported yet), a stock and the
    /// seeded S&amp;P 500 fund; 30 days of prices for anything.
    /// </summary>
    sealed class FakeSource(string fund, string xetraFund, string stock) : IMarketDataSource
    {
        public Task<IReadOnlyList<Candle>> DailyAsync(string symbol, bool fullHistory, bool adjusted, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Candle>>([.. Enumerable.Range(0, 30)
                .Select(day => new Candle(new DateOnly(2026, 9, 1).AddDays(day), 10, 11, 9, 10, 1000))]);

        public Task<IReadOnlyList<SymbolMatch>> SearchAsync(string query, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SymbolMatch>>(
            [
                new(fund, "Example World Fund", "PCX", "ETF", null, null),
                new(xetraFund, "Example Xetra Fund", "GER", "ETF", null, null),
                new(stock, "Example Corporation", "NYQ", "EQUITY", "Industrials", "Machinery"),
                new(TemplateAssets.Spy.Symbol, "SPDR S&P 500 ETF Trust", "PCX", "ETF", null, null),
            ]);

        public Task<string> CurrencyAsync(string symbol, CancellationToken cancellationToken) => Task.FromResult(PriceRefresher.BaseCurrency);
    }

    /// <summary><paramref name="assetLimit"/>: the server's value for USER_ASSET_LIMIT (the page's, if set, would win).</summary>
    sealed class Api(IMarketDataSource source, int? assetLimit = null) : AccountsApi
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            if (assetLimit is { } limit) builder.UseSetting(Admin.SettingDefinitions.UserAssetLimit, limit.ToString());
            builder.ConfigureTestServices(services => services.AddSingleton(source));
        }
    }

    static string NewSymbol() => $"T{Guid.NewGuid():N}"[..10].ToUpperInvariant();

    [NeonFact]
    public async Task Search_and_add_need_a_log_in()
    {
        await using var api = new Api(new FakeSource(NewSymbol(), NewSymbol(), NewSymbol()));
        using var stranger = api.CreateApiClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.GetAsync("/assets/search?q=world&kind=fund")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.PostAsJsonAsync("/assets", new { symbol = "SPY", kind = "fund" })).StatusCode);
    }

    [NeonFact]
    public async Task Adding_needs_a_confirmed_email_but_searching_does_not()
    {
        var (fund, xetra, stock) = (NewSymbol(), NewSymbol(), NewSymbol());
        await using var api = new Api(new FakeSource(fund, xetra, stock));
        var (client, _) = await api.SignedUpAsync();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/assets/search?q=world&kind=fund")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/assets", new { symbol = fund, kind = "fund" })).StatusCode);

        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Assets.AnyAsync(a => a.Symbol == fund));
    }

    [NeonFact]
    public async Task Search_lists_stored_funds_first_then_new_ones_from_supported_exchanges()
    {
        var (fund, xetra, stock) = (NewSymbol(), NewSymbol(), NewSymbol());
        await using var api = new Api(new FakeSource(fund, xetra, stock));
        var (client, _) = await api.SignedUpAsync();

        var results = (await client.GetFromJsonAsync<JsonElement>("/assets/search?q=S%26P%20500&kind=fund")).EnumerateArray().ToList();

        var stored = results.TakeWhile(r => r.GetProperty("id").ValueKind != JsonValueKind.Null).Select(r => r.GetProperty("symbol").GetString()).ToList();
        Assert.Contains(TemplateAssets.Spy.Symbol, stored);
        Assert.Contains(TemplateAssets.Cspx.Symbol, stored);
        var fresh = results.Skip(stored.Count).ToList();
        Assert.All(fresh, r => Assert.Equal(JsonValueKind.Null, r.GetProperty("id").ValueKind));
        // The seeded SPY isn't listed twice; the Xetra fund and the stock aren't listed at all.
        Assert.Equal([fund], fresh.Select(r => r.GetProperty("symbol").GetString()));
        Assert.Equal("ARCX", fresh[0].GetProperty("exchange").GetString());
        Assert.Equal("Example World Fund", fresh[0].GetProperty("what").GetString());

        var tooShort = await client.GetAsync("/assets/search?q=s&kind=fund");
        Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/assets/search?q=spy&kind=index")).StatusCode);
    }

    [NeonFact]
    public async Task An_added_fund_can_be_watched_by_a_fund_template()
    {
        var (fund, xetra, stock) = (NewSymbol(), NewSymbol(), NewSymbol());
        await using var api = new Api(new FakeSource(fund, xetra, stock));
        var (client, _) = await api.VerifiedAsync();
        var name = $"Test {Guid.NewGuid()}";
        try
        {
            var response = await client.PostAsJsonAsync("/assets", new { symbol = fund.ToLowerInvariant(), kind = "fund" });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var added = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(added.GetProperty("pricesIn").GetBoolean());
            var asset = added.GetProperty("asset");
            Assert.Equal(fund, asset.GetProperty("symbol").GetString());
            Assert.Equal("Example World Fund", asset.GetProperty("what").GetString());

            var start = await client.PostAsJsonAsync("/strategies", new
            {
                templateKey = "bear", name, mode = "simulation",
                budget = new { amount = 3000, step = 600, maxSteps = 5 },
                assetIds = new[] { asset.GetProperty("id").GetGuid() },
            });
            Assert.Equal(HttpStatusCode.Created, start.StatusCode);

            // Added again, the stored one comes back.
            var again = await (await client.PostAsJsonAsync("/assets", new { symbol = fund, kind = "fund" })).Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(asset.GetProperty("id").GetGuid(), again.GetProperty("asset").GetProperty("id").GetGuid());
        }
        finally
        {
            using var scope = api.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Strategies.Where(s => s.Name == name).ExecuteDeleteAsync();
            await db.Assets.Where(a => a.Symbol == fund).ExecuteDeleteAsync();
        }
    }

    [NeonFact]
    public async Task Adding_refuses_what_is_not_a_fund_from_a_supported_exchange()
    {
        var (fund, xetra, stock) = (NewSymbol(), NewSymbol(), NewSymbol());
        await using var api = new Api(new FakeSource(fund, xetra, stock));
        var (client, _) = await api.VerifiedAsync();

        async Task<string?> Refusal(string symbol)
        {
            var response = await client.PostAsJsonAsync("/assets", new { symbol, kind = "fund" });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").GetProperty("symbol")[0].GetString();
        }

        Assert.Equal("That is not a fund we can follow.", await Refusal(stock));
        Assert.Equal("That is not a fund we can follow.", await Refusal(xetra));
        Assert.Equal("That is not a fund we can follow.", await Refusal(TemplateAssets.CBrain.Symbol));
        Assert.Equal("That is not a fund we can follow.", await Refusal("UNKNOWN"));

        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Assets.AnyAsync(a => a.Symbol == stock || a.Symbol == xetra));
    }

    [NeonFact]
    public async Task A_user_adds_at_most_the_limit_of_new_assets_but_can_always_pick_a_stored_one()
    {
        var (fund, xetra, stock) = (NewSymbol(), NewSymbol(), NewSymbol());
        await using var api = new Api(new FakeSource(fund, xetra, stock), assetLimit: 1);
        var (client, _) = await api.VerifiedAsync();
        try
        {
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/assets", new { symbol = fund, kind = "fund" })).StatusCode);

            // Funds and stocks share the limit.
            var refused = await client.PostAsJsonAsync("/assets", new { symbol = stock, kind = "stock" });
            Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
            Assert.StartsWith("You've added 1 stocks and funds, the most for now.", await refused.Content.ReadFromJsonAsync<string>());

            // Stored ones never count: the seeded fund, and the user's own added one.
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/assets", new { symbol = TemplateAssets.Spy.Symbol, kind = "fund" })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/assets", new { symbol = fund, kind = "fund" })).StatusCode);

            using var scope = api.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.False(await db.Assets.AnyAsync(a => a.Symbol == stock));
        }
        finally
        {
            using var scope = api.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Assets.Where(a => a.Symbol == fund || a.Symbol == stock).ExecuteDeleteAsync();
        }
    }
}
