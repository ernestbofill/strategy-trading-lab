using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrategyTradingLab.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ComebackVolumeOnRiseFrom : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The comeback's volume counts only on a rising price from now on (ComebackParams.VolumeOnRiseFrom). Strategies
            // already running get tomorrow as that day: the daily replay rebuilds the steps they hold from their past days,
            // which must read as they did when their signals were given. New strategies have the rule from their first day.
            migrationBuilder.Sql("""
                UPDATE strategies
                SET params = params || jsonb_build_object('volumeOnRiseFrom', to_char(CURRENT_DATE + 1, 'YYYY-MM-DD'))
                WHERE template_key = 'comeback' AND NOT params ? 'volumeOnRiseFrom';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE strategies SET params = params - 'volumeOnRiseFrom' WHERE template_key = 'comeback';
                """);
        }
    }
}
