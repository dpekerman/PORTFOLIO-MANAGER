using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSignalAuditEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SignalAuditEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AnalysisSymbol = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    SignalType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    SignalCategory = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    SignalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    SignalPrice = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    Score = table.Column<decimal>(type: "decimal(9,2)", nullable: true),
                    Role = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Origin = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RuleVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    EpisodeKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    EpisodeEndedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ContextJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MyAction = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MyActionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MyActionUpdatedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    LinkedTransactionId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignalAuditEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SignalAuditEvents_UserId_EpisodeKey",
                table: "SignalAuditEvents",
                columns: new[] { "UserId", "EpisodeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SignalAuditEvents_UserId_SignalDate",
                table: "SignalAuditEvents",
                columns: new[] { "UserId", "SignalDate" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_SignalAuditEvents_UserId_Symbol_SignalType",
                table: "SignalAuditEvents",
                columns: new[] { "UserId", "Symbol", "SignalType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SignalAuditEvents");
        }
    }
}
