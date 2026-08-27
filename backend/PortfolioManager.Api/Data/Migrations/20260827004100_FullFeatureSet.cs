using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class FullFeatureSet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WatchlistTier",
                table: "WatchlistItems",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Strategic");

            migrationBuilder.AddColumn<string>(
                name: "PreviousSignalState",
                table: "DailySignals",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TransactionContextSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransactionId = table.Column<int>(type: "int", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RsiAtEntry = table.Column<decimal>(type: "decimal(7,4)", nullable: true),
                    TrendShiftAtEntry = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FibZoneAtEntry = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    VolumeSignalAtEntry = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    TurnStrengthAtEntry = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ValueScoreAtEntry = table.Column<decimal>(type: "decimal(7,2)", nullable: true),
                    ValueTierAtEntry = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    HoldingRoleAtEntry = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SectorAllocationStatusAtEntry = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionContextSnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionContextSnapshots_TransactionId",
                table: "TransactionContextSnapshots",
                column: "TransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransactionContextSnapshots");

            migrationBuilder.DropColumn(
                name: "WatchlistTier",
                table: "WatchlistItems");

            migrationBuilder.DropColumn(
                name: "PreviousSignalState",
                table: "DailySignals");
        }
    }
}
