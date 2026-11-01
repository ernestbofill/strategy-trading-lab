using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrategyTradingLab.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailUnsubscribe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "alert_emails",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "unsubscribe_token",
                table: "users",
                type: "text",
                nullable: false,
                defaultValue: "");

            // Every existing user gets their own token before the index asks for them to differ.
            migrationBuilder.Sql("UPDATE users SET unsubscribe_token = replace(gen_random_uuid()::text, '-', '') || replace(gen_random_uuid()::text, '-', '');");

            // AddColumn left DEFAULT '' behind; the model has none, and a second '' would break the unique index.
            migrationBuilder.Sql("ALTER TABLE users ALTER COLUMN unsubscribe_token DROP DEFAULT;");

            migrationBuilder.CreateIndex(
                name: "ix_users_unsubscribe_token",
                table: "users",
                column: "unsubscribe_token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_users_unsubscribe_token",
                table: "users");

            migrationBuilder.DropColumn(
                name: "alert_emails",
                table: "users");

            migrationBuilder.DropColumn(
                name: "unsubscribe_token",
                table: "users");
        }
    }
}
