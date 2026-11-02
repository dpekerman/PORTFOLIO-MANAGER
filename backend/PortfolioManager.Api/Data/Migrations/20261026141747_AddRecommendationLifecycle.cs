using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRecommendationLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastValidatedAtUtc",
                table: "SignalAuditEvents",
                type: "datetime2(3)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PerformanceAudited",
                table: "SignalAuditEvents",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "SignalAuditEvents",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<DateTime>(
                name: "StatusChangedAtUtc",
                table: "SignalAuditEvents",
                type: "datetime2(3)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StatusReason",
                table: "SignalAuditEvents",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SupersededByEventId",
                table: "SignalAuditEvents",
                type: "bigint",
                nullable: true);

            // Backfill (additive; immutable facts and user actions are never touched).
            // Closed episodes keep their history as Invalidated; open rows stay Active. Legacy Actioned rows become Acted
            // only with evidence (a linked transaction or a recorded action time). Not Actioned is NOT mapped to Dismissed.
            migrationBuilder.Sql(@"
UPDATE SignalAuditEvents SET Status = 'Invalidated', StatusReason = 'legacy closed', StatusChangedAtUtc = CapturedAtUtc
WHERE EpisodeEndedDate IS NOT NULL;

UPDATE SignalAuditEvents SET Status = 'Acted', StatusReason = 'legacy actioned', StatusChangedAtUtc = COALESCE(MyActionUpdatedUtc, CapturedAtUtc)
WHERE EpisodeEndedDate IS NULL AND MyAction = 'Actioned' AND (LinkedTransactionId IS NOT NULL OR MyActionUpdatedUtc IS NOT NULL);

-- A recommendation identity may have only one open episode: keep the oldest, close later duplicates as Invalidated.
;WITH d AS (
    SELECT Id, ROW_NUMBER() OVER (PARTITION BY UserId, Symbol, SignalType, Source ORDER BY SignalDate, Id) AS rn
    FROM SignalAuditEvents WHERE EpisodeEndedDate IS NULL)
UPDATE e SET Status = 'Invalidated', StatusReason = 'legacy duplicate', StatusChangedAtUtc = GETUTCDATE(),
             EpisodeEndedDate = CAST(GETDATE() AS date)
FROM SignalAuditEvents e JOIN d ON d.Id = e.Id WHERE d.rn > 1;");

            migrationBuilder.CreateIndex(
                name: "IX_SignalAuditEvents_SupersededByEventId",
                table: "SignalAuditEvents",
                column: "SupersededByEventId");

            migrationBuilder.CreateIndex(
                name: "IX_SignalAuditEvents_UserId_Status",
                table: "SignalAuditEvents",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_SignalAuditEvents_OpenIdentity",
                table: "SignalAuditEvents",
                columns: new[] { "UserId", "Symbol", "SignalType", "Source" },
                unique: true,
                filter: "[EpisodeEndedDate] IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_SignalAuditEvents_SignalAuditEvents_SupersededByEventId",
                table: "SignalAuditEvents",
                column: "SupersededByEventId",
                principalTable: "SignalAuditEvents",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SignalAuditEvents_SignalAuditEvents_SupersededByEventId",
                table: "SignalAuditEvents");

            migrationBuilder.DropIndex(
                name: "IX_SignalAuditEvents_SupersededByEventId",
                table: "SignalAuditEvents");

            migrationBuilder.DropIndex(
                name: "IX_SignalAuditEvents_UserId_Status",
                table: "SignalAuditEvents");

            migrationBuilder.DropIndex(
                name: "UX_SignalAuditEvents_OpenIdentity",
                table: "SignalAuditEvents");

            migrationBuilder.DropColumn(
                name: "LastValidatedAtUtc",
                table: "SignalAuditEvents");

            migrationBuilder.DropColumn(
                name: "PerformanceAudited",
                table: "SignalAuditEvents");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "SignalAuditEvents");

            migrationBuilder.DropColumn(
                name: "StatusChangedAtUtc",
                table: "SignalAuditEvents");

            migrationBuilder.DropColumn(
                name: "StatusReason",
                table: "SignalAuditEvents");

            migrationBuilder.DropColumn(
                name: "SupersededByEventId",
                table: "SignalAuditEvents");
        }
    }
}
