using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPortfolioValueHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PortfolioValueHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    RecordedDate = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: ""),
                    TotalValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    StocksValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    CashValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    OptionsValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortfolioValueHistories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioValueHistories_RecordedDate",
                table: "PortfolioValueHistories",
                column: "RecordedDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PortfolioValueHistories");
        }
    }
}
