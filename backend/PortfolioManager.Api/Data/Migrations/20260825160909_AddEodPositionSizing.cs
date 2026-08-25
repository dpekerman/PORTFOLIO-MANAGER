using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEodPositionSizing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PositionSizingLimitingReason",
                table: "DailySignals",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PositionSizingPositionValue",
                table: "DailySignals",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PositionSizingRiskAmount",
                table: "DailySignals",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PositionSizingShares",
                table: "DailySignals",
                type: "decimal(18,6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PositionSizingLimitingReason",
                table: "DailySignals");

            migrationBuilder.DropColumn(
                name: "PositionSizingPositionValue",
                table: "DailySignals");

            migrationBuilder.DropColumn(
                name: "PositionSizingRiskAmount",
                table: "DailySignals");

            migrationBuilder.DropColumn(
                name: "PositionSizingShares",
                table: "DailySignals");
        }
    }
}
