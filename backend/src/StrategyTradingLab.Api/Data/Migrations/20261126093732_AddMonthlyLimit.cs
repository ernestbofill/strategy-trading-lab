using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrategyTradingLab.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMonthlyLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "budget_monthly_limit",
                table: "strategies",
                type: "numeric(14,2)",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_strategies_budget_monthly_limit",
                table: "strategies",
                sql: "budget_monthly_limit IS NULL OR (budget_step IS NOT NULL AND budget_monthly_limit >= budget_step AND budget_monthly_limit <= budget_amount)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_strategies_budget_monthly_limit",
                table: "strategies");

            migrationBuilder.DropColumn(
                name: "budget_monthly_limit",
                table: "strategies");
        }
    }
}
