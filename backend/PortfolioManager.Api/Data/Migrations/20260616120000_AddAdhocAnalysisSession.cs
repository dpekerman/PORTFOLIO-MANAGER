using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAdhocAnalysisSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdhocAnalysisSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "default"),
                    Symbols = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    ResultsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OversoldThreshold = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 30m),
                    OverboughtThreshold = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 75m),
                    LogicMode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Legacy"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdhocAnalysisSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdhocAnalysisSessions_SessionKey_UpdatedAt",
                table: "AdhocAnalysisSessions",
                columns: new[] { "SessionKey", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AdhocAnalysisSessions");
        }
    }
}
