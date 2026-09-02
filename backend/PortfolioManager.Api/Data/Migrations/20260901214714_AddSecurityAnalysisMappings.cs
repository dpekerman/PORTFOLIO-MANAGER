using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PortfolioManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSecurityAnalysisMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SecurityAnalysisMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TradingTicker = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UnderlyingTicker = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    UnderlyingMarket = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UseUnderlyingForAnalysis = table.Column<bool>(type: "bit", nullable: false),
                    ResolutionStatus = table.Column<int>(type: "int", nullable: false),
                    MappingSource = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DetectionDetail = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityAnalysisMappings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "SecurityAnalysisMappings",
                columns: new[] { "Id", "CreatedAt", "DetectionDetail", "MappingSource", "ResolutionStatus", "TradingTicker", "UnderlyingMarket", "UnderlyingTicker", "UpdatedAt", "UseUnderlyingForAnalysis", "UserId" },
                values: new object[,]
                {
                    { -3, new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Managed CDR reference data", 0, 1, "MU.TO", "US", "MU", new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, null },
                    { -2, new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Managed CDR reference data", 0, 1, "DIS.TO", "US", "DIS", new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, null },
                    { -1, new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Managed CDR reference data", 0, 1, "SPGI.TO", "US", "SPGI", new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityAnalysisMappings_TradingTicker_UserId",
                table: "SecurityAnalysisMappings",
                columns: new[] { "TradingTicker", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityAnalysisMappings_UnderlyingTicker",
                table: "SecurityAnalysisMappings",
                column: "UnderlyingTicker");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SecurityAnalysisMappings");
        }
    }
}
