using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTradeEpisodesAndRiskTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TradeEpisodeId",
                table: "PortfolioItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BrokerStopRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderIdentity = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImportKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    AccountType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OrderType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TriggerPrice = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    LimitPrice = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    UserStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    BrokerVerificationStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CoveredQuantity = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    PlacedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BrokerOrderRef = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastUserVerifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmationSource = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TradeEpisodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EpisodeOpenDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReplacedByRecordId = table.Column<int>(type: "int", nullable: true),
                    BrokerCancellationUnverified = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrokerStopRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RiskPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    AccountType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PlannedStopPrice = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    TargetPrice = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    StopType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    StopRationale = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TradeRationale = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RiskNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    TradeEpisodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EpisodeOpenDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TradeEpisodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    AccountType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OpenDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradeEpisodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RiskAuditEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AccountType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    RiskPlanId = table.Column<int>(type: "int", nullable: true),
                    BrokerStopRecordId = table.Column<int>(type: "int", nullable: true),
                    EventType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ChangesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskAuditEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiskAuditEvents_BrokerStopRecords_BrokerStopRecordId",
                        column: x => x.BrokerStopRecordId,
                        principalTable: "BrokerStopRecords",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RiskAuditEvents_RiskPlans_RiskPlanId",
                        column: x => x.RiskPlanId,
                        principalTable: "RiskPlans",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioItems_TradeEpisodeId",
                table: "PortfolioItems",
                column: "TradeEpisodeId");

            migrationBuilder.CreateIndex(
                name: "IX_BrokerStopRecords_AccountType_Symbol_UserStatus",
                table: "BrokerStopRecords",
                columns: new[] { "AccountType", "Symbol", "UserStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_BrokerStopRecords_OrderIdentity",
                table: "BrokerStopRecords",
                column: "OrderIdentity",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_BrokerStopRecords_ImportKey",
                table: "BrokerStopRecords",
                column: "ImportKey",
                unique: true,
                filter: "[ImportKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RiskAuditEvents_AccountType_Symbol_OccurredAtUtc",
                table: "RiskAuditEvents",
                columns: new[] { "AccountType", "Symbol", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RiskAuditEvents_BrokerStopRecordId",
                table: "RiskAuditEvents",
                column: "BrokerStopRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskAuditEvents_RiskPlanId",
                table: "RiskAuditEvents",
                column: "RiskPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskPlans_UserId_AccountType_Symbol",
                table: "RiskPlans",
                columns: new[] { "UserId", "AccountType", "Symbol" },
                unique: true,
                filter: "[UserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TradeEpisodes_AccountType_Symbol_Status",
                table: "TradeEpisodes",
                columns: new[] { "AccountType", "Symbol", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rollback safety: never destroy real stop records, risk history or episode assignments.
            // Recovery once real data exists is restore-from-backup only.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM [BrokerStopRecords]) OR EXISTS (SELECT 1 FROM [RiskPlans])
   OR EXISTS (SELECT 1 FROM [RiskAuditEvents]) OR EXISTS (SELECT 1 FROM [TradeEpisodes])
   OR EXISTS (SELECT 1 FROM [PortfolioItems] WHERE [TradeEpisodeId] IS NOT NULL)
    THROW 51033, 'PM-033 rollback refused: risk or trade-episode data exists. Restore from the pre-migration backup instead.', 1;");

            migrationBuilder.DropTable(
                name: "RiskAuditEvents");

            migrationBuilder.DropTable(
                name: "TradeEpisodes");

            migrationBuilder.DropTable(
                name: "BrokerStopRecords");

            migrationBuilder.DropTable(
                name: "RiskPlans");

            migrationBuilder.DropIndex(
                name: "IX_PortfolioItems_TradeEpisodeId",
                table: "PortfolioItems");

            migrationBuilder.DropColumn(
                name: "TradeEpisodeId",
                table: "PortfolioItems");
        }
    }
}
