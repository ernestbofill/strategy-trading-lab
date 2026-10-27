using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrategyTradingLab.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "added_by",
                table: "assets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "industry",
                table: "assets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sector",
                table: "assets",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000001"),
                columns: new[] { "added_by", "industry", "sector" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000002"),
                columns: new[] { "added_by", "industry", "sector" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000003"),
                columns: new[] { "added_by", "industry", "sector" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000004"),
                columns: new[] { "added_by", "industry", "sector" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000005"),
                columns: new[] { "added_by", "industry", "sector" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000006"),
                columns: new[] { "added_by", "industry", "sector" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000007"),
                columns: new[] { "added_by", "industry", "sector" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000008"),
                columns: new[] { "added_by", "industry", "sector" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000009"),
                columns: new[] { "added_by", "industry", "sector" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000010"),
                columns: new[] { "added_by", "industry", "sector" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000011"),
                columns: new[] { "added_by", "industry", "sector" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000012"),
                columns: new[] { "added_by", "industry", "sector" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000013"),
                columns: new[] { "added_by", "industry", "sector" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000014"),
                columns: new[] { "added_by", "industry", "sector" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000015"),
                columns: new[] { "added_by", "industry", "sector" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000016"),
                columns: new[] { "added_by", "industry", "sector" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000017"),
                columns: new[] { "added_by", "industry", "sector" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000018"),
                columns: new[] { "added_by", "industry", "sector" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000019"),
                columns: new[] { "added_by", "industry", "sector" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000000020"),
                columns: new[] { "added_by", "industry", "sector" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000001001"),
                columns: new[] { "added_by", "industry", "sector" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000001002"),
                columns: new[] { "added_by", "industry", "sector" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000001003"),
                columns: new[] { "added_by", "industry", "sector" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000001004"),
                columns: new[] { "added_by", "industry", "sector" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000001005"),
                columns: new[] { "added_by", "industry", "sector" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000001006"),
                columns: new[] { "added_by", "industry", "sector" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000002001"),
                columns: new[] { "added_by", "industry", "sector" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000002002"),
                columns: new[] { "added_by", "industry", "sector" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "assets",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8001-000000002003"),
                columns: new[] { "added_by", "industry", "sector" },
                values: new object[] { null, null, null });

            migrationBuilder.CreateIndex(
                name: "ix_assets_added_by",
                table: "assets",
                column: "added_by");

            migrationBuilder.AddForeignKey(
                name: "fk_assets_users_added_by",
                table: "assets",
                column: "added_by",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_assets_users_added_by",
                table: "assets");

            migrationBuilder.DropIndex(
                name: "ix_assets_added_by",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "added_by",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "industry",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "sector",
                table: "assets");
        }
    }
}
