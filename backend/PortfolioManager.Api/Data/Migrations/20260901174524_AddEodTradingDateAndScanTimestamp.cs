using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEodTradingDateAndScanTimestamp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ScannedAt",
                table: "DailySignals",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TradingDate",
                table: "DailySignals",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailySignals_Symbol_ScanType_SignalType_TradingDate",
                table: "DailySignals",
                columns: new[] { "Symbol", "ScanType", "SignalType", "TradingDate" },
                unique: true,
                filter: "[TradingDate] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DailySignals_Symbol_ScanType_SignalType_TradingDate",
                table: "DailySignals");

            migrationBuilder.DropColumn(
                name: "ScannedAt",
                table: "DailySignals");

            migrationBuilder.DropColumn(
                name: "TradingDate",
                table: "DailySignals");
        }
    }
}
