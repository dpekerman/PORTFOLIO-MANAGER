using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPreMarketAgent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PreMarketAgentConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    ScheduleEnabled = table.Column<bool>(type: "bit", nullable: false),
                    StartTimeLocal = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    FirstRunDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AsOfDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DeadlineLocal = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    ShadowMode = table.Column<bool>(type: "bit", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreMarketAgentConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PreMarketAgentRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunKey = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AsOfDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsTestRun = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DurationMs = table.Column<int>(type: "int", nullable: false),
                    ReportJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReportHtml = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmailStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EmailMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmailSubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreMarketAgentRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PreMarketAgentRuns_RunKey",
                table: "PreMarketAgentRuns",
                column: "RunKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PreMarketAgentConfigs");

            migrationBuilder.DropTable(
                name: "PreMarketAgentRuns");
        }
    }
}
