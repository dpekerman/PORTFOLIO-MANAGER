using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDecisionSourceAndCashAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DecisionSource",
                table: "PortfolioItems",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionSource",
                table: "OptionItems",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountType",
                table: "CashItems",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "DecisionSource", table: "PortfolioItems");
            migrationBuilder.DropColumn(name: "DecisionSource", table: "OptionItems");
            migrationBuilder.DropColumn(name: "AccountType", table: "CashItems");
        }
    }
}
