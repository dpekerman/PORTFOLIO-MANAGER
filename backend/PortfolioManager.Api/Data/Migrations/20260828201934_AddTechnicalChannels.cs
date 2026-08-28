using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTechnicalChannels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TechnicalChannels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ticker = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Timeframe = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Slope = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    LowerRailCurrent = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UpperRailCurrent = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ChannelQuality = table.Column<int>(type: "int", nullable: false),
                    LowerTouchCount = table.Column<int>(type: "int", nullable: false),
                    LastLowerTouchDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DistanceToLowerRailPercent = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    DistanceToLowerRailATR = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ChannelState = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    NearestOpenGapAbove = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    NearestOpenGapBelow = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    DistanceToGapAbovePercent = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    DistanceToGapBelowPercent = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    CalculatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicalChannels", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalChannels_Ticker_Timeframe",
                table: "TechnicalChannels",
                columns: new[] { "Ticker", "Timeframe" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TechnicalChannels");
        }
    }
}
