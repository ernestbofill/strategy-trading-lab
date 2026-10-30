using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StrategyTradingLab.Api.Auth;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Templates;

namespace StrategyTradingLab.Api.Data;

// Tables, checks and indexes from backend/DATA-MODEL.md. Column names come from the
// snake_case naming convention; CHECK and filter SQL uses them directly. Enum checks and the
// created_at / updated_at defaults are added for every table in ModelBuilderExtensions.
// Guid keys get UUID v7 values from Npgsql on the client; there is no database default.

static class Money
{
    public const string Amount = "numeric(14,2)";
    public const string Price = "numeric(14,4)";
    public const string Shares = "numeric(18,6)";
    public const string FxRate = "numeric(18,8)";
    public const string Currency = "char(3)";
}

class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> entity)
    {
        entity.ToTable("assets");
        entity.Property(asset => asset.Currency).HasColumnType(Money.Currency);
        entity.Property(asset => asset.Active).HasColumnDefault(true);
        entity.HasIndex(asset => new { asset.Symbol, asset.Exchange }).IsUnique();
        entity.HasOne<User>().WithMany().HasForeignKey(asset => asset.AddedBy).OnDelete(DeleteBehavior.SetNull);
    }
}

class AssetTextConfiguration : IEntityTypeConfiguration<AssetText>
{
    public void Configure(EntityTypeBuilder<AssetText> entity)
    {
        entity.ToTable("asset_texts");
        entity.HasKey(text => new { text.AssetId, text.Locale });
        entity.HasOne<Asset>().WithMany(asset => asset.Texts)
            .HasForeignKey(text => text.AssetId).OnDelete(DeleteBehavior.Cascade);
    }
}

class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> entity)
    {
        entity.ToTable("users");
        entity.Property(user => user.Email).HasColumnType("citext");
        entity.HasIndex(user => user.Email).IsUnique();
        entity.Property(user => user.Currency).HasColumnType(Money.Currency).HasColumnDefault("EUR");
        entity.Property(user => user.Role).HasColumnDefault(UserRole.User);
        entity.Property(user => user.OverviewEmail).HasColumnDefault(true);
        entity.Property(user => user.AiSharing).HasColumnDefault(false);
        entity.Property(user => user.FirstName).HasMaxLength(AccountRules.NameMax);
        entity.Property(user => user.LastName).HasMaxLength(AccountRules.NameMax);
        entity.HasIndex(user => user.InviteId);
        entity.HasOne<Invite>().WithMany().HasForeignKey(user => user.InviteId).OnDelete(DeleteBehavior.SetNull);
    }
}

class InviteConfiguration : IEntityTypeConfiguration<Invite>
{
    public void Configure(EntityTypeBuilder<Invite> entity)
    {
        entity.ToTable("invites", table =>
        {
            table.HasCheckConstraint("ck_invites_uses", "uses >= 0 AND uses <= max_uses");
            table.HasInCheck("locale", AccountRules.Locales);
        });
        entity.HasIndex(invite => invite.Code).IsUnique();
        entity.HasIndex(invite => invite.CreatedBy);
        entity.Property(invite => invite.Code).HasMaxLength(InviteCodes.Length);
        entity.Property(invite => invite.FirstName).HasMaxLength(AccountRules.NameMax);
        entity.Property(invite => invite.LastName).HasMaxLength(AccountRules.NameMax);
        entity.Property(invite => invite.Email).HasColumnType("citext");
        entity.Property(invite => invite.Role).HasColumnDefault(UserRole.Vip);
        entity.Property(invite => invite.MaxUses).HasColumnDefault(1);
        entity.HasOne<User>().WithMany().HasForeignKey(invite => invite.CreatedBy).OnDelete(DeleteBehavior.Cascade);
    }
}

