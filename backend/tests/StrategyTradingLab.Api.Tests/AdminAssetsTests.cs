using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.MarketData;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Tests;

/// <summary>The admin assets endpoints (/admin/assets), for the owner only.</summary>
public class AdminAssetsTests(AdminFixture fixture) : IClassFixture<AdminFixture>
{
    [NeonFact]
    public async Task Assets_need_the_owner_role()
    {
        static async Task<HttpStatusCode[]> AskAll(HttpClient client) =>
        [
            (await client.GetAsync("/admin/assets")).StatusCode,
            (await client.PutAsJsonAsync($"/admin/assets/{TemplateAssets.CBrain.Id}/active", new { active = false })).StatusCode,
            (await client.PostAsJsonAsync("/admin/assets", new { symbol = "AAPL" })).StatusCode,
            (await client.PutAsJsonAsync($"/admin/assets/{TemplateAssets.CBrain.Id}", new { name = "x", what = "y" })).StatusCode,
            (await client.DeleteAsync("/admin/assets/prices")).StatusCode,
            (await client.DeleteAsync($"/admin/assets/{TemplateAssets.CBrain.Id}/prices")).StatusCode,
            (await client.PostAsJsonAsync($"/admin/assets/{TemplateAssets.CBrain.Id}/prices", new { years = 2 })).StatusCode,
            (await client.PostAsJsonAsync("/admin/assets/rates", new { years = 2 })).StatusCode,
        ];

        using var stranger = fixture.CreateApiClient();
        Assert.All(await AskAll(stranger), status => Assert.Equal(HttpStatusCode.Unauthorized, status));

        foreach (var role in new[] { UserRole.User, UserRole.Vip })
        {
            using var other = await fixture.SignedUpAsync(role);
            Assert.All(await AskAll(other), status => Assert.Equal(HttpStatusCode.Forbidden, status));
        }
    }

    [NeonFact]
    public async Task Lists_all_seeded_assets_and_toggles_whether_one_is_active()
    {
        var cBrainId = TemplateAssets.CBrain.Id;
        try
        {
            var list = (await fixture.Admin.GetFromJsonAsync<JsonElement>("/admin/assets")).EnumerateArray().ToList();
            Assert.True(list.Count >= TemplateAssets.All.Length);

            var cBrain = list.Single(a => a.GetProperty("id").GetGuid() == cBrainId);
            Assert.Equal("CBRAIN.CO", cBrain.GetProperty("symbol").GetString());
            Assert.Equal("XCSE", cBrain.GetProperty("exchange").GetString());
            Assert.Equal("cBrain", cBrain.GetProperty("name").GetString());
            Assert.Equal("Software for governments", cBrain.GetProperty("what").GetString());
            Assert.Equal("stock", cBrain.GetProperty("kind").GetString());
            Assert.Equal("DKK", cBrain.GetProperty("currency").GetString());
            Assert.True(cBrain.GetProperty("active").GetBoolean());

            var hide = await fixture.Admin.PutAsJsonAsync($"/admin/assets/{cBrainId}/active", new { active = false });
            Assert.Equal(HttpStatusCode.NoContent, hide.StatusCode);

            var afterHide = (await fixture.Admin.GetFromJsonAsync<JsonElement>("/admin/assets")).EnumerateArray()
                .Single(a => a.GetProperty("id").GetGuid() == cBrainId);
            Assert.False(afterHide.GetProperty("active").GetBoolean());

            var restore = await fixture.Admin.PutAsJsonAsync($"/admin/assets/{cBrainId}/active", new { active = true });
            Assert.Equal(HttpStatusCode.NoContent, restore.StatusCode);

            var unknown = await fixture.Admin.PutAsJsonAsync($"/admin/assets/{Guid.NewGuid()}/active", new { active = false });
            Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        }
        finally
        {
            await fixture.Admin.PutAsJsonAsync($"/admin/assets/{cBrainId}/active", new { active = true });
        }
    }

