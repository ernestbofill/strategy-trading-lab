using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrategyTradingLab.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DipLowBeforePeakFrom : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Winners at a discount takes its low up to the peak's day from now on (DipParams.LowBeforePeakFrom). Strategies
            // already running get tomorrow as that day: the daily replay rebuilds the steps they hold from their past days,
            // which must read as they did when their signals were given. New strategies have the rule from their first day.
            migrationBuilder.Sql("""
                UPDATE strategies
                SET params = params || jsonb_build_object('lowBeforePeakFrom', to_char(CURRENT_DATE + 1, 'YYYY-MM-DD'))
                WHERE template_key = 'dip' AND NOT params ? 'lowBeforePeakFrom';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE strategies SET params = params - 'lowBeforePeakFrom' WHERE template_key = 'dip';
                """);
        }
    }
}
