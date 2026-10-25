using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrategyTradingLab.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSellSignalEvaluation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_daily_evaluations_status",
                table: "daily_evaluations");

            migrationBuilder.AddCheckConstraint(
                name: "ck_daily_evaluations_status",
                table: "daily_evaluations",
                sql: "status IN ('not_enough_data', 'not_candidate', 'watching', 'buy_signal', 'signal_ended', 'building', 'holding', 'sell_signal')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_daily_evaluations_status",
                table: "daily_evaluations");

            migrationBuilder.AddCheckConstraint(
                name: "ck_daily_evaluations_status",
                table: "daily_evaluations",
                sql: "status IN ('not_enough_data', 'not_candidate', 'watching', 'buy_signal', 'signal_ended', 'building', 'holding')");
        }
    }
}
