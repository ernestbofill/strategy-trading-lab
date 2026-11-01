using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrategyTradingLab.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailSuppressions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "email_suppressions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "citext", maxLength: 320, nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_suppressions", x => x.id);
                    table.CheckConstraint("ck_email_suppressions_reason", "reason IN ('bounced', 'invalid', 'complaint')");
                });

            migrationBuilder.CreateIndex(
                name: "ix_email_suppressions_email",
                table: "email_suppressions",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_suppressions");
        }
    }
}
