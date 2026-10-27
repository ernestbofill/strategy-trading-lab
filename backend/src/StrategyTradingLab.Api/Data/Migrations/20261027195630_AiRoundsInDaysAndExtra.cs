using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrategyTradingLab.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AiRoundsInDaysAndExtra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_ai_rounds_strategy_id_due_on",
                table: "ai_rounds");

            migrationBuilder.AddColumn<bool>(
                name: "extra",
                table: "ai_rounds",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_ai_rounds_strategy_id_due_on_extra",
                table: "ai_rounds",
                columns: new[] { "strategy_id", "due_on", "extra" },
                unique: true);

            // The AI template's round frequency moves from weeks to days: every 2 weeks becomes every 14 days.
            migrationBuilder.Sql("""
                UPDATE strategies
                SET params = (params - 'everyWeeks') || jsonb_build_object('everyDays', (params->>'everyWeeks')::int * 7)
                WHERE template_key = 'ai' AND params ? 'everyWeeks';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Back to whole weeks (rounded up, 1 to 8); extra rounds have no place in the old schedule.
            migrationBuilder.Sql("""
                UPDATE strategies
                SET params = (params - 'everyDays') || jsonb_build_object('everyWeeks', LEAST(8, GREATEST(1, CEIL((params->>'everyDays')::int / 7.0)::int)))
                WHERE template_key = 'ai' AND params ? 'everyDays';
                DELETE FROM ai_rounds WHERE extra;
                """);

            migrationBuilder.DropIndex(
                name: "ix_ai_rounds_strategy_id_due_on_extra",
                table: "ai_rounds");

            migrationBuilder.DropColumn(
                name: "extra",
                table: "ai_rounds");

            migrationBuilder.CreateIndex(
                name: "ix_ai_rounds_strategy_id_due_on",
                table: "ai_rounds",
                columns: new[] { "strategy_id", "due_on" },
                unique: true);
        }
    }
}
