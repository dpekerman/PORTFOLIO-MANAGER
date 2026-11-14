using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPm029TradingOpportunities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IndexConstituents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IndexCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "date", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    SourceAsOfDate = table.Column<DateTime>(type: "date", nullable: false),
                    VerifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VerifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndexConstituents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OpportunityRefreshStates",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    LastAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastAttemptStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ErrorSummary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LastSuccessfulSnapshotId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpportunityRefreshStates", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "OpportunitySnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ScoringVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EvaluationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ComputedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinalActionComputedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataAsOfSession = table.Column<DateTime>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ErrorSummary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpportunitySnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SectorBenchmarkMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Sector = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Market = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    BenchmarkSymbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BenchmarkName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ProviderSectorAliasesJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SourceAsOfDate = table.Column<DateTime>(type: "date", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectorBenchmarkMappings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IndexConstituents_IndexCode_Symbol_EffectiveFrom",
                table: "IndexConstituents",
                columns: new[] { "IndexCode", "Symbol", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpportunitySnapshots_UserId_ComputedAtUtc",
                table: "OpportunitySnapshots",
                columns: new[] { "UserId", "ComputedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SectorBenchmarkMappings_Sector_Market",
                table: "SectorBenchmarkMappings",
                columns: new[] { "Sector", "Market" },
                unique: true,
                filter: "[IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IndexConstituents");

            migrationBuilder.DropTable(
                name: "OpportunityRefreshStates");

            migrationBuilder.DropTable(
                name: "OpportunitySnapshots");

            migrationBuilder.DropTable(
                name: "SectorBenchmarkMappings");
        }
    }
}
