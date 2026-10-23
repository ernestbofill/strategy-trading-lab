using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using StrategyTradingLab.Api.MarketData;

namespace StrategyTradingLab.Api.Tests;

/// <summary>The admin page's Yahoo test and Refresh now, with a fake source (no network).</summary>
public class AdminPricesTests
{
    sealed class FakeSource(Func<string, IReadOnlyList<Candle>> days) : IMarketDataSource
    {
        public Task<IReadOnlyList<Candle>> DailyAsync(string symbol, bool fullHistory, bool adjusted, CancellationToken cancellationToken) =>
            Task.FromResult(days(symbol));
    }

    sealed class Api(IMarketDataSource source) : AccountsApi
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services => services.AddSingleton(source));
        }
    }

    static Task<HttpClient> Admin(Api api) => api.OwnerAsync();

    [NeonFact]
    public async Task The_test_says_what_Yahoo_answered_for_each_symbol_and_stores_nothing()
    {
        using var api = new Api(new FakeSource(symbol => symbol == "SPY"
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

    [NeonFact]
    public async Task An_empty_answer_is_not_ok()
    {
        using var api = new Api(new FakeSource(_ => []));
        using var admin = await Admin(api);

        var checks = (await (await admin.PostAsync("/admin/prices/test", null)).Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray();

        Assert.All(checks, check => Assert.False(check.GetProperty("ok").GetBoolean()));
    }

    [NeonFact]
    public async Task Refresh_now_is_accepted()
    {
        using var api = new Api(new FakeSource(_ => []));
        using var admin = await Admin(api);

        // A second ask while it runs is refused (PriceRefreshState tests): here the worker has no database to run on.
        Assert.Equal(HttpStatusCode.Accepted, (await admin.PostAsync("/admin/prices/refresh", null)).StatusCode);
    }

    [NeonFact]
    public async Task The_price_endpoints_need_the_admin_login()
    {
        using var api = new Api(new FakeSource(_ => []));
        using var visitor = api.CreateApiClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.PostAsync("/admin/prices/test", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.PostAsync("/admin/prices/refresh", null)).StatusCode);
    }
}
