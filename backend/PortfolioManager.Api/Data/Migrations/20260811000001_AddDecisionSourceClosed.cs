using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDecisionSourceClosed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DecisionSourceClosed",
                table: "PortfolioItems",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DecisionSourceClosed",
                table: "PortfolioItems");
        }
    }
}
