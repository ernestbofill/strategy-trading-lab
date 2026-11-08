using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrategyTradingLab.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTemplateVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "template_versions",
                columns: table => new
                {
                    template_key = table.Column<string>(type: "text", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    defaults = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    asset_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    close_pct = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_template_versions", x => new { x.template_key, x.version });
                    table.CheckConstraint("ck_template_versions_template_key", "template_key IN ('dip', 'comeback', 'bear', 'trend', 'ai')");
                    table.CheckConstraint("ck_template_versions_version", "version >= 1");
                });

            migrationBuilder.CreateIndex(
                name: "ux_template_versions_active",
                table: "template_versions",
                column: "template_key",
                unique: true,
                filter: "active");

            // Version 1 of each template: the catalogue's tested values on this day (TemplateCatalog, TemplateParams), written as
            // literals so a later change in code doesn't rewrite history. Every strategy so far started from it (defaults_version 1).
            // TemplateVersionTests checks they still match the catalogue. Asset ids are TemplateAssets' seeded ones.
            migrationBuilder.Sql("""
                INSERT INTO template_versions (template_key, version, defaults, asset_ids, close_pct, active) VALUES
                ('dip', 1,
                 '{"minGainPct":50,"gainDays":168,"trendDays":200,"peakDays":60,"zoneStartPct":5,"zoneEndPct":15,"floorDays":50,"cooldownDays":10}',
                 ARRAY['019a0000-0000-7000-8001-000000000016','019a0000-0000-7000-8001-000000000001','019a0000-0000-7000-8001-000000000002',
                       '019a0000-0000-7000-8001-000000000003','019a0000-0000-7000-8001-000000000004','019a0000-0000-7000-8001-000000000017',
                       '019a0000-0000-7000-8001-000000000005','019a0000-0000-7000-8001-000000000006','019a0000-0000-7000-8001-000000000007']::uuid[],
                 3, true),
                ('comeback', 1,
                 '{"drawdownPct":30,"candidateDays":60,"aboveSma50Pct":15,"volumeRatio":1.2,"maxChasePct":8,"nearHighPct":15,"breakoutPullbackPct":8,"cooldownDays":5}',
                 ARRAY['019a0000-0000-7000-8001-000000000018','019a0000-0000-7000-8001-000000000016','019a0000-0000-7000-8001-000000000001',
                       '019a0000-0000-7000-8001-000000000002','019a0000-0000-7000-8001-000000000008','019a0000-0000-7000-8001-000000000019',
                       '019a0000-0000-7000-8001-000000000003','019a0000-0000-7000-8001-000000000009','019a0000-0000-7000-8001-000000000010',
                       '019a0000-0000-7000-8001-000000000011','019a0000-0000-7000-8001-000000000012','019a0000-0000-7000-8001-000000000013',
                       '019a0000-0000-7000-8001-000000000006','019a0000-0000-7000-8001-000000000007','019a0000-0000-7000-8001-000000000014',
                       '019a0000-0000-7000-8001-000000000015']::uuid[],
                 3, true),
                ('bear', 1, '{"drawdownPct":20,"highDays":126,"reboundPct":5,"stepEveryDays":10}',
                 ARRAY['019a0000-0000-7000-8001-000000001002']::uuid[], 3, true),
                ('trend', 1, '{"smaDays":200,"bandPct":3,"cooldownDays":5}',
                 ARRAY['019a0000-0000-7000-8001-000000001002']::uuid[], 3, true),
                ('ai', 1, '{"everyDays":14,"topN":1}',
                 ARRAY['019a0000-0000-7000-8001-000000000016','019a0000-0000-7000-8001-000000000001','019a0000-0000-7000-8001-000000000002',
                       '019a0000-0000-7000-8001-000000000003','019a0000-0000-7000-8001-000000000004','019a0000-0000-7000-8001-000000000017',
                       '019a0000-0000-7000-8001-000000000005','019a0000-0000-7000-8001-000000000006','019a0000-0000-7000-8001-000000000007',
                       '019a0000-0000-7000-8001-000000000018','019a0000-0000-7000-8001-000000000008','019a0000-0000-7000-8001-000000000019',
                       '019a0000-0000-7000-8001-000000000009','019a0000-0000-7000-8001-000000000010','019a0000-0000-7000-8001-000000000011',
                       '019a0000-0000-7000-8001-000000000012','019a0000-0000-7000-8001-000000000013','019a0000-0000-7000-8001-000000000014',
                       '019a0000-0000-7000-8001-000000000015']::uuid[],
                 3, true);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "template_versions");
        }
    }
}
