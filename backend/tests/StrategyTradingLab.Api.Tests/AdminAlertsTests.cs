using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StrategyTradingLab.Api.Admin;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Evaluation;

namespace StrategyTradingLab.Api.Tests;

public class UsSessionWeekdaysTests
{
    [Theory]
    [InlineData("2026-10-23", "2026-10-23", 0)] // The same day.
    [InlineData("2026-10-22", "2026-10-23", 1)] // Thursday to Friday.
    [InlineData("2026-10-23", "2026-10-26", 1)] // Friday to Monday: the weekend doesn't count.
    [InlineData("2026-10-16", "2026-10-26", 6)]
    [InlineData("2026-10-26", "2026-10-23", 0)] // Ahead of the session.
    public void Weekdays_between_two_days_skip_the_weekend(string from, string to, int expected) =>
        Assert.Equal(expected, UsSession.WeekdaysBetween(DateOnly.Parse(from), DateOnly.Parse(to)));
}

public class AdminAlertStorageTests
{
    [Fact]
    public void The_upserts_status_words_are_those_EF_stores()
    {
        // The upsert is raw SQL: if these drift from the converter, every raise would fail (and only be logged).
        Assert.Equal(["open", "dismissed", "cleared"], Data.SnakeCaseEnumConverter<AlertStatus>.StoredValues);
        Assert.Equal("ai_strategies", AdminAlerts.Stored(AlertSource.AiStrategies));
        Assert.Equal(Data.SnakeCaseEnumConverter<AlertSource>.StoredValues, Enum.GetValues<AlertSource>().Select(s => AdminAlerts.Stored(s)));
    }
}

/// <summary>The owner's alerts (/admin/alerts): raised by the jobs, counted, dismissed, cleared and opened again.</summary>
public class AdminAlertsTests(AdminFixture fixture) : IClassFixture<AdminFixture>
{
    AdminAlerts Alerts(Data.AppDbContext db) => new(db, TimeProvider.System, NullLogger<AdminAlerts>.Instance);

    async Task<JsonElement?> Listed(string title) =>
        (await fixture.Admin.GetFromJsonAsync<JsonElement>("/admin/alerts")).EnumerateArray()
            .Cast<JsonElement?>().SingleOrDefault(a => a!.Value.GetProperty("title").GetString() == title);

    [NeonFact]
    public async Task Alerts_need_the_owner_role()
    {
        using var stranger = fixture.CreateApiClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await stranger.GetAsync("/admin/alerts")).StatusCode);
        using var user = await fixture.SignedUpAsync(UserRole.User);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/admin/alerts")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.PostAsync($"/admin/alerts/{Guid.NewGuid()}/dismiss", null)).StatusCode);
    }

    [NeonFact]
    public async Task An_alert_counts_up_stays_quiet_once_dismissed_and_opens_again_after_it_cleared()
    {
        var db = fixture.Db();
        var key = $"test:{Guid.NewGuid():N}";
        var title = $"Test alert {key}";
        // Its own source would clear other tests' alerts: the test's alert uses one no job of the test server runs.
        var source = AlertSource.DailyCheck;
        try
        {
            await Alerts(db).RaiseAsync(source, new NewAlert(key, title, "First"));
            await Alerts(db).RaiseAsync(source, new NewAlert(key, title, "Second"));
            var alert = (await Listed(title))!.Value;
            Assert.Equal(("open", 2, "Second"), (alert.GetProperty("status").GetString(), alert.GetProperty("count").GetInt32(),
                alert.GetProperty("detail").GetString()));

            var id = alert.GetProperty("id").GetGuid();
            Assert.Equal(HttpStatusCode.NoContent, (await fixture.Admin.PostAsync($"/admin/alerts/{id}/dismiss", null)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await fixture.Admin.PostAsync($"/admin/alerts/{id}/dismiss", null)).StatusCode);
            await Alerts(db).RaiseAsync(source, new NewAlert(key, title, "Third"));
            alert = (await Listed(title))!.Value;
            Assert.Equal(("dismissed", 3), (alert.GetProperty("status").GetString(), alert.GetProperty("count").GetInt32()));

            await Alerts(db).ClearAsync(key);
            Assert.Equal("cleared", (await Listed(title))!.Value.GetProperty("status").GetString());

            await Alerts(db).RaiseAsync(source, new NewAlert(key, title, "Back"));
            alert = (await Listed(title))!.Value;
            Assert.Equal(("open", 1), (alert.GetProperty("status").GetString(), alert.GetProperty("count").GetInt32()));
            Assert.Equal(JsonValueKind.Null, alert.GetProperty("closedAt").ValueKind);
        }
        finally
        {
            await db.AdminAlerts.Where(a => a.Key == key).ExecuteDeleteAsync();
        }
    }

    [NeonFact]
    public async Task Long_texts_are_cut_to_fit()
    {
        var db = fixture.Db();
        var key = $"test:{Guid.NewGuid():N}";
        try
        {
            await Alerts(db).RaiseAsync(AlertSource.DailyCheck, new NewAlert(key, new string('t', 500), new string('d', 5000)));
            var alert = await db.AdminAlerts.AsNoTracking().SingleAsync(a => a.Key == key);
            Assert.Equal((AdminAlerts.TitleMax, AdminAlerts.DetailMax), (alert.Title.Length, alert.Detail!.Length));

            // An emoji across the cut is dropped whole: half of one would make the write fail and the alert be lost.
            var title = new string('t', AdminAlerts.TitleMax - 2) + "😀😀";
            await Alerts(db).RaiseAsync(AlertSource.DailyCheck, new NewAlert(key, title));
            Assert.Equal(new string('t', AdminAlerts.TitleMax - 2) + "…",
                (await db.AdminAlerts.AsNoTracking().SingleAsync(a => a.Key == key)).Title);
        }
        finally
        {
            await db.AdminAlerts.Where(a => a.Key == key).ExecuteDeleteAsync();
        }
    }
}
