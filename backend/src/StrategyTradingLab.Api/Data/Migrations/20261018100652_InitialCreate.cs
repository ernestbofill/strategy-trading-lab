using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrategyTradingLab.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:citext", ",,");

            migrationBuilder.CreateTable(
                name: "assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    symbol = table.Column<string>(type: "text", nullable: false),
                    exchange = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    currency = table.Column<string>(type: "char(3)", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assets", x => x.id);
                    table.CheckConstraint("ck_assets_kind", "kind IN ('stock', 'fund')");
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "citext", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    email_verified_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    time_zone = table.Column<string>(type: "text", nullable: false),
                    locale = table.Column<string>(type: "text", nullable: false),
                    currency = table.Column<string>(type: "char(3)", nullable: false, defaultValue: "EUR"),
                    overview_email = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    disclaimer_accepted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "asset_texts",
                columns: table => new
                {
                    asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    locale = table.Column<string>(type: "text", nullable: false),
                    what = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asset_texts", x => new { x.asset_id, x.locale });
                    table.ForeignKey(
                        name: "fk_asset_texts_assets_asset_id",
                        column: x => x.asset_id,
                        principalTable: "assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "push_subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    endpoint = table.Column<string>(type: "text", nullable: false),
                    p256dh = table.Column<string>(type: "text", nullable: false),
                    auth = table.Column<string>(type: "text", nullable: false),
                    last_failed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_push_subscriptions", x => x.id);
                    table.ForeignKey(
                        name: "fk_push_subscriptions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "strategies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_key = table.Column<string>(type: "text", nullable: false),
                    defaults_version = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    mode = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    started_on = table.Column<DateOnly>(type: "date", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    notifications = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    currency = table.Column<string>(type: "char(3)", nullable: false),
                    @params = table.Column<JsonDocument>(name: "params", type: "jsonb", nullable: false),
                    source_strategy_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    budget_amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    budget_max_steps = table.Column<int>(type: "integer", nullable: true),
                    budget_step = table.Column<decimal>(type: "numeric(14,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_strategies", x => x.id);
                    table.CheckConstraint("ck_strategies_budget_step", "budget_step <= budget_amount");
                    table.CheckConstraint("ck_strategies_budget_steps", "(budget_step IS NULL) = (budget_max_steps IS NULL)");
                    table.CheckConstraint("ck_strategies_ended_at", "(status = 'ended') = (ended_at IS NOT NULL)");
                    table.CheckConstraint("ck_strategies_mode", "mode IN ('simulation', 'live')");
                    table.CheckConstraint("ck_strategies_status", "status IN ('running', 'ended')");
                    table.CheckConstraint("ck_strategies_template_key", "template_key IN ('dip', 'comeback', 'drop', 'trend', 'ai')");
                    table.ForeignKey(
                        name: "fk_strategies_strategies_source_strategy_id",
                        column: x => x.source_strategy_id,
                        principalTable: "strategies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_strategies_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "signals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    strategy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    side = table.Column<string>(type: "text", nullable: false),
                    step_no = table.Column<int>(type: "integer", nullable: true),
                    max_steps = table.Column<int>(type: "integer", nullable: true),
                    step_amount = table.Column<decimal>(type: "numeric(14,2)", nullable: true),
                    signal_date = table.Column<DateOnly>(type: "date", nullable: false),
                    close_price = table.Column<decimal>(type: "numeric(14,4)", nullable: false),
                    facts = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    skip_reason = table.Column<string>(type: "text", nullable: true),
                    answered_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    snoozed_until = table.Column<DateOnly>(type: "date", nullable: true),
                    ended_on = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_signals", x => x.id);
                    table.CheckConstraint("ck_signals_answered_at", "(status IN ('confirmed', 'skipped')) = (answered_at IS NOT NULL)");
                    table.CheckConstraint("ck_signals_close_price", "close_price > 0");
                    table.CheckConstraint("ck_signals_missed_ended_on", "status <> 'missed' OR ended_on IS NOT NULL");
                    table.CheckConstraint("ck_signals_sell_no_step", "side = 'buy' OR (step_no IS NULL AND max_steps IS NULL AND step_amount IS NULL)");
                    table.CheckConstraint("ck_signals_side", "side IN ('buy', 'sell')");
                    table.CheckConstraint("ck_signals_skip_reason", "skip_reason IN ('price_ran_away', 'didnt_trust_it', 'no_cash', 'other')");
                    table.CheckConstraint("ck_signals_skip_reason_skipped", "skip_reason IS NULL OR status = 'skipped'");
                    table.CheckConstraint("ck_signals_snoozed_until", "snoozed_until IS NULL OR status = 'waiting'");
                    table.CheckConstraint("ck_signals_status", "status IN ('waiting', 'confirmed', 'skipped', 'missed')");
                    table.CheckConstraint("ck_signals_step_no", "step_no IS NULL OR step_no BETWEEN 1 AND max_steps");
                    table.CheckConstraint("ck_signals_step_together", "(step_no IS NULL) = (max_steps IS NULL) AND (step_no IS NULL) = (step_amount IS NULL)");
                    table.ForeignKey(
                        name: "fk_signals_assets_asset_id",
                        column: x => x.asset_id,
                        principalTable: "assets",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_signals_strategies_strategy_id",
                        column: x => x.strategy_id,
                        principalTable: "strategies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "strategy_assets",
                columns: table => new
                {
                    strategy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    added_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    removed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_strategy_assets", x => new { x.strategy_id, x.asset_id });
                    table.CheckConstraint("ck_strategy_assets_removed", "removed_at IS NULL OR NOT active");
                    table.ForeignKey(
                        name: "fk_strategy_assets_assets_asset_id",
                        column: x => x.asset_id,
                        principalTable: "assets",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_strategy_assets_strategies_strategy_id",
                        column: x => x.strategy_id,
                        principalTable: "strategies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    signal_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<string>(type: "text", nullable: false),
                    channel = table.Column<string>(type: "text", nullable: false),
                    for_date = table.Column<DateOnly>(type: "date", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    failed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    error = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                    table.CheckConstraint("ck_notifications_channel", "channel IN ('push', 'email')");
                    table.CheckConstraint("ck_notifications_kind", "kind IN ('signal', 'snoozed')");
                    table.CheckConstraint("ck_notifications_signal_id", "(kind IN ('signal', 'snoozed')) = (signal_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_notifications_signals_signal_id",
                        column: x => x.signal_id,
                        principalTable: "signals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_notifications_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "signal_trades",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    signal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    strategy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trade_date = table.Column<DateOnly>(type: "date", nullable: false),
                    side = table.Column<string>(type: "text", nullable: false),
                    step_no = table.Column<int>(type: "integer", nullable: true),
                    price = table.Column<decimal>(type: "numeric(14,4)", nullable: false),
                    shares = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    fee = table.Column<decimal>(type: "numeric(14,2)", nullable: false, defaultValue: 0m),
                    fx_rate = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_signal_trades", x => x.id);
                    table.CheckConstraint("ck_signal_trades_amount", "amount > 0");
                    table.CheckConstraint("ck_signal_trades_fee", "fee >= 0");
                    table.CheckConstraint("ck_signal_trades_fx_rate", "fx_rate > 0");
                    table.CheckConstraint("ck_signal_trades_price", "price > 0");
                    table.CheckConstraint("ck_signal_trades_shares", "shares > 0");
                    table.CheckConstraint("ck_signal_trades_side", "side IN ('buy', 'sell')");
                    table.CheckConstraint("ck_signal_trades_step_no", "step_no IS NULL OR step_no >= 1");
                    table.ForeignKey(
                        name: "fk_signal_trades_assets_asset_id",
                        column: x => x.asset_id,
                        principalTable: "assets",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_signal_trades_signals_signal_id",
                        column: x => x.signal_id,
                        principalTable: "signals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_signal_trades_strategies_strategy_id",
                        column: x => x.strategy_id,
                        principalTable: "strategies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_trades",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    strategy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trade_date = table.Column<DateOnly>(type: "date", nullable: false),
                    side = table.Column<string>(type: "text", nullable: false),
                    price = table.Column<decimal>(type: "numeric(14,4)", nullable: false),
                    entered_as = table.Column<string>(type: "text", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    shares = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    fee = table.Column<decimal>(type: "numeric(14,2)", nullable: false, defaultValue: 0m),
                    fx_rate = table.Column<decimal>(type: "numeric(18,8)", nullable: false),
                    fx_source = table.Column<string>(type: "text", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    signal_id = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_trades", x => x.id);
                    table.CheckConstraint("ck_user_trades_amount", "amount > 0");
                    table.CheckConstraint("ck_user_trades_entered_as", "entered_as IN ('amount', 'shares')");
                    table.CheckConstraint("ck_user_trades_fee", "fee >= 0");
                    table.CheckConstraint("ck_user_trades_fx_rate", "fx_rate > 0");
                    table.CheckConstraint("ck_user_trades_fx_source", "fx_source IN ('reference', 'user')");
                    table.CheckConstraint("ck_user_trades_price", "price > 0");
                    table.CheckConstraint("ck_user_trades_shares", "shares > 0");
                    table.CheckConstraint("ck_user_trades_side", "side IN ('buy', 'sell')");
                    table.CheckConstraint("ck_user_trades_source", "source IN ('signal', 'manual')");
                    table.CheckConstraint("ck_user_trades_source_signal", "(source = 'signal') = (signal_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_user_trades_assets_asset_id",
                        column: x => x.asset_id,
                        principalTable: "assets",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_user_trades_signals_signal_id",
                        column: x => x.signal_id,
                        principalTable: "signals",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_user_trades_strategies_strategy_id",
                        column: x => x.strategy_id,
                        principalTable: "strategies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "users",
                columns: new[] { "id", "created_at", "currency", "disclaimer_accepted_at", "email", "email_verified_at", "locale", "overview_email", "password_hash", "time_zone", "updated_at" },
                values: new object[] { new Guid("019a0000-0000-7000-8000-000000000001"), new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "EUR", new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "dev@strategytradinglab.local", null, "en", true, null, "Europe/Madrid", new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.CreateIndex(
                name: "ix_assets_symbol_exchange",
                table: "assets",
                columns: new[] { "symbol", "exchange" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_signal_id",
                table: "notifications",
                column: "signal_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_user_id_kind_channel_for_date_signal_id",
                table: "notifications",
                columns: new[] { "user_id", "kind", "channel", "for_date", "signal_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_push_subscriptions_endpoint",
                table: "push_subscriptions",
                column: "endpoint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_push_subscriptions_user_id",
                table: "push_subscriptions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_signal_trades_asset_id",
                table: "signal_trades",
                column: "asset_id");

            migrationBuilder.CreateIndex(
                name: "ix_signal_trades_signal_id",
                table: "signal_trades",
                column: "signal_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_signal_trades_strategy_id_asset_id_trade_date",
                table: "signal_trades",
                columns: new[] { "strategy_id", "asset_id", "trade_date" });

            migrationBuilder.CreateIndex(
                name: "ix_signals_asset_id",
                table: "signals",
                column: "asset_id");

            migrationBuilder.CreateIndex(
                name: "ix_signals_strategy_id_asset_id_signal_date_side",
                table: "signals",
                columns: new[] { "strategy_id", "asset_id", "signal_date", "side" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_signals_strategy_id_status",
                table: "signals",
                columns: new[] { "strategy_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_signals_strategy_id_waiting",
                table: "signals",
                column: "strategy_id",
                filter: "status = 'waiting'");

            migrationBuilder.CreateIndex(
                name: "ix_strategies_source_strategy_id",
                table: "strategies",
                column: "source_strategy_id");

            migrationBuilder.CreateIndex(
                name: "ix_strategies_user_id",
                table: "strategies",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_strategy_assets_asset_id",
                table: "strategy_assets",
                column: "asset_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_trades_asset_id",
                table: "user_trades",
                column: "asset_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_trades_signal_id",
                table: "user_trades",
                column: "signal_id",
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_user_trades_strategy_id_asset_id_trade_date",
                table: "user_trades",
                columns: new[] { "strategy_id", "asset_id", "trade_date" });

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true);

            // A running strategy's name is unique per user, ignoring case. EF can't model an
            // expression index, so it isn't in the model snapshot.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX ux_strategies_user_id_running_name " +
                "ON strategies (user_id, lower(name)) WHERE status = 'running';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX ux_strategies_user_id_running_name;");

            migrationBuilder.DropTable(
                name: "asset_texts");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "push_subscriptions");

            migrationBuilder.DropTable(
                name: "signal_trades");

            migrationBuilder.DropTable(
                name: "strategy_assets");

            migrationBuilder.DropTable(
                name: "user_trades");

            migrationBuilder.DropTable(
                name: "signals");

            migrationBuilder.DropTable(
                name: "assets");

            migrationBuilder.DropTable(
                name: "strategies");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
