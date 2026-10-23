using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrategyTradingLab.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPrices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fx_rates",
                columns: table => new
                {
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    @base = table.Column<string>(name: "base", type: "char(3)", nullable: false),
                    quote = table.Column<string>(type: "char(3)", nullable: false),
                    rate = table.Column<decimal>(type: "numeric(18,8)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fx_rates", x => new { x.date, x.@base, x.quote });
                    table.CheckConstraint("ck_fx_rates_rate", "rate > 0");
                });

            migrationBuilder.CreateTable(
                name: "prices_daily",
                columns: table => new
                {
                    asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    open = table.Column<decimal>(type: "numeric(14,4)", nullable: false),
                    high = table.Column<decimal>(type: "numeric(14,4)", nullable: false),
                    low = table.Column<decimal>(type: "numeric(14,4)", nullable: false),
                    close = table.Column<decimal>(type: "numeric(14,4)", nullable: false),
                    volume = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_prices_daily", x => new { x.asset_id, x.date });
                    table.CheckConstraint("ck_prices_daily_prices", "open > 0 AND high > 0 AND low > 0 AND close > 0");
                    table.CheckConstraint("ck_prices_daily_range", "low <= high");
                    table.CheckConstraint("ck_prices_daily_volume", "volume >= 0");
                    table.ForeignKey(
                        name: "fk_prices_daily_assets_asset_id",
                        column: x => x.asset_id,
                        principalTable: "assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fx_rates");

            migrationBuilder.DropTable(
                name: "prices_daily");
        }
    }
}
