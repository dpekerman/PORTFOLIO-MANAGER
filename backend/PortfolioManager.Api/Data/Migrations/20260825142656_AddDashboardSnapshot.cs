using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDashboardSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DashboardSnapshots",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "{}"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DashboardSnapshots", x => x.UserId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DashboardSnapshots");
        }
    }
}
