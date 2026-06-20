using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemovePortfolioSymbolUniqueConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PortfolioItems_Symbol",
                table: "PortfolioItems");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioItems_Symbol",
                table: "PortfolioItems",
                column: "Symbol");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PortfolioItems_Symbol",
                table: "PortfolioItems");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioItems_Symbol",
                table: "PortfolioItems",
                column: "Symbol",
                unique: true);
        }
    }
}
