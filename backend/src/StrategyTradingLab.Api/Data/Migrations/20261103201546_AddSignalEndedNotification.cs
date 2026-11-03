using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrategyTradingLab.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSignalEndedNotification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_kind",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_signal_id",
                table: "notifications");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_kind",
                table: "notifications",
                sql: "kind IN ('signal', 'snoozed', 'overview', 'ai_round', 'signal_ended')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_signal_id",
                table: "notifications",
                sql: "(kind IN ('signal', 'snoozed', 'signal_ended')) = (signal_id IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_kind",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_signal_id",
                table: "notifications");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_kind",
                table: "notifications",
                sql: "kind IN ('signal', 'snoozed', 'overview', 'ai_round')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_signal_id",
                table: "notifications",
                sql: "(kind IN ('signal', 'snoozed')) = (signal_id IS NOT NULL)");
        }
    }
}
