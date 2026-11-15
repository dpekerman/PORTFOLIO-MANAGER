using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Phase15Verification.Migrations
{
    /// <inheritdoc />
    public partial class Phase15CurrencyProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CashItems_Account_OpeningBalance",
                table: "CashItems");

            migrationBuilder.AddColumn<string>(
                name: "ContractIdentity",
                table: "OptionItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ContractMultiplier",
                table: "OptionItems",
                type: "decimal(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "OptionItems",
                type: "char(3)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrencyEvidenceSource",
                table: "OptionItems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrencyVerificationStatus",
                table: "OptionItems",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CurrencyVerifiedAtUtc",
                table: "OptionItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PositionAsOfUtc",
                table: "OptionItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PositionSide",
                table: "OptionItems",
                type: "nvarchar(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PositionSource",
                table: "OptionItems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PositionState",
                table: "OptionItems",
                type: "nvarchar(6)",
                maxLength: 6,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "QuoteAsOfUtc",
                table: "OptionItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QuoteCurrency",
                table: "OptionItems",
                type: "char(3)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QuoteSource",
                table: "OptionItems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SignedOpenContracts",
                table: "OptionItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TermsSource",
                table: "OptionItems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TermsVerifiedAtUtc",
                table: "OptionItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValuationAccountId",
                table: "OptionItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "CashItems",
                type: "char(3)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrencyEvidenceSource",
                table: "CashItems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrencyVerificationStatus",
                table: "CashItems",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CurrencyVerifiedAtUtc",
                table: "CashItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValuationAccountId",
                table: "CashItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ValuationCurrencyEvidence",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityKind = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    EntityId = table.Column<int>(type: "int", nullable: false),
                    ValuationAccountId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CurrencyCode = table.Column<string>(type: "char(3)", nullable: true),
                    VerificationStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EvidenceSource = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ObservedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SupersedesEvidenceId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValuationCurrencyEvidence", x => x.Id);
                    table.CheckConstraint("CK_ValuationEvidence_Dates", "[ObservedAtUtc] >= [EffectiveFromUtc]");
                    table.CheckConstraint("CK_ValuationEvidence_EntityKind", "[EntityKind] IN ('Cash','Option')");
                    table.CheckConstraint("CK_ValuationEvidence_VerifiedCurrency", "[VerificationStatus] <> 'Verified' OR ([CurrencyCode] IS NOT NULL AND [CurrencyCode] IN ('CAD','USD') AND [ValuationAccountId] IS NOT NULL AND LTRIM(RTRIM([ValuationAccountId])) <> '' AND LTRIM(RTRIM([EvidenceSource])) <> '')");
                    table.ForeignKey(
                        name: "FK_ValuationCurrencyEvidence_ValuationCurrencyEvidence_SupersedesEvidenceId",
                        column: x => x.SupersedesEvidenceId,
                        principalTable: "ValuationCurrencyEvidence",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OptionItems_ValuationOpenContract",
                table: "OptionItems",
                columns: new[] { "UserId", "ValuationAccountId", "ContractIdentity" },
                unique: true,
                filter: "[PositionState] = 'Open' AND [ContractIdentity] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OptionItems_Multiplier",
                table: "OptionItems",
                sql: "[ContractMultiplier] IS NULL OR [ContractMultiplier] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OptionItems_PositionSide",
                table: "OptionItems",
                sql: "[PositionSide] IS NULL OR [PositionSide] IN ('LONG','SHORT')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OptionItems_PositionState",
                table: "OptionItems",
                sql: "[PositionState] IS NULL OR [PositionState] IN ('Open','Closed')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OptionItems_QuoteCurrency",
                table: "OptionItems",
                sql: "[QuoteCurrency] IS NULL OR ([CurrencyCode] IS NOT NULL AND [QuoteCurrency] = [CurrencyCode])");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OptionItems_SignedPosition",
                table: "OptionItems",
                sql: "[SignedOpenContracts] IS NULL OR ([PositionState] IS NOT NULL AND (([PositionState] = 'Closed' AND [SignedOpenContracts] = 0) OR ([PositionState] = 'Open' AND [PositionSide] IS NOT NULL AND (([PositionSide] = 'LONG' AND [SignedOpenContracts] > 0) OR ([PositionSide] = 'SHORT' AND [SignedOpenContracts] < 0)))))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OptionItems_VerifiedCurrency",
                table: "OptionItems",
                sql: "[CurrencyVerificationStatus] IS NULL OR [CurrencyVerificationStatus] <> 'Verified' OR ([CurrencyCode] IS NOT NULL AND [CurrencyCode] IN ('CAD','USD') AND [ValuationAccountId] IS NOT NULL AND LTRIM(RTRIM([ValuationAccountId])) <> '' AND [CurrencyEvidenceSource] IS NOT NULL AND LTRIM(RTRIM([CurrencyEvidenceSource])) <> '' AND [CurrencyVerifiedAtUtc] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_CashItems_Account_OpeningBalance",
                table: "CashItems",
                columns: new[] { "AccountType", "TransactionDate" },
                unique: true,
                filter: "[CashFlowType] = 'OpeningBalance' AND [CurrencyCode] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CashItems_Currency_OpeningBalance",
                table: "CashItems",
                columns: new[] { "UserId", "ValuationAccountId", "CurrencyCode", "TransactionDate" },
                unique: true,
                filter: "[CashFlowType] = 'OpeningBalance' AND [CurrencyCode] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CashItems_VerifiedCurrency",
                table: "CashItems",
                sql: "[CurrencyVerificationStatus] IS NULL OR [CurrencyVerificationStatus] <> 'Verified' OR ([CurrencyCode] IS NOT NULL AND [CurrencyCode] IN ('CAD','USD') AND [ValuationAccountId] IS NOT NULL AND LTRIM(RTRIM([ValuationAccountId])) <> '' AND [CurrencyEvidenceSource] IS NOT NULL AND LTRIM(RTRIM([CurrencyEvidenceSource])) <> '' AND [CurrencyVerifiedAtUtc] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_ValuationCurrencyEvidence_EntityKind_EntityId_EffectiveFromUtc",
                table: "ValuationCurrencyEvidence",
                columns: new[] { "EntityKind", "EntityId", "EffectiveFromUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ValuationCurrencyEvidence_SupersedesEvidenceId",
                table: "ValuationCurrencyEvidence",
                column: "SupersedesEvidenceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ValuationCurrencyEvidence");

            migrationBuilder.DropIndex(
                name: "IX_OptionItems_ValuationOpenContract",
                table: "OptionItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OptionItems_Multiplier",
                table: "OptionItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OptionItems_PositionSide",
                table: "OptionItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OptionItems_PositionState",
                table: "OptionItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OptionItems_QuoteCurrency",
                table: "OptionItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OptionItems_SignedPosition",
                table: "OptionItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OptionItems_VerifiedCurrency",
                table: "OptionItems");

            migrationBuilder.DropIndex(
                name: "IX_CashItems_Account_OpeningBalance",
                table: "CashItems");

            migrationBuilder.DropIndex(
                name: "IX_CashItems_Currency_OpeningBalance",
                table: "CashItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CashItems_VerifiedCurrency",
                table: "CashItems");

            migrationBuilder.DropColumn(
                name: "ContractIdentity",
                table: "OptionItems");

            migrationBuilder.DropColumn(
                name: "ContractMultiplier",
                table: "OptionItems");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "OptionItems");

            migrationBuilder.DropColumn(
                name: "CurrencyEvidenceSource",
                table: "OptionItems");

            migrationBuilder.DropColumn(
                name: "CurrencyVerificationStatus",
                table: "OptionItems");

            migrationBuilder.DropColumn(
                name: "CurrencyVerifiedAtUtc",
                table: "OptionItems");

            migrationBuilder.DropColumn(
                name: "PositionAsOfUtc",
                table: "OptionItems");

            migrationBuilder.DropColumn(
                name: "PositionSide",
                table: "OptionItems");

            migrationBuilder.DropColumn(
                name: "PositionSource",
                table: "OptionItems");

            migrationBuilder.DropColumn(
                name: "PositionState",
                table: "OptionItems");

            migrationBuilder.DropColumn(
                name: "QuoteAsOfUtc",
                table: "OptionItems");

            migrationBuilder.DropColumn(
                name: "QuoteCurrency",
                table: "OptionItems");

            migrationBuilder.DropColumn(
                name: "QuoteSource",
                table: "OptionItems");

            migrationBuilder.DropColumn(
                name: "SignedOpenContracts",
                table: "OptionItems");

            migrationBuilder.DropColumn(
                name: "TermsSource",
                table: "OptionItems");

            migrationBuilder.DropColumn(
                name: "TermsVerifiedAtUtc",
                table: "OptionItems");

            migrationBuilder.DropColumn(
                name: "ValuationAccountId",
                table: "OptionItems");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "CashItems");

            migrationBuilder.DropColumn(
                name: "CurrencyEvidenceSource",
                table: "CashItems");

            migrationBuilder.DropColumn(
                name: "CurrencyVerificationStatus",
                table: "CashItems");

            migrationBuilder.DropColumn(
                name: "CurrencyVerifiedAtUtc",
                table: "CashItems");

            migrationBuilder.DropColumn(
                name: "ValuationAccountId",
                table: "CashItems");

            migrationBuilder.CreateIndex(
                name: "IX_CashItems_Account_OpeningBalance",
                table: "CashItems",
                columns: new[] { "AccountType", "TransactionDate" },
                unique: true,
                filter: "[CashFlowType] = 'OpeningBalance'");
        }
    }
}
