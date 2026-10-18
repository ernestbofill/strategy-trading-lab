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
            migrationBuilder.DropCheckConstraint(
                name: "ck_assets_kind",
                table: "assets");

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
                    { new Guid("019a0000-0000-7000-8001-000000000016"), true, new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "USD", "XNYS", "stock", "Coherent", "COHR" },
                    { new Guid("019a0000-0000-7000-8001-000000000017"), true, new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "EUR", "XPAR", "stock", "Schneider Electric", "SU.PA" },
                    { new Guid("019a0000-0000-7000-8001-000000000018"), true, new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "HKD", "XHKG", "stock", "BYD", "1211.HK" },
                    { new Guid("019a0000-0000-7000-8001-000000000019"), true, new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "GBP", "XLON", "stock", "Molten Ventures", "GROW.L" },
                    { new Guid("019a0000-0000-7000-8001-000000000020"), true, new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "DKK", "XCSE", "stock", "cBrain", "CBRAIN.CO" },
                    { new Guid("019a0000-0000-7000-8001-000000001001"), true, new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "USD", "ARCX", "fund", "SPDR S&P 500 ETF", "SPY" },
                    { new Guid("019a0000-0000-7000-8001-000000001002"), true, new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "USD", "XLON", "fund", "iShares Core S&P 500 UCITS ETF", "CSPX.L" },
                    { new Guid("019a0000-0000-7000-8001-000000001003"), true, new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "EUR", "XAMS", "fund", "iShares Core MSCI World UCITS ETF", "IWDA.AS" },
                    { new Guid("019a0000-0000-7000-8001-000000001004"), true, new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "USD", "XNAS", "fund", "Invesco QQQ Trust", "QQQ" },
                    { new Guid("019a0000-0000-7000-8001-000000001005"), true, new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "USD", "ARCX", "fund", "iShares MSCI South Korea ETF", "EWY" },
                    { new Guid("019a0000-0000-7000-8001-000000001006"), true, new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "USD", "XNAS", "fund", "iShares MSCI China ETF", "MCHI" },
                    { new Guid("019a0000-0000-7000-8001-000000002001"), true, new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "USD", "XNAS", "index", "Nasdaq-100", "^NDX" },
                    { new Guid("019a0000-0000-7000-8001-000000002002"), true, new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "EUR", "XETR", "index", "DAX", "^GDAXI" },
                    { new Guid("019a0000-0000-7000-8001-000000002003"), true, new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "JPY", "XJPX", "index", "Nikkei 225", "^N225" }
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
                    { new Guid("019a0000-0000-7000-8001-000000000016"), "en", new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Lasers and optical parts for networks" },
                    { new Guid("019a0000-0000-7000-8001-000000000017"), "en", new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Electrical power and automation equipment" },
                    { new Guid("019a0000-0000-7000-8001-000000000018"), "en", new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Electric cars and batteries" },
                    { new Guid("019a0000-0000-7000-8001-000000000019"), "en", new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Invests in young tech companies" },
                    { new Guid("019a0000-0000-7000-8001-000000000020"), "en", new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Software for governments" },
                    { new Guid("019a0000-0000-7000-8001-000000001001"), "en", new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Tracks the 500 largest US companies" },
                    { new Guid("019a0000-0000-7000-8001-000000001002"), "en", new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Tracks the 500 largest US companies" },
                    { new Guid("019a0000-0000-7000-8001-000000001003"), "en", new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Large companies in developed countries" },
                    { new Guid("019a0000-0000-7000-8001-000000001004"), "en", new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Tracks the Nasdaq-100" },
                    { new Guid("019a0000-0000-7000-8001-000000001005"), "en", new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Large South Korean companies" },
                    { new Guid("019a0000-0000-7000-8001-000000001006"), "en", new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Large Chinese companies" },
                    { new Guid("019a0000-0000-7000-8001-000000002001"), "en", new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "The 100 largest non-financial Nasdaq companies" },
                    { new Guid("019a0000-0000-7000-8001-000000002002"), "en", new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "The 40 largest German companies" },
                    { new Guid("019a0000-0000-7000-8001-000000002003"), "en", new DateTimeOffset(new DateTime(2026, 10, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "225 large Japanese companies" }
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_assets_kind",
                table: "assets",
                sql: "kind IN ('stock', 'fund', 'index')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_assets_kind",
                table: "assets");

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
                keyValues: new object[] { new Guid("019a0000-0000-7000-8001-000000000016"), "en" });

            migrationBuilder.DeleteData(
                table: "asset_texts",
                keyColumns: new[] { "asset_id", "locale" },
                keyValues: new object[] { new Guid("019a0000-0000-7000-8001-000000000017"), "en" });

            migrationBuilder.DeleteData(
                table: "asset_texts",
                keyColumns: new[] { "asset_id", "locale" },
                keyValues: new object[] { new Guid("019a0000-0000-7000-8001-000000000018"), "en" });

            migrationBuilder.DeleteData(
                table: "asset_texts",
                keyColumns: new[] { "asset_id", "locale" },
                keyValues: new object[] { new Guid("019a0000-0000-7000-8001-000000000019"), "en" });

            migrationBuilder.DeleteData(
                table: "asset_texts",
                keyColumns: new[] { "asset_id", "locale" },
                keyValues: new object[] { new Guid("019a0000-0000-7000-8001-000000000020"), "en" });

            migrationBuilder.DeleteData(
                table: "asset_texts",
                keyColumns: new[] { "asset_id", "locale" },
                keyValues: new object[] { new Guid("019a0000-0000-7000-8001-000000001001"), "en" });

            migrationBuilder.DeleteData(
                table: "asset_texts",
                keyColumns: new[] { "asset_id", "locale" },
                keyValues: new object[] { new Guid("019a0000-0000-7000-8001-000000001002"), "en" });

            migrationBuilder.DeleteData(
                table: "asset_texts",
                keyColumns: new[] { "asset_id", "locale" },
                keyValues: new object[] { new Guid("019a0000-0000-7000-8001-000000001003"), "en" });

            migrationBuilder.DeleteData(
                table: "asset_texts",
                keyColumns: new[] { "asset_id", "locale" },
                keyValues: new object[] { new Guid("019a0000-0000-7000-8001-000000001004"), "en" });

            migrationBuilder.DeleteData(
                table: "asset_texts",
                keyColumns: new[] { "asset_id", "locale" },
                keyValues: new object[] { new Guid("019a0000-0000-7000-8001-000000001005"), "en" });

            migrationBuilder.DeleteData(
                table: "asset_texts",
                keyColumns: new[] { "asset_id", "locale" },
                keyValues: new object[] { new Guid("019a0000-0000-7000-8001-000000001006"), "en" });

            migrationBuilder.DeleteData(
                table: "asset_texts",
                keyColumns: new[] { "asset_id", "locale" },
                keyValues: new object[] { new Guid("019a0000-0000-7000-8001-000000002001"), "en" });

            migrationBuilder.DeleteData(
                table: "asset_texts",
                keyColumns: new[] { "asset_id", "locale" },
                keyValues: new object[] { new Guid("019a0000-0000-7000-8001-000000002002"), "en" });

            migrationBuilder.DeleteData(
                table: "asset_texts",
                keyColumns: new[] { "asset_id", "locale" },
                keyValues: new object[] { new Guid("019a0000-0000-7000-8001-000000002003"), "en" });

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
                keyValue: new Guid("019a0000-0000-7000-8001-000000000016"));

            migrationBuilder.DeleteData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000017"));

            migrationBuilder.DeleteData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000018"));

            migrationBuilder.DeleteData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000019"));

            migrationBuilder.DeleteData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000020"));

            migrationBuilder.DeleteData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000001001"));

            migrationBuilder.DeleteData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000001002"));

            migrationBuilder.DeleteData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000001003"));

            migrationBuilder.DeleteData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000001004"));

            migrationBuilder.DeleteData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000001005"));

            migrationBuilder.DeleteData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000001006"));

            migrationBuilder.DeleteData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000002001"));

            migrationBuilder.DeleteData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000002002"));

            migrationBuilder.DeleteData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000002003"));

            migrationBuilder.AddCheckConstraint(
                name: "ck_assets_kind",
                table: "assets",
                sql: "kind IN ('stock', 'fund')");
        }
    }
}
