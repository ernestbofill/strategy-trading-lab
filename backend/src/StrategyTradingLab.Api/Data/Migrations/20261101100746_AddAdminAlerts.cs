using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrategyTradingLab.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "admin_alerts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "open"),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    detail = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    strategy_id = table.Column<Guid>(type: "uuid", nullable: true),
                    count = table.Column<int>(type: "integer", nullable: false),
                    first_seen_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_admin_alerts", x => x.id);
                    table.CheckConstraint("ck_admin_alerts_closed_at", "(status = 'open') = (closed_at IS NULL)");
                    table.CheckConstraint("ck_admin_alerts_count", "count >= 1");
                    table.CheckConstraint("ck_admin_alerts_source", "source IN ('daily_check', 'prices', 'strategies', 'ai_strategies', 'email')");
                    table.CheckConstraint("ck_admin_alerts_status", "status IN ('open', 'dismissed', 'cleared')");
                    table.ForeignKey(
                        name: "fk_admin_alerts_strategies_strategy_id",
                        column: x => x.strategy_id,
                        principalTable: "strategies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_admin_alerts_key",
                table: "admin_alerts",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_admin_alerts_status_last_seen_at",
                table: "admin_alerts",
                columns: new[] { "status", "last_seen_at" });

            migrationBuilder.CreateIndex(
                name: "ix_admin_alerts_strategy_id",
                table: "admin_alerts",
                column: "strategy_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admin_alerts");
        }
    }
}
