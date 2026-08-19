using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFibonacciToDailySignals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Fib61_8AtSignal",
                table: "DailySignals",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FibStatusAtSignal",
                table: "DailySignals",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FibZoneAtSignal",
                table: "DailySignals",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Fib61_8AtSignal",
                table: "DailySignals");

            migrationBuilder.DropColumn(
                name: "FibStatusAtSignal",
                table: "DailySignals");

            migrationBuilder.DropColumn(
                name: "FibZoneAtSignal",
                table: "DailySignals");
        }
    }
}
