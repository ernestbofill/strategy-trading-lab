using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StrategyTradingLab.Api.Admin;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Tests;

/// <summary>One API and one logged-in client for the class; the settings rows are put back as they were afterwards.</summary>
public sealed class AdminFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    List<AppSetting> saved = [];
    public HttpClient Admin { get; private set; } = null!;

    public AppDbContext Db() => Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();

    public async Task InitializeAsync()
    {
        if (!NeonFactAttribute.Configured) return;
        saved = await Db().AppSettings.AsNoTracking().ToListAsync();
        Admin = CreateClient();
        (await Admin.PostAsJsonAsync("/admin/log-in", new { password = "STL" })).EnsureSuccessStatusCode();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (NeonFactAttribute.Configured)
        {
            var db = Db();
            await db.AppSettings.ExecuteDeleteAsync();
            db.AppSettings.AddRange(saved);
            await db.SaveChangesAsync();
        }
        await base.DisposeAsync();
    }
}

/// <summary>The admin settings page's API, end to end against the linked Neon branch.</summary>
public class AdminSettingsTests(AdminFixture fixture) : IClassFixture<AdminFixture>
{
    async Task<JsonElement> Setting(string key) =>
        (await fixture.Admin.GetFromJsonAsync<JsonElement>("/admin/settings")).EnumerateArray().Single(s => s.GetProperty("key").GetString() == key);

    [NeonFact]
    public async Task Settings_need_the_admin_cookie()
    {
        using var stranger = fixture.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.GetAsync("/admin/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.PutAsJsonAsync("/admin/settings/SMTP_USER", new { value = "a@b.c" })).StatusCode);
        stranger.DefaultRequestHeaders.Add("Cookie", "stl_admin=forged");
        Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.GetAsync("/admin/settings")).StatusCode);
    }

    [NeonFact]
    public async Task A_wrong_password_is_refused()
    {
        using var stranger = fixture.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.PostAsJsonAsync("/admin/log-in", new { password = "stl" })).StatusCode);
    }

    [NeonFact]
    public async Task A_setting_from_the_page_wins_and_default_brings_back_the_rest()
    {
        await fixture.Admin.DeleteAsync("/admin/settings/SMTP_PORT");
        Assert.Equal("587", (await Setting("SMTP_PORT")).GetProperty("value").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await fixture.Admin.PutAsJsonAsync("/admin/settings/SMTP_PORT", new { value = " 465 " })).StatusCode);
        var port = await Setting("SMTP_PORT");
        Assert.Equal("465", port.GetProperty("value").GetString());
        Assert.Equal("page", port.GetProperty("source").GetString());

        await fixture.Admin.DeleteAsync("/admin/settings/SMTP_PORT");
        Assert.Equal("default", (await Setting("SMTP_PORT")).GetProperty("source").GetString());
    }

    [NeonFact]
    public async Task A_secret_is_never_sent_back_and_is_stored_encrypted()
    {
        Assert.Equal(HttpStatusCode.NoContent, (await fixture.Admin.PutAsJsonAsync("/admin/settings/SMTP_PASSWORD", new { value = "app password 123" })).StatusCode);

        var password = await Setting("SMTP_PASSWORD");
        Assert.Equal(JsonValueKind.Null, password.GetProperty("value").ValueKind);
        Assert.True(password.GetProperty("isSet").GetBoolean());
        var stored = await fixture.Db().AppSettings.AsNoTracking().SingleAsync(s => s.Key == "SMTP_PASSWORD");
        Assert.DoesNotContain("app password", stored.Value);

        using var scope = fixture.Services.CreateScope();
        Assert.Equal("app password 123", await scope.ServiceProvider.GetRequiredService<SettingsStore>().GetAsync("SMTP_PASSWORD", default));
    }

    [NeonFact]
    public async Task Changing_the_mail_server_clears_the_password()
    {
        await fixture.Admin.PutAsJsonAsync("/admin/settings/SMTP_HOST", new { value = "smtp.gmail.com" });
        await fixture.Admin.PutAsJsonAsync("/admin/settings/SMTP_PASSWORD", new { value = "secret" });

        await fixture.Admin.PutAsJsonAsync("/admin/settings/SMTP_HOST", new { value = "smtp.example.com" });

        var password = await Setting("SMTP_PASSWORD");
        Assert.False(password.GetProperty("isSet").GetBoolean());
        Assert.Equal("none", password.GetProperty("source").GetString());
    }

    [NeonFact]
    public async Task Resetting_the_mail_server_to_its_default_clears_the_password_too()
    {
        await fixture.Admin.PutAsJsonAsync("/admin/settings/SMTP_HOST", new { value = "smtp.gmail.com" });
        await fixture.Admin.PutAsJsonAsync("/admin/settings/SMTP_PASSWORD", new { value = "secret" });

        await fixture.Admin.DeleteAsync("/admin/settings/SMTP_HOST");

        Assert.False((await Setting("SMTP_PASSWORD")).GetProperty("isSet").GetBoolean());
    }

    [NeonFact]
    public async Task Saving_the_same_server_keeps_the_password()
    {
        await fixture.Admin.PutAsJsonAsync("/admin/settings/SMTP_HOST", new { value = "smtp.gmail.com" });
        await fixture.Admin.PutAsJsonAsync("/admin/settings/SMTP_PASSWORD", new { value = "secret" });

        await fixture.Admin.PutAsJsonAsync("/admin/settings/SMTP_HOST", new { value = "smtp.gmail.com" });

        Assert.True((await Setting("SMTP_PASSWORD")).GetProperty("isSet").GetBoolean());
    }

    [NeonFact]
    public async Task Bad_values_and_unknown_settings_are_refused()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Admin.PutAsJsonAsync("/admin/settings/SMTP_PORT", new { value = "99999" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Admin.PutAsJsonAsync("/admin/settings/SMTP_USER", new { value = "not an email" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Admin.PutAsJsonAsync("/admin/settings/SMTP_HOST", new { value = "  " })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Admin.PutAsJsonAsync("/admin/settings/DATABASE_URL", new { value = "x" })).StatusCode);
    }
}

/// <summary>Its own API, so its rate limiter counts only these tries.</summary>
public class AdminLogInRateLimitTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task The_sixth_try_in_15_minutes_is_refused()
    {
        using var client = factory.CreateClient();
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/admin/log-in", new { password = "wrong" })).StatusCode);

        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/admin/log-in", new { password = "STL" })).StatusCode);
    }
}

public class SettingDefinitionTests
{
    [Theory]
    [InlineData("SMTP_PORT", "587", true)]
    [InlineData("SMTP_PORT", "0", false)]
    [InlineData("SMTP_PORT", "abc", false)]
    [InlineData("SMTP_USER", "you@gmail.com", true)]
    [InlineData("SMTP_USER", "you@", false)]
    [InlineData("SMTP_USER", "you @gmail.com", false)]
    public void Values_are_checked(string key, string value, bool valid) =>
        Assert.Equal(valid, SettingDefinitions.Find(key)!.Validate!(value) is null);

    [Fact]
    public void Only_the_password_is_secret_and_it_is_bound_to_the_server_port_and_user()
    {
        Assert.Equal(["SMTP_PASSWORD"], SettingDefinitions.All.Where(s => s.Secret).Select(s => s.Key));
        Assert.Equal(["SMTP_USER", "SMTP_HOST", "SMTP_PORT"], SettingDefinitions.Find("SMTP_PASSWORD")!.BoundTo!);
        Assert.Null(SettingDefinitions.Find("DATABASE_URL"));
    }
}
