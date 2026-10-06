using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PortfolioManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountTypes", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "AccountTypes",
                columns: new[] { "Id", "Name", "NormalizedName", "Version" },
                values: new object[,]
                {
                    { 1, "TFSA_L_RBC", "TFSA_L_RBC", new Guid("00000000-0000-0000-0000-000000000001") },
                    { 2, "TFSA_L_TD", "TFSA_L_TD", new Guid("00000000-0000-0000-0000-000000000002") },
                    { 3, "TFSA_D_TD", "TFSA_D_TD", new Guid("00000000-0000-0000-0000-000000000003") },
                    { 4, "Margin_L_TD", "MARGIN_L_TD", new Guid("00000000-0000-0000-0000-000000000004") },
                    { 5, "Margin_L_RBC", "MARGIN_L_RBC", new Guid("00000000-0000-0000-0000-000000000005") },
                    { 6, "Margin_D_TD", "MARGIN_D_TD", new Guid("00000000-0000-0000-0000-000000000006") },
                    { 7, "Corp_TD", "CORP_TD", new Guid("00000000-0000-0000-0000-000000000007") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountTypes_NormalizedName",
                table: "AccountTypes",
                column: "NormalizedName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountTypes");
        }
    }
}
