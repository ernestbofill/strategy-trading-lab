using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrategyTradingLab.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetPricesProblem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "prices_checked_at",
                table: "assets",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "prices_problem",
                table: "assets",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000001"),
                columns: new[] { "prices_checked_at", "prices_problem" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000002"),
                columns: new[] { "prices_checked_at", "prices_problem" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000003"),
                columns: new[] { "prices_checked_at", "prices_problem" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000004"),
                columns: new[] { "prices_checked_at", "prices_problem" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000005"),
                columns: new[] { "prices_checked_at", "prices_problem" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000006"),
                columns: new[] { "prices_checked_at", "prices_problem" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000007"),
                columns: new[] { "prices_checked_at", "prices_problem" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000008"),
                columns: new[] { "prices_checked_at", "prices_problem" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000009"),
                columns: new[] { "prices_checked_at", "prices_problem" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000010"),
                columns: new[] { "prices_checked_at", "prices_problem" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000011"),
                columns: new[] { "prices_checked_at", "prices_problem" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000012"),
                columns: new[] { "prices_checked_at", "prices_problem" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000013"),
                columns: new[] { "prices_checked_at", "prices_problem" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000014"),
                columns: new[] { "prices_checked_at", "prices_problem" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000015"),
                columns: new[] { "prices_checked_at", "prices_problem" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000016"),
                columns: new[] { "prices_checked_at", "prices_problem" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000017"),
                columns: new[] { "prices_checked_at", "prices_problem" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000018"),
                columns: new[] { "prices_checked_at", "prices_problem" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000019"),
                columns: new[] { "prices_checked_at", "prices_problem" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000020"),
                columns: new[] { "prices_checked_at", "prices_problem" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000001001"),
                columns: new[] { "prices_checked_at", "prices_problem" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000001002"),
                columns: new[] { "prices_checked_at", "prices_problem" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000001003"),
                columns: new[] { "prices_checked_at", "prices_problem" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000001004"),
                columns: new[] { "prices_checked_at", "prices_problem" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000001005"),
                columns: new[] { "prices_checked_at", "prices_problem" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000001006"),
                columns: new[] { "prices_checked_at", "prices_problem" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000002001"),
                columns: new[] { "prices_checked_at", "prices_problem" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000002002"),
                columns: new[] { "prices_checked_at", "prices_problem" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000002003"),
                columns: new[] { "prices_checked_at", "prices_problem" },
                values: new object[] { null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "prices_checked_at",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "prices_problem",
                table: "assets");
        }
    }
}
