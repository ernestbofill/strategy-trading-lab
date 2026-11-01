using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrategyTradingLab.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SimplerNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "notifications",
                table: "strategies");

            migrationBuilder.DropColumn(
                name: "push",
                table: "strategies");

            migrationBuilder.DropColumn(
                name: "stock_change_emails",
                table: "strategies");

            migrationBuilder.AddColumn<bool>(
                name: "alert_push",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "alert_push",
                table: "users");

            migrationBuilder.AddColumn<bool>(
                name: "notifications",
                table: "strategies",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "push",
                table: "strategies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "stock_change_emails",
                table: "strategies",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
