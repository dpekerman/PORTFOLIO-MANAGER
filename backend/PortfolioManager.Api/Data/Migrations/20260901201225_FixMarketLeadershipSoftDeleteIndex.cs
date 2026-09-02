using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixMarketLeadershipSoftDeleteIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MarketLeadershipTrackers_UserId_Symbol_IsActive",
                table: "MarketLeadershipTrackers");

            migrationBuilder.CreateIndex(
                name: "IX_MarketLeadershipTrackers_UserId_Symbol",
                table: "MarketLeadershipTrackers",
                columns: new[] { "UserId", "Symbol" },
                unique: true,
                filter: "[IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MarketLeadershipTrackers_UserId_Symbol",
                table: "MarketLeadershipTrackers");

            migrationBuilder.CreateIndex(
                name: "IX_MarketLeadershipTrackers_UserId_Symbol_IsActive",
                table: "MarketLeadershipTrackers",
                columns: new[] { "UserId", "Symbol", "IsActive" },
                unique: true);
        }
    }
}
