using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrategyTradingLab.Api.Data.Migrations
{
    /// <summary>
    /// Deletes Manual strategies' signal trades ("the rules alone"): since #282 a Manual strategy follows the user's trades, and
    /// nothing reads them (Ernest, 2026-11-24). Drops data; there is no way back.
    /// </summary>
    public partial class DropManualSignalTrades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM signal_trades WHERE strategy_id IN (SELECT id FROM strategies WHERE mode = 'live');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The deleted rows can't be brought back.
        }
    }
}
