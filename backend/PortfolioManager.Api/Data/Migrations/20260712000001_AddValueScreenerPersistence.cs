using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddValueScreenerPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ValueScreenerSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Origin = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RunAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResultsJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValueScreenerSnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ValueScreenerSnapshots_Origin_RunAt",
                table: "ValueScreenerSnapshots",
                columns: new[] { "Origin", "RunAt" });

            migrationBuilder.CreateTable(
                name: "ValueScreenerScheduleConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ScheduledTimeEt = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "17:00"),
                    Enabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    LastPortfolioRunAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastWatchlistRunAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValueScreenerScheduleConfigs", x => x.Id);
                });

            // Seed default schedule config
            migrationBuilder.InsertData(
                table: "ValueScreenerScheduleConfigs",
                columns: new[] { "ScheduledTimeEt", "Enabled" },
                values: new object[] { "17:00", true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ValueScreenerSnapshots");
            migrationBuilder.DropTable(name: "ValueScreenerScheduleConfigs");
        }
    }
}