    /// <summary>Knows one listing, <see cref="Symbol"/>, and answers 30 days of prices for anything.</summary>
    sealed class FakeSource(string symbol) : IMarketDataSource
    {
        /// <summary>The first day the last whole-history request asked for.</summary>
        public DateOnly? Since { get; private set; }

        public Task<IReadOnlyList<Candle>> DailyAsync(string symbol, DateOnly? since, bool adjusted, CancellationToken cancellationToken)
        {
            Since = since ?? Since;
            return Task.FromResult<IReadOnlyList<Candle>>([.. Enumerable.Range(0, 30)
                .Select(day => new Candle(new DateOnly(2026, 9, 1).AddDays(day), 10, 11, 9, 10, 1000))]);
        }

        public Task<IReadOnlyList<SymbolMatch>> SearchAsync(string query, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SymbolMatch>>([new(symbol, "Example Corporation", "NYQ", "EQUITY", "Industrials", "Specialty Industrial Machinery")]);

        // The euro: no exchange rate is fetched, so no fake one lands on a real currency.
        public Task<string> CurrencyAsync(string symbol, CancellationToken cancellationToken) => Task.FromResult(PriceRefresher.BaseCurrency);
    }

    sealed class Api(IMarketDataSource source) : AccountsApi
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services => services.AddSingleton(source));
        }
    }

    [NeonFact]
    public async Task Adds_an_asset_by_its_symbol_and_edits_its_name_and_description()
    {
        var symbol = $"T{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        await using var api = new Api(new FakeSource(symbol));
        using var admin = await api.OwnerAsync();
        try
        {
            var response = await admin.PostAsJsonAsync("/admin/assets", new { symbol = symbol.ToLowerInvariant() });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var added = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(added.GetProperty("created").GetBoolean());
            Assert.Equal(JsonValueKind.Null, added.GetProperty("problem").ValueKind);
            var asset = added.GetProperty("asset");
            var id = asset.GetProperty("id").GetGuid();
            Assert.Equal(symbol, asset.GetProperty("symbol").GetString());
            Assert.Equal("XNYS", asset.GetProperty("exchange").GetString());
            Assert.Equal("Industrials · Specialty industrial machinery", asset.GetProperty("what").GetString());
            Assert.Equal("2026-09-30", asset.GetProperty("latestPriceDate").GetString());

            var again = await (await admin.PostAsJsonAsync("/admin/assets", new { symbol })).Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(again.GetProperty("created").GetBoolean());
            Assert.Equal(id, again.GetProperty("asset").GetProperty("id").GetGuid());

            // Unticked on a running strategy, it still counts: its prices are still fetched daily (PriceRefresher.Watched).
            using (var scope = api.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var user = new User { Email = api.NewEmail(), TimeZone = "UTC", Locale = "en" };
                db.Users.Add(user);
                await db.SaveChangesAsync();
                db.Strategies.Add(new Strategy
                {
                    UserId = user.Id, TemplateKey = "dip", Name = $"Count {symbol}", Mode = StrategyMode.Simulation, Status = StrategyStatus.Running,
                    StartedOn = new DateOnly(2026, 10, 1), Currency = "USD", Budget = new Budget(3000, 100, 5), Params = TemplateCatalog.Dip.ParamsJson(),
                    Assets = [new StrategyAsset { AssetId = id, Active = false, AddedAt = DateTimeOffset.UtcNow }],
                });
                await db.SaveChangesAsync();
            }
            var counted = (await admin.GetFromJsonAsync<JsonElement>("/admin/assets")).EnumerateArray().Single(a => a.GetProperty("id").GetGuid() == id);
            Assert.Equal(1, counted.GetProperty("strategies").GetInt32());

            var edit = await admin.PutAsJsonAsync($"/admin/assets/{id}", new { name = " Example ", what = "Machines for factories" });
            Assert.Equal(HttpStatusCode.NoContent, edit.StatusCode);
            var edited = (await admin.GetFromJsonAsync<JsonElement>("/admin/assets")).EnumerateArray().Single(a => a.GetProperty("id").GetGuid() == id);
            Assert.Equal("Example", edited.GetProperty("name").GetString());
            Assert.Equal("Machines for factories", edited.GetProperty("what").GetString());

            var blank = await admin.PutAsJsonAsync($"/admin/assets/{id}", new { name = "", what = new string('x', 201) });
            Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
            var errors = (await blank.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
            Assert.Equal("Type something.", errors.GetProperty("name")[0].GetString());
            Assert.Equal("Use at most 200 characters.", errors.GetProperty("what")[0].GetString());
            Assert.Equal(HttpStatusCode.NotFound,
                (await admin.PutAsJsonAsync($"/admin/assets/{Guid.NewGuid()}", new { name = "x", what = "y" })).StatusCode);
        }
        finally
        {
            using var scope = api.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Strategies.Where(s => s.Name == $"Count {symbol}").ExecuteDeleteAsync();
            await db.Assets.Where(a => a.Symbol == symbol).ExecuteDeleteAsync();
        }
    }

    // Deleting every price and downloading every rate aren't run here: they would change the shared dev database's real rows.
    [NeonFact]
    public async Task Downloads_and_deletes_one_assets_prices()
    {
        var symbol = $"T{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var source = new FakeSource(symbol);
        await using var api = new Api(source);
        using var admin = await api.OwnerAsync();
        try
        {
            var added = await (await admin.PostAsJsonAsync("/admin/assets", new { symbol })).Content.ReadFromJsonAsync<JsonElement>();
            var id = added.GetProperty("asset").GetProperty("id").GetGuid();

            var downloaded = await admin.PostAsJsonAsync($"/admin/assets/{id}/prices", new { years = 3 });

            Assert.Equal(HttpStatusCode.OK, downloaded.StatusCode);
            var row = await downloaded.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("2026-09-30", row.GetProperty("latestPriceDate").GetString());
            Assert.Equal(JsonValueKind.Null, row.GetProperty("pricesProblem").ValueKind);
            Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-3), source.Since);

            var noYears = await admin.PostAsJsonAsync($"/admin/assets/{id}/prices", new { years = 0 });
            Assert.Equal(HttpStatusCode.BadRequest, noYears.StatusCode);
            Assert.Equal("Choose 1 to 30 years.",
                (await noYears.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").GetProperty("years")[0].GetString());

            Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/admin/assets/{id}/prices")).StatusCode);
            var emptied = (await admin.GetFromJsonAsync<JsonElement>("/admin/assets")).EnumerateArray().Single(a => a.GetProperty("id").GetGuid() == id);
            Assert.Equal(JsonValueKind.Null, emptied.GetProperty("latestPriceDate").ValueKind);

            Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"/admin/assets/{Guid.NewGuid()}/prices")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync($"/admin/assets/{Guid.NewGuid()}/prices", new { years = 2 })).StatusCode);
        }
        finally
        {
            using var scope = api.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Assets.Where(a => a.Symbol == symbol).ExecuteDeleteAsync();
        }
    }

    [NeonFact]
    public async Task Adding_says_why_a_symbol_is_refused()
    {
        await using var api = new Api(new FakeSource("KNOWN"));
        using var admin = await api.OwnerAsync();

        async Task<string?> Refusal(string? symbol)
        {
            var response = await admin.PostAsJsonAsync("/admin/assets", new { symbol });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").GetProperty("symbol")[0].GetString();
        }

        Assert.Equal("Type a symbol.", await Refusal(" "));
        Assert.Equal("Type one symbol, as Yahoo writes it (AAPL, SAP.DE).", await Refusal("AAPL MSFT"));
        Assert.Equal("Yahoo has no stock, fund or index with the symbol UNKNOWN.", await Refusal("unknown"));
    }
}
