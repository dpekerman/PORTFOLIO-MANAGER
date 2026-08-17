using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNotesFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "PortfolioItems",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "OptionItems",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Notes",
                table: "PortfolioItems");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "OptionItems");
        }
    }
}
