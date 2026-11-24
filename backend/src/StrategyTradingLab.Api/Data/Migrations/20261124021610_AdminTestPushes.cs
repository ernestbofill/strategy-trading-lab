using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrategyTradingLab.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdminTestPushes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_notifications_user_id_kind_channel_for_date_signal_id_strat",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_kind",
                table: "notifications");

            migrationBuilder.AddColumn<string>(
                name: "body",
                table: "notifications",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "title",
                table: "notifications",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_channel_created_at",
                table: "notifications",
                columns: new[] { "channel", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_user_id_kind_channel_for_date_signal_id_strat",
                table: "notifications",
                columns: new[] { "user_id", "kind", "channel", "for_date", "signal_id", "strategy_id", "asset_id" },
                unique: true,
                filter: "kind <> 'test'")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_kind",
                table: "notifications",
                sql: "kind IN ('signal', 'snoozed', 'overview', 'ai_round', 'signal_ended', 'stock_candidate', 'stock_close', 'test')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Test pushes fit neither the old kinds nor the old unique index.
            migrationBuilder.Sql("DELETE FROM notifications WHERE kind = 'test';");

            migrationBuilder.DropIndex(
                name: "ix_notifications_channel_created_at",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "ix_notifications_user_id_kind_channel_for_date_signal_id_strat",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_kind",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "body",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "title",
                table: "notifications");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_user_id_kind_channel_for_date_signal_id_strat",
                table: "notifications",
                columns: new[] { "user_id", "kind", "channel", "for_date", "signal_id", "strategy_id", "asset_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_kind",
                table: "notifications",
                sql: "kind IN ('signal', 'snoozed', 'overview', 'ai_round', 'signal_ended', 'stock_candidate', 'stock_close')");
        }
    }
}
