using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrategyTradingLab.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ValidatedFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_feedback_items_closed_at",
                table: "feedback_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_feedback_items_status",
                table: "feedback_items");

            migrationBuilder.AddCheckConstraint(
                name: "ck_feedback_items_closed_at",
                table: "feedback_items",
                sql: "(status <> 'open') = (closed_at IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_feedback_items_status",
                table: "feedback_items",
                sql: "status IN ('open', 'closed', 'validated')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_feedback_items_closed_at",
                table: "feedback_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_feedback_items_status",
                table: "feedback_items");

            migrationBuilder.AddCheckConstraint(
                name: "ck_feedback_items_closed_at",
                table: "feedback_items",
                sql: "(status = 'closed') = (closed_at IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_feedback_items_status",
                table: "feedback_items",
                sql: "status IN ('open', 'closed')");
        }
    }
}
