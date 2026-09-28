using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSnapshotAuditAndCashModifiedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastRecalculatedAt",
                table: "PortfolioValueHistories",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Source",
                table: "PortfolioValueHistories",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Every row that exists at the moment this migration runs predates the audit-trail feature —
            // its true origin (EodAuto/Manual/reseal/recalculation) is unknown, so stamp it distinctly
            // rather than defaulting to EodAuto (int 4 = PortfolioValueSource.Migration).
            migrationBuilder.Sql("UPDATE [PortfolioValueHistories] SET [Source] = 4;");

            migrationBuilder.AddColumn<DateTime>(
                name: "ModifiedAt",
                table: "CashItems",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastRecalculatedAt",
                table: "PortfolioValueHistories");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "PortfolioValueHistories");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                table: "CashItems");
        }
    }
}
