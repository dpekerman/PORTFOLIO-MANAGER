using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSectorIndustryConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SectorIndustryConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    SectorsJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    IndustriesJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    DecisionSourcesJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectorIndustryConfigs", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SectorIndustryConfigs");
        }
    }
}
