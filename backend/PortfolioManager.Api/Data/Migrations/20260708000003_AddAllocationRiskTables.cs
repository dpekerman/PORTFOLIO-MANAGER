using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAllocationRiskTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AllocationRiskTargets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Role = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TargetPct = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table => { table.PrimaryKey("PK_AllocationRiskTargets", x => x.Id); });

            migrationBuilder.CreateTable(
                name: "AllocationSectorTargets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Sector = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TargetPct = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table => { table.PrimaryKey("PK_AllocationSectorTargets", x => x.Id); });

            migrationBuilder.CreateTable(
                name: "SinglePositionLimits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Role = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TargetPct = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table => { table.PrimaryKey("PK_SinglePositionLimits", x => x.Id); });

            // ── Seed default data ─────────────────────────────────────────────────
            migrationBuilder.InsertData("AllocationRiskTargets",
                new[] { "Role", "TargetPct", "DisplayOrder" },
                new object[,]
                {
                    { "Core",             40m, 1 },
                    { "Strategic",        15m, 2 },
                    { "Strategic-Income",  5m, 3 },
                    { "Swing",            20m, 4 },
                    { "Speculative",      10m, 5 },
                    { "Options",           5m, 6 },
                    { "Cash",              5m, 7 },
                });

            migrationBuilder.InsertData("AllocationSectorTargets",
                new[] { "Sector", "TargetPct", "DisplayOrder" },
                new object[,]
                {
                    { "Energy",                 20m,  1 },
                    { "Industrials",            20m,  2 },
                    { "Financial Services",     15m,  3 },
                    { "Communication Services",  5m,  4 },
                    { "Utilities",              10m,  5 },
                    { "Technology",             10m,  6 },
                    { "Healthcare",              5m,  7 },
                    { "Consumer Defensive",     10m,  8 },
                    { "Materials",               3m,  9 },
                    { "Cash",                    2m, 10 },
                });

            migrationBuilder.InsertData("SinglePositionLimits",
                new[] { "Role", "TargetPct", "DisplayOrder" },
                new object[,]
                {
                    { "Core",             5m, 1 },
                    { "Strategic",        5m, 2 },
                    { "Strategic-Income", 5m, 3 },
                    { "Swing",            2m, 4 },
                    { "Speculative",      2m, 5 },
                    { "Options",          1m, 6 },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AllocationRiskTargets");
            migrationBuilder.DropTable(name: "AllocationSectorTargets");
            migrationBuilder.DropTable(name: "SinglePositionLimits");
        }
    }
}
