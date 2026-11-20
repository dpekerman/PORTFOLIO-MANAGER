using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CorrectPm033EpisodeIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RiskPlans_UserId_AccountType_Symbol",
                table: "RiskPlans");

            migrationBuilder.AddColumn<Guid>(
                name: "TradeEpisodeId",
                table: "RiskAuditEvents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RiskPlans_UserId_AccountType_Symbol_TradeEpisodeId",
                table: "RiskPlans",
                columns: new[] { "UserId", "AccountType", "Symbol", "TradeEpisodeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RiskAuditEvents_UserId_TradeEpisodeId_OccurredAtUtc",
                table: "RiskAuditEvents",
                columns: new[] { "UserId", "TradeEpisodeId", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM [RiskAuditEvents]) OR EXISTS (SELECT 1 FROM [RiskPlans])
    OR EXISTS (SELECT 1 FROM [BrokerStopRecords]) OR EXISTS (SELECT 1 FROM [TradeEpisodes])
    OR EXISTS (SELECT 1 FROM [PortfolioItems] WHERE [TradeEpisodeId] IS NOT NULL)
    THROW 51033, 'Corrective rollback refused: populated trade/risk history. Use verified database recovery.', 1;");

            migrationBuilder.DropIndex(
                name: "IX_RiskPlans_UserId_AccountType_Symbol_TradeEpisodeId",
                table: "RiskPlans");

            migrationBuilder.DropIndex(
                name: "IX_RiskAuditEvents_UserId_TradeEpisodeId_OccurredAtUtc",
                table: "RiskAuditEvents");

            migrationBuilder.DropColumn(
                name: "TradeEpisodeId",
                table: "RiskAuditEvents");

            migrationBuilder.CreateIndex(
                name: "IX_RiskPlans_UserId_AccountType_Symbol",
                table: "RiskPlans",
                columns: new[] { "UserId", "AccountType", "Symbol" },
                unique: true,
                filter: "[UserId] IS NOT NULL");
        }
    }
}
