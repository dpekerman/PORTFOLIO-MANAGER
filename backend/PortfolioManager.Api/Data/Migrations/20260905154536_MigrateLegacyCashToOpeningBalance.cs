using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class MigrateLegacyCashToOpeningBalance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Consolidates every existing CashItem row into exactly one OpeningBalance row per
            // (UserId, AccountType) group, dated at a fixed ledger-start date (NOT AddedAt, which
            // reflects when a mutable balance row was last touched, not a true cash-flow date).
            // Aborts the whole migration (single transaction, EF wraps Up() in one) if the pre/post
            // reconciliation doesn't match exactly to the cent, or if the OpeningBalance row count
            // doesn't equal the number of distinct account groups that had legacy rows.
            migrationBuilder.Sql(@"
IF OBJECT_ID('tempdb..#PreMigrationTotals') IS NOT NULL DROP TABLE #PreMigrationTotals;

SELECT UserId, AccountType, SUM(Amount) AS TotalAmount, COUNT(*) AS Cnt
INTO #PreMigrationTotals
FROM CashItems
GROUP BY UserId, AccountType;

DECLARE @PreGrandTotal DECIMAL(18,4) = (SELECT ISNULL(SUM(Amount), 0) FROM CashItems);
DECLARE @PreGroupCount INT = (SELECT COUNT(*) FROM #PreMigrationTotals);
DECLARE @LedgerStartDate DATE = '2026-09-05';

DELETE FROM CashItems;

INSERT INTO CashItems (UserId, Description, Amount, AddedAt, AccountType, TransactionDate, CashFlowType)
SELECT UserId, 'Opening Balance (migrated)', TotalAmount, SYSUTCDATETIME(), AccountType, @LedgerStartDate, 'OpeningBalance'
FROM #PreMigrationTotals;

INSERT INTO CashLedgerSettings (LedgerStartDate) VALUES (@LedgerStartDate);

DECLARE @PostGrandTotal DECIMAL(18,4) = (SELECT ISNULL(SUM(Amount), 0) FROM CashItems WHERE CashFlowType = 'OpeningBalance');
DECLARE @PostGroupCount INT = (SELECT COUNT(*) FROM CashItems WHERE CashFlowType = 'OpeningBalance');

IF @PostGrandTotal <> @PreGrandTotal
BEGIN
    DECLARE @msg1 NVARCHAR(400) = 'MigrateLegacyCashToOpeningBalance: grand total mismatch. Pre=' + CAST(@PreGrandTotal AS NVARCHAR(30)) + ' Post=' + CAST(@PostGrandTotal AS NVARCHAR(30));
    RAISERROR(@msg1, 16, 1);
END

IF @PostGroupCount <> @PreGroupCount
BEGIN
    DECLARE @msg2 NVARCHAR(400) = 'MigrateLegacyCashToOpeningBalance: OpeningBalance row count (' + CAST(@PostGroupCount AS NVARCHAR(10)) + ') does not match distinct account group count (' + CAST(@PreGroupCount AS NVARCHAR(10)) + ').';
    RAISERROR(@msg2, 16, 1);
END

IF EXISTS (
    SELECT 1 FROM #PreMigrationTotals pre
    LEFT JOIN CashItems post
        ON post.CashFlowType = 'OpeningBalance'
       AND ((post.AccountType = pre.AccountType) OR (post.AccountType IS NULL AND pre.AccountType IS NULL))
       AND ((post.UserId = pre.UserId) OR (post.UserId IS NULL AND pre.UserId IS NULL))
    WHERE post.Id IS NULL OR post.Amount <> pre.TotalAmount
)
BEGIN
    RAISERROR('MigrateLegacyCashToOpeningBalance: per-account reconciliation mismatch detected.', 16, 1);
END

DROP TABLE #PreMigrationTotals;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Irreversible by design: consolidating N legacy rows into 1 OpeningBalance row discards
            // the original row-level detail. Restore from the pre-migration backup instead of reverting.
        }
    }
}
