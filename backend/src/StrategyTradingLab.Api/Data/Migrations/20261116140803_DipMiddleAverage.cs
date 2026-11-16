using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrategyTradingLab.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DipMiddleAverage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Winners at a discount stacks two averages by default from now on (DipParams.MiddleAverage = 0). Strategies already
            // running keep the three they started with, written out so their Settings screen shows it and a save keeps it.
            migrationBuilder.Sql("""
                UPDATE strategies
                SET params = params || jsonb_build_object('middleAverage', 1)
                WHERE template_key = 'dip' AND NOT params ? 'middleAverage';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE strategies SET params = params - 'middleAverage' WHERE template_key = 'dip';
                """);
        }
    }
}
