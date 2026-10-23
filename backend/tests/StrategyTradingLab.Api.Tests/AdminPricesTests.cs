using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using StrategyTradingLab.Api.MarketData;

namespace StrategyTradingLab.Api.Tests;

/// <summary>The admin page's Yahoo test and Refresh now, with a fake source (no database, no network).</summary>
public class AdminPricesTests
{
    sealed class FakeSource(Func<string, IReadOnlyList<Candle>> days) : IMarketDataSource
    {
        public Task<IReadOnlyList<Candle>> DailyAsync(string symbol, bool fullHistory, bool adjusted, CancellationToken cancellationToken) =>
            Task.FromResult(days(symbol));
    }

    static WebApplicationFactory<Program> Api(IMarketDataSource source) => new WebApplicationFactory<Program>()
        .WithWebHostBuilder(b => b.ConfigureTestServices(services => services.AddSingleton(source)));

    static async Task<HttpClient> Admin(WebApplicationFactory<Program> api)
    {
        var client = api.CreateApiClient();
        (await client.PostAsJsonAsync("/admin/log-in", new { password = "STL" })).EnsureSuccessStatusCode();
        return client;
    }

    [Fact]
    public async Task The_test_says_what_Yahoo_answered_for_each_symbol_and_stores_nothing()
    {
        using var api = Api(new FakeSource(symbol => symbol == "SPY"
            ? [new Candle(new DateOnly(2026, 10, 22), 500, 510, 495, 505.5m, 1000)]
            : throw new MarketDataException("Yahoo answered HTTP 429 for EURUSD=X.")));
        using var admin = await Admin(api);

        var response = await admin.PostAsync("/admin/prices/test", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var checks = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        Assert.Equal("SPY", checks[0].GetProperty("symbol").GetString());
        Assert.True(checks[0].GetProperty("ok").GetBoolean());
        Assert.Equal("2026-10-22", checks[0].GetProperty("day").GetString());
        Assert.Equal(505.5m, checks[0].GetProperty("close").GetDecimal());
        Assert.False(checks[1].GetProperty("ok").GetBoolean());
        Assert.Equal("Yahoo answered HTTP 429 for EURUSD=X.", checks[1].GetProperty("problem").GetString());
    }

    [Fact]
    public async Task An_empty_answer_is_not_ok()
    {
        using var api = Api(new FakeSource(_ => []));
        using var admin = await Admin(api);

        var checks = (await (await admin.PostAsync("/admin/prices/test", null)).Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray();

        Assert.All(checks, check => Assert.False(check.GetProperty("ok").GetBoolean()));
    }

    [Fact]
    public async Task Refresh_now_is_accepted_once_and_the_second_ask_is_refused()
    {
        using var api = Api(new FakeSource(_ => []));
        using var admin = await Admin(api);

        Assert.Equal(HttpStatusCode.Accepted, (await admin.PostAsync("/admin/prices/refresh", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync("/admin/prices/refresh", null)).StatusCode);
    }

    [Fact]
    public async Task The_price_endpoints_need_the_admin_login()
    {
        using var api = Api(new FakeSource(_ => []));
        using var visitor = api.CreateApiClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.PostAsync("/admin/prices/test", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.PostAsync("/admin/prices/refresh", null)).StatusCode);
    }
}
