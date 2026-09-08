using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAutomationRunLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AutomationRunLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TradingDate = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TriggerType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ScheduledStartUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualStartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OverallStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Running"),
                    OwnerUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RefreshStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: ""),
                    RefreshStartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RefreshCompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PortfolioSymbolCount = table.Column<int>(type: "int", nullable: false),
                    WatchlistSymbolCount = table.Column<int>(type: "int", nullable: false),
                    RsiStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: ""),
                    RsiCompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EodSignalsPersistedCount = table.Column<int>(type: "int", nullable: false),
                    SnapshotStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: ""),
                    SnapshotCompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SnapshotSource = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ValueScreenerStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: ""),
                    ValueScreenerLastRunAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PowerRequestAcquiredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PowerRequestReleasedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ErrorStep = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    MachineName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationRunLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationRunLogs_RunId",
                table: "AutomationRunLogs",
                column: "RunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AutomationRunLogs_TradingDate",
                table: "AutomationRunLogs",
                column: "TradingDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutomationRunLogs");
        }
    }
}
