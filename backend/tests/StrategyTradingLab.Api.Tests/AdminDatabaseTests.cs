using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace StrategyTradingLab.Api.Tests;

/// <summary>An API with its own state directory, deleted afterwards.</summary>
public sealed class DatabaseFixture : WebApplicationFactory<Program>
{
    public async Task<HttpClient> LoggedIn(WebApplicationFactory<Program>? factory = null)
    {
        var client = (factory ?? this).CreateClient();
        (await client.PostAsJsonAsync("/admin/log-in", new { password = "STL" })).EnsureSuccessStatusCode();
        return client;
    }

    /// <summary>A host with its own state directory and these settings, so no test sees another's database.json.</summary>
    public WebApplicationFactory<Program> With(TemporaryDirectory state, params (string Key, string Value)[] settings) =>
        WithWebHostBuilder(builder =>
        {
            builder.UseSetting("STATE_DIRECTORY", state.Path);
            foreach (var (key, value) in settings) builder.UseSetting(key, value);
        });
}

public sealed class TemporaryDirectory : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("stl-state-").FullName;
    public void Dispose() => Directory.Delete(Path, recursive: true);
}

/// <summary>
/// The admin page's database box. Both URLs come from the server's environment; the page only switches.
/// Switching to a database that answers is in the [NeonFact] test.
/// </summary>
public class AdminDatabaseTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    // Never reached: .invalid names don't resolve. Not DATABASE_URL, which .env.local (added after test settings) would replace.
    const string Unreachable = "postgresql://owner:s3cret-pw@ep-test-pooler.eu-central-1.aws.neon.invalid.neon.tech/neondb?sslmode=require";

    static async Task<string?> Problem(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString();

    static JsonElement Database(JsonElement databases, string name) =>
        databases.GetProperty("databases").EnumerateArray().Single(d => d.GetProperty("name").GetString() == name);

    static async Task<string?> InUse(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonElement>("/version")).GetProperty("database").GetString();

    [Fact]
    public async Task The_database_box_needs_the_admin_cookie()
    {
        using var stranger = fixture.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.GetAsync("/admin/database")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.PostAsJsonAsync("/admin/database/active", new { name = "production" })).StatusCode);
    }

    [Fact]
    public async Task Locally_the_api_uses_dev_and_each_url_comes_from_a_server_variable()
    {
        using var state = new TemporaryDirectory();
        using var admin = await fixture.LoggedIn(fixture.With(state, ("DATABASE_URL_PRODUCTION", Unreachable)));

        var body = await admin.GetStringAsync("/admin/database");
        var databases = JsonDocument.Parse(body).RootElement;

        Assert.Equal("dev", databases.GetProperty("active").GetString());
        Assert.Equal("dev", databases.GetProperty("home").GetString());
        Assert.Equal("DATABASE_URL", Database(databases, "dev").GetProperty("variable").GetString());
        Assert.Equal("DATABASE_URL_PRODUCTION", Database(databases, "production").GetProperty("variable").GetString());
        Assert.Equal("ep-test-pooler.eu-central-1.aws.neon.invalid.neon.tech", Database(databases, "production").GetProperty("host").GetString());
        Assert.DoesNotContain("s3cret", body);
        Assert.Equal("dev", await InUse(admin));
    }

    [Fact]
    public async Task Urls_cant_be_set_on_the_page()
    {
        using var admin = await fixture.LoggedIn();

        Assert.False((await admin.PutAsJsonAsync("/admin/database/production/url", new { url = Unreachable })).IsSuccessStatusCode);
        Assert.False((await admin.DeleteAsync("/admin/database/production/url")).IsSuccessStatusCode);
    }

    [Fact]
    public async Task A_database_without_a_url_is_never_switched_to()
    {
        using var state = new TemporaryDirectory();
        using var admin = await fixture.LoggedIn(fixture.With(state));

        var response = await admin.PostAsJsonAsync("/admin/database/active", new { name = "production" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("The production database has no URL: set DATABASE_URL_PRODUCTION on the server.", await Problem(response));
        Assert.False(File.Exists(Path.Combine(state.Path, "database.json")));
    }

    [Fact]
    public async Task A_database_that_does_not_answer_is_never_switched_to()
    {
        using var state = new TemporaryDirectory();
        using var admin = await fixture.LoggedIn(fixture.With(state, ("DATABASE_URL_PRODUCTION", Unreachable)));

        var response = await admin.PostAsJsonAsync("/admin/database/active", new { name = "production" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var reason = await Problem(response);
        Assert.StartsWith("Can't reach the production database", reason);
        Assert.DoesNotContain("s3cret", reason);
        Assert.Equal("dev", await InUse(admin));
    }

    [Fact]
    public async Task Unknown_databases_are_not_found()
    {
        using var admin = await fixture.LoggedIn();

        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync("/admin/database/active", new { name = "1" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync("/admin/database/active", new { name = "staging" })).StatusCode);
    }

    [Fact]
    public async Task A_saved_choice_is_used_on_start_while_its_variable_is_set()
    {
        using var state = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(state.Path, "database.json"), """{"Active":1}""");

        using var withUrl = fixture.With(state, ("DATABASE_URL_PRODUCTION", Unreachable)).CreateClient();
        using var withoutUrl = fixture.With(state).CreateClient();

        Assert.Equal("production", await InUse(withUrl));
        Assert.Equal("dev", await InUse(withoutUrl));
    }

    [Fact]
    public async Task An_unreadable_choice_falls_back_to_the_home_database()
    {
        using var state = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(state.Path, "database.json"), "not json");

        using var client = fixture.With(state, ("DATABASE_URL_PRODUCTION", Unreachable)).CreateClient();

        Assert.Equal("dev", await InUse(client));
    }

    [Fact]
    public async Task On_the_server_nothing_switches_while_the_admin_password_is_the_default()
    {
        using var state = new TemporaryDirectory();
        using var server = fixture.With(state, ("ConnectionStrings:Default", "Host=localhost;Database=unused"))
            .WithWebHostBuilder(b => b.UseEnvironment("Production"));
        using var admin = await fixture.LoggedIn(server);

        var response = await admin.PostAsJsonAsync("/admin/database/active", new { name = "dev" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("Set ADMIN_PASSWORD on the server before changing the database here.", await Problem(response));
        var databases = await admin.GetFromJsonAsync<JsonElement>("/admin/database");
        Assert.Equal("production", databases.GetProperty("home").GetString());
        Assert.Equal("DATABASE_URL_DEV", Database(databases, "dev").GetProperty("variable").GetString());
    }

    [NeonFact]
    public async Task Switching_to_a_database_that_answers_moves_the_api_there_and_is_kept()
    {
        // The linked dev branch stands in for production; dev itself points nowhere.
        var devUrl = fixture.Services.GetRequiredService<IConfiguration>()["DATABASE_URL"]!;
        using var state = new TemporaryDirectory();
        var settings = new[] { ("DATABASE_URL_PRODUCTION", devUrl), ("ConnectionStrings:Default", "Host=unused.invalid;Database=unused") };
        using var admin = await fixture.LoggedIn(fixture.With(state, settings));

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync("/admin/database/active", new { name = "production" })).StatusCode);
        Assert.Equal("production", await InUse(admin));
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/health")).StatusCode);

        using var restarted = fixture.With(state, settings).CreateClient();
        Assert.Equal("production", await InUse(restarted));
    }
}
