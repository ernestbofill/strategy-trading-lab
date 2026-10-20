using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrategyTradingLab.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedErnest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8000-000000000001"),
                columns: new[] { "email", "password_hash" },
                values: new object[] { "ernest@strategytradinglab.local", "AQAAAAIAAYagAAAAEEkw/SFwEfBT+xCwwf3uaFOLXNoGjm9pJWiDNunC/TnZTcSPl8IDVQolTo5td97WGA==" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "id",
                keyValue: new Guid("019a0000-0000-7000-8000-000000000001"),
                columns: new[] { "email", "password_hash" },
                values: new object[] { "dev@strategytradinglab.local", null });
        }
    }
}
