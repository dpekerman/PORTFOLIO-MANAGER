using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCashFlowType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CashFlowType",
                table: "CashItems",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CashLedgerSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LedgerStartDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashLedgerSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashItems_Account_OpeningBalance",
                table: "CashItems",
                columns: new[] { "AccountType", "TransactionDate" },
                unique: true,
                filter: "[CashFlowType] = 'OpeningBalance'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashLedgerSettings");

            migrationBuilder.DropIndex(
                name: "IX_CashItems_Account_OpeningBalance",
                table: "CashItems");

            migrationBuilder.DropColumn(
                name: "CashFlowType",
                table: "CashItems");
        }
    }
}
