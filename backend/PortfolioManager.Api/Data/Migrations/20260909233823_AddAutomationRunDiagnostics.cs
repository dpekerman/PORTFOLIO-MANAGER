using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAutomationRunDiagnostics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastHeartbeatAtUtc",
                table: "AutomationRunLogs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastHeartbeatStep",
                table: "AutomationRunLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TriggerCorrelationId",
                table: "AutomationRunLogs",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AutomationRunLogs_TriggerCorrelationId",
                table: "AutomationRunLogs",
                column: "TriggerCorrelationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AutomationRunLogs_TriggerCorrelationId",
                table: "AutomationRunLogs");

            migrationBuilder.DropColumn(
                name: "LastHeartbeatAtUtc",
                table: "AutomationRunLogs");

            migrationBuilder.DropColumn(
                name: "LastHeartbeatStep",
                table: "AutomationRunLogs");

            migrationBuilder.DropColumn(
                name: "TriggerCorrelationId",
                table: "AutomationRunLogs");
        }
    }
}
