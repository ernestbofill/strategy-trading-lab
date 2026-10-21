using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using StrategyTradingLab.Api.Admin;

namespace StrategyTradingLab.Api.Tests;

/// <summary>An API with its own state directory, so the database file starts empty and is deleted afterwards.</summary>
public sealed class DatabaseFixture : WebApplicationFactory<Program>
{
    public readonly string StateDirectory = Directory.CreateTempSubdirectory("stl-state-").FullName;

    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseSetting("STATE_DIRECTORY", StateDirectory);

    public async Task<HttpClient> LoggedIn(WebApplicationFactory<Program>? factory = null)
    {
        var client = (factory ?? this).CreateClient();
        (await client.PostAsJsonAsync("/admin/log-in", new { password = "STL" })).EnsureSuccessStatusCode();
        return client;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        Directory.Delete(StateDirectory, recursive: true);
    }
}

/// <summary>The admin page's database box. Switching to a database that answers is in the [NeonFact] test.</summary>
public class AdminDatabaseTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    // Never reached: .invalid names don't resolve.
    const string ProductionUrl = "postgresql://owner:s3cret-pw@ep-test-pooler.eu-central-1.aws.neon.invalid.neon.tech/neondb?sslmode=require";

    static async Task<string?> Problem(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString();

    [Fact]
    public async Task The_database_box_needs_the_admin_cookie()
    {
        using var stranger = fixture.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.GetAsync("/admin/database")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.PostAsJsonAsync("/admin/database/active", new { name = "production" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.PutAsJsonAsync("/admin/database/production/url", new { url = ProductionUrl })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.DeleteAsync("/admin/database/production/url")).StatusCode);
    }

    [Fact]
    public async Task Locally_the_api_uses_the_dev_database_and_says_so_in_its_version()
    {
        using var admin = await fixture.LoggedIn();

        var databases = await admin.GetFromJsonAsync<JsonElement>("/admin/database");
        var version = await admin.GetFromJsonAsync<JsonElement>("/version");

        Assert.Equal("dev", databases.GetProperty("active").GetString());
        Assert.Equal("dev", databases.GetProperty("home").GetString());
        Assert.Equal("dev", version.GetProperty("database").GetString());
    }

    [Fact]
    public async Task A_url_is_kept_encrypted_and_only_its_host_is_shown()
    {
        using var admin = await fixture.LoggedIn();

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync("/admin/database/production/url", new { url = $" {ProductionUrl} " })).StatusCode);

        var body = await admin.GetStringAsync("/admin/database");
        Assert.DoesNotContain("s3cret", body);
        var production = JsonDocument.Parse(body).RootElement.GetProperty("databases").EnumerateArray()
            .Single(d => d.GetProperty("name").GetString() == "production");
        Assert.Equal("page", production.GetProperty("source").GetString());
        Assert.Equal("ep-test-pooler.eu-central-1.aws.neon.invalid.neon.tech", production.GetProperty("host").GetString());
        Assert.DoesNotContain("s3cret", await File.ReadAllTextAsync(Path.Combine(fixture.StateDirectory, "database.json")));

        // A restart (a new host) reads it back.
        using var restarted = fixture.WithWebHostBuilder(_ => { });
        using var again = await fixture.LoggedIn(restarted);
        Assert.Contains("ep-test-pooler", await again.GetStringAsync("/admin/database"));

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync("/admin/database/production/url")).StatusCode);
        Assert.DoesNotContain("ep-test-pooler", await admin.GetStringAsync("/admin/database"));
    }

    [Fact]
    public async Task A_database_without_a_url_or_that_does_not_answer_is_never_switched_to()
    {
        using var admin = await fixture.LoggedIn();
        await admin.DeleteAsync("/admin/database/production/url");

        var noUrl = await admin.PostAsJsonAsync("/admin/database/active", new { name = "production" });
        Assert.Equal(HttpStatusCode.BadRequest, noUrl.StatusCode);
        Assert.Equal("The production database has no URL: set one first.", await Problem(noUrl));

        await admin.PutAsJsonAsync("/admin/database/production/url", new { url = ProductionUrl });
        var unreachable = await admin.PostAsJsonAsync("/admin/database/active", new { name = "production" });
        Assert.Equal(HttpStatusCode.BadRequest, unreachable.StatusCode);
        var reason = await Problem(unreachable);
        Assert.StartsWith("Can't reach the production database", reason);
        Assert.DoesNotContain("s3cret", reason);

        Assert.Equal("dev", (await admin.GetFromJsonAsync<JsonElement>("/version")).GetProperty("database").GetString());
        await admin.DeleteAsync("/admin/database/production/url");
    }

    [Fact]
    public async Task Bad_urls_and_unknown_databases_are_refused()
    {
        using var admin = await fixture.LoggedIn();

        var elsewhere = await admin.PutAsJsonAsync("/admin/database/production/url", new { url = "postgresql://u:p@db.example.com/neondb" });
        Assert.Equal(HttpStatusCode.BadRequest, elsewhere.StatusCode);
        Assert.Equal("Use a Neon database (a host ending in .neon.tech).", await Problem(elsewhere));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/admin/database/production/url", new { url = "not a url" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync("/admin/database/active", new { name = "1" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync("/admin/database/1/url", new { url = ProductionUrl })).StatusCode);
    }

    [Fact]
    public async Task On_the_server_nothing_changes_while_the_admin_password_is_the_default()
    {
        using var server = fixture.WithWebHostBuilder(b => b.UseEnvironment("Production")
            .UseSetting("ConnectionStrings:Default", "Host=localhost;Database=unused"));
        using var admin = await fixture.LoggedIn(server);

        var response = await admin.PutAsJsonAsync("/admin/database/dev/url", new { url = ProductionUrl });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("Set ADMIN_PASSWORD on the server before changing the database here.", await Problem(response));
        var databases = await admin.GetFromJsonAsync<JsonElement>("/admin/database");
        Assert.Equal("production", databases.GetProperty("home").GetString());
    }

    [NeonFact]
    public async Task Switching_to_a_database_that_answers_moves_the_api_there_and_back()
    {
        using var admin = await fixture.LoggedIn();
        // The linked dev branch stands in for production here.
        var devUrl = Environment.GetEnvironmentVariable("DATABASE_URL")!;
        await admin.PutAsJsonAsync("/admin/database/production/url", new { url = devUrl });

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync("/admin/database/active", new { name = "production" })).StatusCode);
        Assert.Equal("production", (await admin.GetFromJsonAsync<JsonElement>("/version")).GetProperty("database").GetString());
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/health")).StatusCode);

        var clearActive = await admin.DeleteAsync("/admin/database/production/url");
        Assert.Equal(HttpStatusCode.BadRequest, clearActive.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync("/admin/database/active", new { name = "dev" })).StatusCode);
        Assert.Equal("dev", (await admin.GetFromJsonAsync<JsonElement>("/version")).GetProperty("database").GetString());
        await admin.DeleteAsync("/admin/database/production/url");
    }
}

public class DatabaseUrlTests
{
    [Theory]
    [InlineData("postgresql://u:p@ep-x-pooler.eu-central-1.aws.neon.tech/neondb?sslmode=require&channel_binding=require", true)]
    [InlineData("postgres://u:p@ep-x.neon.tech/stl", true)]
    [InlineData("postgresql://u:p@db.example.com/neondb", false)]
    [InlineData("postgresql://u:p@neon.tech.example.com/neondb", false)]
    [InlineData("postgresql://u@ep-x.neon.tech/neondb", false)]
    [InlineData("postgresql://u:p@ep-x.neon.tech/", false)]
    [InlineData("https://u:p@ep-x.neon.tech/neondb", false)]
    [InlineData("Host=ep-x.neon.tech;Database=neondb", false)]
    public void Only_neon_urls_with_a_user_password_and_database_are_taken(string url, bool valid) =>
        Assert.Equal(valid, DatabaseSwitch.CheckUrl(url) is null);
}
