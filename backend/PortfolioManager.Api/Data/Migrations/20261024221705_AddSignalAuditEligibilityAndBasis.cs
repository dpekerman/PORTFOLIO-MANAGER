using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSignalAuditEligibilityAndBasis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BasisClose",
                table: "SignalAuditEvents",
                type: "decimal(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EligibilityStatus",
                table: "SignalAuditEvents",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Unverified");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BasisClose",
                table: "SignalAuditEvents");

            migrationBuilder.DropColumn(
                name: "EligibilityStatus",
                table: "SignalAuditEvents");
        }
    }
}
