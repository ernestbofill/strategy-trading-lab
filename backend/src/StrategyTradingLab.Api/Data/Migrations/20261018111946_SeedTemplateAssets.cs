using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace StrategyTradingLab.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedTemplateAssets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "assets",
                columns: new[] { "id", "active", "created_at", "currency", "exchange", "kind", "name", "symbol" },
                values: new object[,]
                {
                    { new Guid("019a0000-0000-7000-8001-000000000001"), true, new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "USD", "XNYS", "stock", "Eaton", "ETN" },
                    { new Guid("019a0000-0000-7000-8001-000000000002"), true, new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "USD", "XNYS", "stock", "Modine", "MOD" },
                    { new Guid("019a0000-0000-7000-8001-000000000003"), true, new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "USD", "XNAS", "stock", "Monolithic Power Systems", "MPWR" },
                    { new Guid("019a0000-0000-7000-8001-000000000004"), true, new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "USD", "XNYS", "stock", "nVent Electric", "NVT" },
                    { new Guid("019a0000-0000-7000-8001-000000000005"), true, new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "USD", "XNAS", "stock", "Super Micro Computer", "SMCI" },
                    { new Guid("019a0000-0000-7000-8001-000000000006"), true, new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "USD", "XNYS", "stock", "Vertiv", "VRT" },
                    { new Guid("019a0000-0000-7000-8001-000000000007"), true, new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "USD", "XNAS", "stock", "Vicor", "VICR" },
                    { new Guid("019a0000-0000-7000-8001-000000000008"), true, new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "USD", "XNAS", "stock", "Moderna", "MRNA" },
                    { new Guid("019a0000-0000-7000-8001-000000000009"), true, new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "USD", "XNAS", "stock", "Palantir Technologies", "PLTR" },
                    { new Guid("019a0000-0000-7000-8001-000000000010"), true, new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "USD", "XNYS", "stock", "Salesforce", "CRM" },
                    { new Guid("019a0000-0000-7000-8001-000000000011"), true, new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "USD", "XNYS", "stock", "ServiceNow", "NOW" },
                    { new Guid("019a0000-0000-7000-8001-000000000012"), true, new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "USD", "XNYS", "stock", "Snowflake", "SNOW" },
                    { new Guid("019a0000-0000-7000-8001-000000000013"), true, new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "USD", "XNAS", "stock", "Tesla", "TSLA" },
                    { new Guid("019a0000-0000-7000-8001-000000000014"), true, new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "USD", "XNYS", "stock", "Zeta Global", "ZETA" },
                    { new Guid("019a0000-0000-7000-8001-000000000015"), true, new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "USD", "XNAS", "stock", "Zscaler", "ZS" },
                    { new Guid("019a0000-0000-7000-8001-000000001001"), true, new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "USD", "ARCX", "fund", "SPDR S&P 500 ETF", "SPY" }
                });

            migrationBuilder.InsertData(
                table: "asset_texts",
                columns: new[] { "asset_id", "locale", "created_at", "what" },
                values: new object[,]
                {
                    { new Guid("019a0000-0000-7000-8001-000000000001"), "en", new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Electrical power equipment" },
                    { new Guid("019a0000-0000-7000-8001-000000000002"), "en", new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Cooling for data centres and vehicles" },
                    { new Guid("019a0000-0000-7000-8001-000000000003"), "en", new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Power chips for computers and cars" },
                    { new Guid("019a0000-0000-7000-8001-000000000004"), "en", new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Electrical enclosures and cooling" },
                    { new Guid("019a0000-0000-7000-8001-000000000005"), "en", new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Servers for AI and data centres" },
                    { new Guid("019a0000-0000-7000-8001-000000000006"), "en", new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Power and cooling for data centres" },
                    { new Guid("019a0000-0000-7000-8001-000000000007"), "en", new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Power modules for servers and electronics" },
                    { new Guid("019a0000-0000-7000-8001-000000000008"), "en", new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "mRNA vaccines and medicines" },
                    { new Guid("019a0000-0000-7000-8001-000000000009"), "en", new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Data analysis software" },
                    { new Guid("019a0000-0000-7000-8001-000000000010"), "en", new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Customer management software" },
                    { new Guid("019a0000-0000-7000-8001-000000000011"), "en", new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Workflow software for companies" },
                    { new Guid("019a0000-0000-7000-8001-000000000012"), "en", new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Cloud data platform" },
                    { new Guid("019a0000-0000-7000-8001-000000000013"), "en", new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Electric cars and batteries" },
                    { new Guid("019a0000-0000-7000-8001-000000000014"), "en", new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Marketing software and data" },
                    { new Guid("019a0000-0000-7000-8001-000000000015"), "en", new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Cloud security software" },
                    { new Guid("019a0000-0000-7000-8001-000000001001"), "en", new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Tracks the 500 largest US companies" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "asset_texts",
                keyColumns: new[] { "asset_id", "locale" },
                keyValues: new object[] { new Guid("019a0000-0000-7000-8001-000000000001"), "en" });

            migrationBuilder.DeleteData(
                table: "asset_texts",
                keyColumns: new[] { "asset_id", "locale" },
                keyValues: new object[] { new Guid("019a0000-0000-7000-8001-000000000002"), "en" });

            migrationBuilder.DeleteData(
                table: "asset_texts",
                keyColumns: new[] { "asset_id", "locale" },
                keyValues: new object[] { new Guid("019a0000-0000-7000-8001-000000000003"), "en" });

            migrationBuilder.DeleteData(
                table: "asset_texts",
                keyColumns: new[] { "asset_id", "locale" },
                keyValues: new object[] { new Guid("019a0000-0000-7000-8001-000000000004"), "en" });

            migrationBuilder.DeleteData(
                table: "asset_texts",
                keyColumns: new[] { "asset_id", "locale" },
                keyValues: new object[] { new Guid("019a0000-0000-7000-8001-000000000005"), "en" });

            migrationBuilder.DeleteData(
                table: "asset_texts",
                keyColumns: new[] { "asset_id", "locale" },
                keyValues: new object[] { new Guid("019a0000-0000-7000-8001-000000000006"), "en" });

            migrationBuilder.DeleteData(
                table: "asset_texts",
                keyColumns: new[] { "asset_id", "locale" },
                keyValues: new object[] { new Guid("019a0000-0000-7000-8001-000000000007"), "en" });

            migrationBuilder.DeleteData(
                table: "asset_texts",
                keyColumns: new[] { "asset_id", "locale" },
                keyValues: new object[] { new Guid("019a0000-0000-7000-8001-000000000008"), "en" });

            migrationBuilder.DeleteData(
                table: "asset_texts",
                keyColumns: new[] { "asset_id", "locale" },
                keyValues: new object[] { new Guid("019a0000-0000-7000-8001-000000000009"), "en" });

            migrationBuilder.DeleteData(
                table: "asset_texts",
                keyColumns: new[] { "asset_id", "locale" },
                keyValues: new object[] { new Guid("019a0000-0000-7000-8001-000000000010"), "en" });

            migrationBuilder.DeleteData(
                table: "asset_texts",
                keyColumns: new[] { "asset_id", "locale" },
                keyValues: new object[] { new Guid("019a0000-0000-7000-8001-000000000011"), "en" });

            migrationBuilder.DeleteData(
                table: "asset_texts",
                keyColumns: new[] { "asset_id", "locale" },
                keyValues: new object[] { new Guid("019a0000-0000-7000-8001-000000000012"), "en" });

            migrationBuilder.DeleteData(
                table: "asset_texts",
                keyColumns: new[] { "asset_id", "locale" },
                keyValues: new object[] { new Guid("019a0000-0000-7000-8001-000000000013"), "en" });

            migrationBuilder.DeleteData(
                table: "asset_texts",
                keyColumns: new[] { "asset_id", "locale" },
                keyValues: new object[] { new Guid("019a0000-0000-7000-8001-000000000014"), "en" });

            migrationBuilder.DeleteData(
                table: "asset_texts",
                keyColumns: new[] { "asset_id", "locale" },
                keyValues: new object[] { new Guid("019a0000-0000-7000-8001-000000000015"), "en" });

            migrationBuilder.DeleteData(
                table: "asset_texts",
                keyColumns: new[] { "asset_id", "locale" },
                keyValues: new object[] { new Guid("019a0000-0000-7000-8001-000000001001"), "en" });

            migrationBuilder.DeleteData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000001"));

            migrationBuilder.DeleteData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000002"));

            migrationBuilder.DeleteData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000003"));

            migrationBuilder.DeleteData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000004"));

            migrationBuilder.DeleteData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000005"));

            migrationBuilder.DeleteData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000006"));

            migrationBuilder.DeleteData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000007"));

            migrationBuilder.DeleteData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000008"));

            migrationBuilder.DeleteData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000009"));

            migrationBuilder.DeleteData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000010"));

            migrationBuilder.DeleteData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000011"));

            migrationBuilder.DeleteData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000012"));

            migrationBuilder.DeleteData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000013"));

            migrationBuilder.DeleteData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000014"));

            migrationBuilder.DeleteData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000015"));

            migrationBuilder.DeleteData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000001001"));
        }
    }
}
