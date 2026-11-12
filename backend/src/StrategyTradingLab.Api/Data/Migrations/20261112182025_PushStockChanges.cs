using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrategyTradingLab.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class PushStockChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_notifications_user_id_kind_channel_for_date_signal_id",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_kind",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "overview_push",
                table: "users");

            migrationBuilder.AddColumn<Guid>(
                name: "asset_id",
                table: "notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "strategy_id",
                table: "notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_asset_id",
                table: "notifications",
                column: "asset_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_strategy_id",
                table: "notifications",
                column: "strategy_id");

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

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_stock",
                table: "notifications",
                sql: "(kind IN ('stock_candidate', 'stock_close')) = (strategy_id IS NOT NULL AND asset_id IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "fk_notifications_assets_asset_id",
                table: "notifications",
                column: "asset_id",
                principalTable: "assets",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_notifications_strategies_strategy_id",
                table: "notifications",
                column: "strategy_id",
                principalTable: "strategies",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_notifications_assets_asset_id",
                table: "notifications");

            migrationBuilder.DropForeignKey(
                name: "fk_notifications_strategies_strategy_id",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "ix_notifications_asset_id",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "ix_notifications_strategy_id",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "ix_notifications_user_id_kind_channel_for_date_signal_id_strat",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_kind",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_stock",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "asset_id",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "strategy_id",
                table: "notifications");

            migrationBuilder.AddColumn<bool>(
                name: "overview_push",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_user_id_kind_channel_for_date_signal_id",
                table: "notifications",
                columns: new[] { "user_id", "kind", "channel", "for_date", "signal_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_kind",
                table: "notifications",
                sql: "kind IN ('signal', 'snoozed', 'overview', 'ai_round', 'signal_ended')");
        }
    }
}