class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
{
    public void Configure(EntityTypeBuilder<PushSubscription> entity)
    {
        entity.ToTable("push_subscriptions");
        entity.HasIndex(subscription => subscription.Endpoint).IsUnique();
        entity.HasOne<User>().WithMany()
            .HasForeignKey(subscription => subscription.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

class StrategyConfiguration : IEntityTypeConfiguration<Strategy>
{
    public void Configure(EntityTypeBuilder<Strategy> entity)
    {
        entity.ToTable("strategies", table =>
        {
            table.HasInCheck("template_key", TemplateCatalog.Keys);
            table.HasCheckConstraint("ck_strategies_ended_at", "(status = 'ended') = (ended_at IS NOT NULL)");
            table.HasCheckConstraint("ck_strategies_budget_step", "budget_step <= budget_amount");
            table.HasCheckConstraint("ck_strategies_budget_steps",
                "(budget_step IS NULL) = (budget_max_steps IS NULL)");
        });
        entity.Property(strategy => strategy.Notifications).HasColumnDefault(true);
        entity.Property(strategy => strategy.Currency).HasColumnType(Money.Currency);
        entity.ComplexProperty(strategy => strategy.Budget, budget =>
        {
            budget.Property(b => b.Amount).HasColumnName("budget_amount").HasColumnType(Money.Amount);
            budget.Property(b => b.Step).HasColumnName("budget_step").HasColumnType(Money.Amount);
            budget.Property(b => b.MaxSteps).HasColumnName("budget_max_steps");
        });
        entity.Property(strategy => strategy.Params).HasColumnType("jsonb");

        entity.HasOne<User>().WithMany()
            .HasForeignKey(strategy => strategy.UserId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<Strategy>().WithMany()
            .HasForeignKey(strategy => strategy.SourceStrategyId).OnDelete(DeleteBehavior.SetNull);
        // The unique running name, (user_id, lower(name)) WHERE status = 'running', is an
        // expression index EF can't model: it's created in the migration's SQL.
    }
}

class StrategyAssetConfiguration : IEntityTypeConfiguration<StrategyAsset>
{
    public void Configure(EntityTypeBuilder<StrategyAsset> entity)
    {
        entity.ToTable("strategy_assets", table =>
            table.HasCheckConstraint("ck_strategy_assets_removed", "removed_at IS NULL OR NOT active"));
        entity.HasKey(item => new { item.StrategyId, item.AssetId });
        entity.HasOne<Strategy>().WithMany(strategy => strategy.Assets)
            .HasForeignKey(item => item.StrategyId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<Asset>().WithMany()
            .HasForeignKey(item => item.AssetId).OnDelete(DeleteBehavior.NoAction);
    }
}

class SignalConfiguration : IEntityTypeConfiguration<Signal>
{
    public void Configure(EntityTypeBuilder<Signal> entity)
    {
        entity.ToTable("signals", table =>
        {
            table.HasCheckConstraint("ck_signals_skip_reason_skipped", "skip_reason IS NULL OR status = 'skipped'");
            table.HasCheckConstraint("ck_signals_answered_at",
                "(status IN ('confirmed', 'skipped')) = (answered_at IS NOT NULL)");
            table.HasCheckConstraint("ck_signals_snoozed_until", "snoozed_until IS NULL OR status = 'waiting'");
            table.HasCheckConstraint("ck_signals_missed_ended_on", "status <> 'missed' OR ended_on IS NOT NULL");
            table.HasCheckConstraint("ck_signals_sell_no_step",
                "side = 'buy' OR (step_no IS NULL AND max_steps IS NULL AND step_amount IS NULL)");
            table.HasCheckConstraint("ck_signals_step_together",
                "(step_no IS NULL) = (max_steps IS NULL) AND (step_no IS NULL) = (step_amount IS NULL)");
            table.HasCheckConstraint("ck_signals_step_no", "step_no IS NULL OR step_no BETWEEN 1 AND max_steps");
            table.HasCheckConstraint("ck_signals_close_price", "close_price > 0");
        });
        entity.Property(signal => signal.StepAmount).HasColumnType(Money.Amount);
        entity.Property(signal => signal.ClosePrice).HasColumnType(Money.Price);
        entity.Property(signal => signal.Facts).HasColumnType("jsonb");

        entity.HasIndex(signal => new { signal.StrategyId, signal.AssetId, signal.SignalDate, signal.Side }).IsUnique();
        entity.HasIndex(signal => new { signal.StrategyId, signal.Status });
        entity.HasIndex(signal => signal.StrategyId)
            .HasDatabaseName("ix_signals_strategy_id_waiting")
            .HasFilter("status = 'waiting'");

        entity.HasOne<Strategy>().WithMany()
            .HasForeignKey(signal => signal.StrategyId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<Asset>().WithMany()
            .HasForeignKey(signal => signal.AssetId).OnDelete(DeleteBehavior.NoAction);
    }
}

class SignalTradeConfiguration : IEntityTypeConfiguration<SignalTrade>
{
    public void Configure(EntityTypeBuilder<SignalTrade> entity)
    {
        entity.ToTable("signal_trades", table =>
        {
            table.HasTradeSignChecks();
            table.HasCheckConstraint("ck_signal_trades_step_no", "step_no IS NULL OR step_no >= 1");
        });
        entity.Property(trade => trade.Price).HasColumnType(Money.Price);
        entity.Property(trade => trade.Shares).HasColumnType(Money.Shares);
        entity.Property(trade => trade.Amount).HasColumnType(Money.Amount);
        entity.Property(trade => trade.Fee).HasColumnType(Money.Amount).HasColumnDefault(0m);
        entity.Property(trade => trade.FxRate).HasColumnType(Money.FxRate);

        entity.HasIndex(trade => trade.SignalId).IsUnique();
        entity.HasIndex(trade => new { trade.StrategyId, trade.AssetId, trade.TradeDate });

        entity.HasOne<Signal>().WithOne()
            .HasForeignKey<SignalTrade>(trade => trade.SignalId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<Strategy>().WithMany()
            .HasForeignKey(trade => trade.StrategyId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<Asset>().WithMany()
            .HasForeignKey(trade => trade.AssetId).OnDelete(DeleteBehavior.NoAction);
    }
}

class UserTradeConfiguration : IEntityTypeConfiguration<UserTrade>
{
    public void Configure(EntityTypeBuilder<UserTrade> entity)
    {
        entity.ToTable("user_trades", table =>
        {
            table.HasCheckConstraint("ck_user_trades_source_signal", "(source = 'signal') = (signal_id IS NOT NULL)");
            table.HasTradeSignChecks();
        });
        entity.Property(trade => trade.Price).HasColumnType(Money.Price);
        entity.Property(trade => trade.Amount).HasColumnType(Money.Amount);
        entity.Property(trade => trade.Shares).HasColumnType(Money.Shares);
        entity.Property(trade => trade.Fee).HasColumnType(Money.Amount).HasColumnDefault(0m);
        entity.Property(trade => trade.FxRate).HasColumnType(Money.FxRate);

        entity.HasIndex(trade => trade.SignalId).IsUnique().HasFilter("deleted_at IS NULL");
        entity.HasIndex(trade => new { trade.StrategyId, trade.AssetId, trade.TradeDate });

        entity.HasOne<Strategy>().WithMany()
            .HasForeignKey(trade => trade.StrategyId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<Asset>().WithMany()
            .HasForeignKey(trade => trade.AssetId).OnDelete(DeleteBehavior.NoAction);
        entity.HasOne<Signal>().WithMany()
            .HasForeignKey(trade => trade.SignalId).OnDelete(DeleteBehavior.NoAction);
    }
}

class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> entity)
    {
        entity.ToTable("notifications", table =>
        {
            table.HasCheckConstraint("ck_notifications_signal_id",
                "(kind IN ('signal', 'snoozed')) = (signal_id IS NOT NULL)");
        });

        // The dedupe contract: once per user, kind, channel, day and subject, a null subject included.
        entity.HasIndex(n => new { n.UserId, n.Kind, n.Channel, n.ForDate, n.SignalId })
            .IsUnique()
            .AreNullsDistinct(false);

        entity.HasOne<User>().WithMany()
            .HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<Signal>().WithMany()
            .HasForeignKey(n => n.SignalId).OnDelete(DeleteBehavior.Cascade);
    }
}

class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
{
    public void Configure(EntityTypeBuilder<AppSetting> entity)
    {
        entity.ToTable("app_settings");
        entity.HasKey(setting => setting.Key);
    }
}

class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> entity)
    {
        entity.ToTable("sessions");
        entity.HasIndex(session => session.TokenHash).IsUnique();
        entity.HasIndex(session => session.UserId);
        entity.HasOne<User>().WithMany().HasForeignKey(session => session.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

class EmailTokenConfiguration : IEntityTypeConfiguration<EmailToken>
{
    public void Configure(EntityTypeBuilder<EmailToken> entity)
    {
        entity.ToTable("email_tokens");
        entity.HasIndex(token => token.TokenHash).IsUnique();
        entity.HasIndex(token => token.UserId);
        entity.HasOne<User>().WithMany().HasForeignKey(token => token.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

class PriceDailyConfiguration : IEntityTypeConfiguration<PriceDaily>
{
    public void Configure(EntityTypeBuilder<PriceDaily> entity)
    {
        entity.ToTable("prices_daily", table =>
        {
            table.HasCheckConstraint("ck_prices_daily_prices", "open > 0 AND high > 0 AND low > 0 AND close > 0");
            table.HasCheckConstraint("ck_prices_daily_range", "low <= high");
            table.HasCheckConstraint("ck_prices_daily_volume", "volume >= 0");
        });
        entity.HasKey(price => new { price.AssetId, price.Date });
        entity.HasIndex(price => price.Date); // The admin page's newest day, without scanning the table.
        foreach (var column in new[] { nameof(PriceDaily.Open), nameof(PriceDaily.High), nameof(PriceDaily.Low), nameof(PriceDaily.Close) })
            entity.Property(column).HasColumnType(Money.Price);
        entity.HasOne<Asset>().WithMany()
            .HasForeignKey(price => price.AssetId).OnDelete(DeleteBehavior.Cascade);
    }
}

class FxRateConfiguration : IEntityTypeConfiguration<FxRate>
{
    public void Configure(EntityTypeBuilder<FxRate> entity)
    {
        entity.ToTable("fx_rates", table => table.HasCheckConstraint("ck_fx_rates_rate", "rate > 0"));
        entity.HasKey(rate => new { rate.Date, rate.Base, rate.Quote });
        entity.Property(rate => rate.Base).HasColumnType(Money.Currency);
        entity.Property(rate => rate.Quote).HasColumnType(Money.Currency);
        entity.Property(rate => rate.Rate).HasColumnType(Money.FxRate);
    }
}

class DailyEvaluationConfiguration : IEntityTypeConfiguration<DailyEvaluation>
{
    public void Configure(EntityTypeBuilder<DailyEvaluation> entity)
    {
        entity.ToTable("daily_evaluations", table => table.HasCheckConstraint("ck_daily_evaluations_step_no", "step_no IS NULL OR step_no >= 1"));
        entity.HasKey(row => new { row.StrategyId, row.AssetId, row.Date });
        entity.Property(row => row.Facts).HasColumnType("jsonb");
        entity.HasOne<Strategy>().WithMany()
            .HasForeignKey(row => row.StrategyId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<Asset>().WithMany()
            .HasForeignKey(row => row.AssetId).OnDelete(DeleteBehavior.NoAction);
    }
}

class FeedbackItemConfiguration : IEntityTypeConfiguration<FeedbackItem>
{
    public void Configure(EntityTypeBuilder<FeedbackItem> entity)
    {
        entity.ToTable("feedback_items", table =>
            table.HasCheckConstraint("ck_feedback_items_closed_at", "(status = 'closed') = (closed_at IS NOT NULL)"));
        entity.Property(item => item.Status).HasColumnDefault(FeedbackStatus.Open);
        entity.HasIndex(item => new { item.Status, item.CreatedAt });
        entity.HasIndex(item => item.UserId);
        entity.HasOne<User>().WithMany()
            .HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

class AiRoundConfiguration : IEntityTypeConfiguration<AiRound>
{
    public void Configure(EntityTypeBuilder<AiRound> entity)
    {
        entity.ToTable("ai_rounds", table =>
        {
            table.HasCheckConstraint("ck_ai_rounds_done", "(status = 'done') = (done_at IS NOT NULL)");
            table.HasCheckConstraint("ck_ai_rounds_done_answer",
                "status <> 'done' OR (answer IS NOT NULL AND scores IS NOT NULL AND ai_model IS NOT NULL AND session_date IS NOT NULL)");
        });
        entity.Property(round => round.Scores).HasColumnType("jsonb");
        entity.Property(round => round.PromptEdited).HasColumnDefault(false);
        entity.Property(round => round.Extra).HasColumnDefault(false);
        // One scheduled round per due date, and one extra round a day.
        entity.HasIndex(round => new { round.StrategyId, round.DueOn, round.Extra }).IsUnique();
        // One round open at a time per strategy, also when two requests race.
        entity.HasIndex(round => round.StrategyId).IsUnique()
            .HasDatabaseName("ux_ai_rounds_strategy_id_due")
            .HasFilter("status = 'due'");
        entity.HasOne<Strategy>().WithMany()
            .HasForeignKey(round => round.StrategyId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<SharedAiRound>().WithMany()
            .HasForeignKey(round => round.SharedRoundId).OnDelete(DeleteBehavior.SetNull);
    }
}

class SharedAiRoundConfiguration : IEntityTypeConfiguration<SharedAiRound>
{
    public void Configure(EntityTypeBuilder<SharedAiRound> entity)
    {
        entity.ToTable("shared_ai_rounds");
        entity.Property(round => round.Sources).HasColumnType("jsonb");
        entity.Property(round => round.Stocks).HasColumnType("jsonb");
        entity.Property(round => round.Scores).HasColumnType("jsonb");
        entity.HasIndex(round => round.RoundOn);
    }
}
