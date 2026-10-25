using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrategyTradingLab.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAiRounds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_kind",
                table: "notifications");

            migrationBuilder.AddColumn<bool>(
                name: "ai_sharing",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ai_sharing_changed_at",
                table: "users",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ai_rounds",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    strategy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    due_on = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    done_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    session_date = table.Column<DateOnly>(type: "date", nullable: true),
                    prompt_version = table.Column<int>(type: "integer", nullable: true),
                    prompt_edited = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    prompt_text = table.Column<string>(type: "text", nullable: true),
                    ai_model = table.Column<string>(type: "text", nullable: true),
                    answer = table.Column<string>(type: "text", nullable: true),
                    scores = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_rounds", x => x.id);
                    table.CheckConstraint("ck_ai_rounds_done", "(status = 'done') = (done_at IS NOT NULL)");
                    table.CheckConstraint("ck_ai_rounds_done_answer", "status <> 'done' OR (answer IS NOT NULL AND scores IS NOT NULL AND ai_model IS NOT NULL AND session_date IS NOT NULL)");
                    table.CheckConstraint("ck_ai_rounds_status", "status IN ('due', 'done', 'skipped')");
                    table.ForeignKey(
                        name: "fk_ai_rounds_strategies_strategy_id",
                        column: x => x.strategy_id,
                        principalTable: "strategies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shared_ai_rounds",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    round_on = table.Column<DateOnly>(type: "date", nullable: false),
                    ai_model = table.Column<string>(type: "text", nullable: false),
                    prompt_version = table.Column<int>(type: "integer", nullable: false),
                    prompt_edited = table.Column<bool>(type: "boolean", nullable: false),
                    sources = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    stocks = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    answer = table.Column<string>(type: "text", nullable: false),
                    scores = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shared_ai_rounds", x => x.id);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_kind",
                table: "notifications",
                sql: "kind IN ('signal', 'snoozed', 'overview', 'ai_round')");

            migrationBuilder.CreateIndex(
                name: "ix_ai_rounds_strategy_id_due_on",
                table: "ai_rounds",
                columns: new[] { "strategy_id", "due_on" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_ai_rounds_strategy_id_due",
                table: "ai_rounds",
                column: "strategy_id",
                unique: true,
                filter: "status = 'due'");

            migrationBuilder.CreateIndex(
                name: "ix_shared_ai_rounds_round_on",
                table: "shared_ai_rounds",
                column: "round_on");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_rounds");

            migrationBuilder.DropTable(
                name: "shared_ai_rounds");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_kind",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "ai_sharing",
                table: "users");

            migrationBuilder.DropColumn(
                name: "ai_sharing_changed_at",
                table: "users");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_kind",
                table: "notifications",
                sql: "kind IN ('signal', 'snoozed', 'overview')");
        }
    }
}
