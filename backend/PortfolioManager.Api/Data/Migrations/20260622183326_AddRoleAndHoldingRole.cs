using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleAndHoldingRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "WatchlistItems",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Strategic");

            migrationBuilder.AddColumn<string>(
                name: "HoldingRole",
                table: "PortfolioItems",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Role",
                table: "WatchlistItems");

            migrationBuilder.DropColumn(
                name: "HoldingRole",
                table: "PortfolioItems");
        }
    }
}
