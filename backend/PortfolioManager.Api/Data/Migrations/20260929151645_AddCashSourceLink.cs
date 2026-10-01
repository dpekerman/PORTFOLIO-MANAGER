using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCashSourceLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SourceItemId",
                table: "CashItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "CashItems",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashItems_TradeLink",
                table: "CashItems",
                columns: new[] { "SourceType", "SourceItemId" },
                unique: true,
                filter: "[SourceType] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CashItems_TradeLink",
                table: "CashItems");

            migrationBuilder.DropColumn(
                name: "SourceItemId",
                table: "CashItems");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "CashItems");
        }
    }
}
