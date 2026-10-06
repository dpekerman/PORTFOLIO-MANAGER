using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalysisCurrencyToDailySignals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnalysisCurrency",
                table: "DailySignals",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnalysisTicker",
                table: "DailySignals",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            // Backfill signals of symbols currently analyzed via a US underlying (stored prices are USD).
            migrationBuilder.Sql(@"
UPDATE ds
SET ds.AnalysisTicker = m.UnderlyingTicker, ds.AnalysisCurrency = 'USD'
FROM DailySignals ds
JOIN SecurityAnalysisMappings m
  ON m.TradingTicker = ds.Symbol AND m.UseUnderlyingForAnalysis = 1 AND m.UnderlyingTicker IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnalysisCurrency",
                table: "DailySignals");

            migrationBuilder.DropColumn(
                name: "AnalysisTicker",
                table: "DailySignals");
        }
    }
}
