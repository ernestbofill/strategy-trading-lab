using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace StrategyTradingLab.Api.Tests;

public class VersionTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    record VersionResponse(string Version);

    [Fact]
    public async Task Version_reports_the_backend_version()
    {
        var response = await factory.CreateClient().GetFromJsonAsync<VersionResponse>("/version");

        Assert.Matches(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.]+)?(\+\w+)?$", response!.Version);
    }
}
