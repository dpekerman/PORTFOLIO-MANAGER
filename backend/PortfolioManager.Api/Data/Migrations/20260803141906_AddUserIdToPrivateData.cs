using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdToPrivateData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WatchlistItems_Symbol",
                table: "WatchlistItems");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "WatchlistItems",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "PortfolioItems",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "OptionItems",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "CashItems",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WatchlistItems_Symbol_UserId",
                table: "WatchlistItems",
                columns: new[] { "Symbol", "UserId" },
                unique: true,
                filter: "[UserId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WatchlistItems_Symbol_UserId",
                table: "WatchlistItems");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "WatchlistItems");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "PortfolioItems");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "OptionItems");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "CashItems");

            migrationBuilder.CreateIndex(
                name: "IX_WatchlistItems_Symbol",
                table: "WatchlistItems",
                column: "Symbol",
                unique: true);
        }
    }
}
