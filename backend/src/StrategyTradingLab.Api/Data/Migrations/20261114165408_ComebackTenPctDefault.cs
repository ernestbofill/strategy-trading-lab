using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrategyTradingLab.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ComebackTenPctDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE template_versions
                SET defaults = '{"drawdownPct":30,"candidateDays":60,"aboveSma50Pct":10,"lowDays":50,"volumeRatio":1.2,"maxChasePct":8,"nearHighPct":15,"breakoutPullbackPct":8,"cooldownDays":5}'::jsonb
                WHERE template_key = 'comeback' AND version = 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE template_versions
                SET defaults = '{"drawdownPct":30,"candidateDays":60,"aboveSma50Pct":15,"volumeRatio":1.2,"maxChasePct":8,"nearHighPct":15,"breakoutPullbackPct":8,"cooldownDays":5}'::jsonb
                WHERE template_key = 'comeback' AND version = 1;
                """);
        }
    }
}
