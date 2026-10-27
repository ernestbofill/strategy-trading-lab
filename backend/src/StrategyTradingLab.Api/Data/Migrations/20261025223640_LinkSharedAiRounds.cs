using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrategyTradingLab.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class LinkSharedAiRounds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "shared_round_id",
                table: "ai_rounds",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_ai_rounds_shared_round_id",
                table: "ai_rounds",
                column: "shared_round_id");

            migrationBuilder.AddForeignKey(
                name: "fk_ai_rounds_shared_ai_rounds_shared_round_id",
                table: "ai_rounds",
                column: "shared_round_id",
                principalTable: "shared_ai_rounds",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_ai_rounds_shared_ai_rounds_shared_round_id",
                table: "ai_rounds");

            migrationBuilder.DropIndex(
                name: "ix_ai_rounds_shared_round_id",
                table: "ai_rounds");

            migrationBuilder.DropColumn(
                name: "shared_round_id",
                table: "ai_rounds");
        }
    }
}
