using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace StrategyTradingLab.Api.Tests;

public class VersionTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    record VersionResponse(string Version, string? Commit, DateTimeOffset? BuiltAt);

    [Fact]
    public async Task Version_reports_the_version_apart_from_the_commit()
    {
        var response = await factory.CreateClient().GetFromJsonAsync<VersionResponse>("/version");

        Assert.Matches(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.]+)?$", response!.Version);
        if (response.Commit is not null) Assert.Matches("^[0-9a-f]{40}$", response.Commit);
#if DEBUG
        Assert.Null(response.BuiltAt);
#else
        Assert.InRange(response.BuiltAt!.Value, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow);
#endif
    }
}
