using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── PortfolioItems: transaction tracking columns ─────────────────
            migrationBuilder.AddColumn<string>(
                name: "TransactionType",
                table: "PortfolioItems",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountType",
                table: "PortfolioItems",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OpenDate",
                table: "PortfolioItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CloseDate",
                table: "PortfolioItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ClosingPrice",
                table: "PortfolioItems",
                type: "decimal(18,4)",
                nullable: true);

            // ── OptionItems: transaction tracking columns ────────────────────
            migrationBuilder.AddColumn<string>(
                name: "TransactionType",
                table: "OptionItems",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountType",
                table: "OptionItems",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OpenDate",
                table: "OptionItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CloseDate",
                table: "OptionItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ClosingPrice",
                table: "OptionItems",
                type: "decimal(18,4)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "TransactionType", table: "PortfolioItems");
            migrationBuilder.DropColumn(name: "AccountType",     table: "PortfolioItems");
            migrationBuilder.DropColumn(name: "OpenDate",        table: "PortfolioItems");
            migrationBuilder.DropColumn(name: "CloseDate",       table: "PortfolioItems");
            migrationBuilder.DropColumn(name: "ClosingPrice",    table: "PortfolioItems");

            migrationBuilder.DropColumn(name: "TransactionType", table: "OptionItems");
            migrationBuilder.DropColumn(name: "AccountType",     table: "OptionItems");
            migrationBuilder.DropColumn(name: "OpenDate",        table: "OptionItems");
            migrationBuilder.DropColumn(name: "CloseDate",       table: "OptionItems");
            migrationBuilder.DropColumn(name: "ClosingPrice",    table: "OptionItems");
        }
    }
}
