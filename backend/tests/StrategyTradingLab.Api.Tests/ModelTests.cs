using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Tests;

/// <summary>The EF model against backend/DATA-MODEL.md. Builds the model only; no database.</summary>
public class ModelTests
{
    static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        AppDbContextOptions.Configure(options, "Host=localhost;Database=unused");
        return new AppDbContext(options.Options);
    }

    static readonly IModel Model = CreateContext().GetService<IDesignTimeModel>().Model;

    // Fails when an entity or convention changed without a migration (`dotnet ef migrations add`).
    [Fact]
    public void Has_no_model_changes_without_a_migration()
    {
        using var context = CreateContext();
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public void Has_exactly_the_data_model_tables()
    {
        var tables = Model.GetEntityTypes().Select(entity => entity.GetTableName()).Order();

        Assert.Equal(
            ["ai_rounds", "app_settings", "asset_texts", "assets", "daily_evaluations", "email_tokens", "feedback_items", "fx_rates", "invites", "notifications", "prices_daily", "push_subscriptions", "sessions", "shared_ai_rounds", "signal_trades", "signals",
             "strategies", "strategy_assets", "user_trades", "users"],
            tables);
    }

    [Fact]
    public void Keys_are_generated_on_the_client()
    {
        foreach (var entity in Model.GetEntityTypes())
        {
            foreach (var key in entity.FindPrimaryKey()!.Properties.Where(p => p.ClrType == typeof(Guid)))
            {
                Assert.Null(key.GetDefaultValueSql());
            }
        }
    }

    [Fact]
    public void Enums_are_snake_case_text()
    {
        var reason = Model.FindEntityType(typeof(Signal))!.FindProperty(nameof(Signal.SkipReason))!;

        Assert.Equal("skip_reason", reason.GetColumnName());
        Assert.Equal("text", reason.GetColumnType());
    }

    [Fact]
    public void Budget_is_a_complex_type_in_three_columns()
    {
        var budget = Model.FindEntityType(typeof(Strategy))!.FindComplexProperty(nameof(Strategy.Budget))!;

        Assert.Equal(
            ["budget_amount", "budget_max_steps", "budget_step"],
            budget.ComplexType.GetProperties().Select(p => p.GetColumnName()).Order());
    }

    [Fact]
    public void Notifications_dedupe_treats_a_missing_subject_as_equal()
    {
        var index = Model.FindEntityType(typeof(Notification))!.GetIndexes().Single(i => i.IsUnique);

        Assert.False(index.GetAreNullsDistinct());
        Assert.Equal(["user_id", "kind", "channel", "for_date", "signal_id"], index.Properties.Select(p => p.GetColumnName()));
    }

    [Fact]
    public void Signal_lifecycle_checks_are_there()
    {
        var checks = Model.FindEntityType(typeof(Signal))!.GetCheckConstraints().Select(c => c.Name);

        Assert.Contains("ck_signals_answered_at", checks);
        Assert.Contains("ck_signals_step_no", checks);
        Assert.Contains("ck_signals_skip_reason", checks);
    }

    [Fact]
    public void Updated_at_is_stamped_on_added_and_changed_rows()
    {
        using var context = CreateContext();
        var user = new User { Email = "a@example.com", TimeZone = "Europe/Madrid", Locale = "en" };
        context.Users.Add(user);
        var strategy = new Strategy
        {
            Id = Guid.CreateVersion7(), UserId = user.Id, TemplateKey = "dip", Name = "Dip", Currency = "EUR",
            Budget = new Budget(3000, 300, 10), Params = JsonDocument.Parse("{}"),
        };
        context.Strategies.Attach(strategy);
        var now = new DateTimeOffset(2026, 10, 18, 9, 0, 0, TimeSpan.Zero);

        UpdatedAtInterceptor.Stamp(context.ChangeTracker, now);
        Assert.Equal(now, user.UpdatedAt);
        Assert.NotEqual(now, strategy.UpdatedAt); // unchanged

        strategy.Name = "Dip 2";
        context.ChangeTracker.DetectChanges();
        UpdatedAtInterceptor.Stamp(context.ChangeTracker, now);
        Assert.Equal(now, strategy.UpdatedAt);
    }
}
