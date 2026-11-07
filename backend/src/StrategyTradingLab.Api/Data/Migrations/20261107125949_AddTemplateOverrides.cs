using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrategyTradingLab.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTemplateOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "template_overrides",
                columns: table => new
                {
                    template_key = table.Column<string>(type: "text", nullable: false),
                    defaults = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    asset_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: true),
                    close_pct = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_template_overrides", x => x.template_key);
                    table.CheckConstraint("ck_template_overrides_template_key", "template_key IN ('dip', 'comeback', 'bear', 'trend', 'ai')");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "template_overrides");
        }
    }
}
