-- REVIEW ONLY. Never run against protected data without Phase 15 schema approval.
-- No currency, side or multiplier backfill is included.
SET XACT_ABORT ON;
DECLARE @SchemaApproval bit = 0;
IF @SchemaApproval <> 1
    THROW 51000, 'Phase 15 approval and isolated rehearsal are required.', 1;
IF COL_LENGTH('dbo.CashItems', 'CurrencyCode') IS NOT NULL
    THROW 51001, 'Schema already contains currency fields; reconcile with generated EF migration.', 1;

BEGIN TRY
    BEGIN TRANSACTION;
    ALTER TABLE dbo.CashItems ADD
        ValuationAccountId nvarchar(100) NULL,
        CurrencyCode char(3) NULL,
        CurrencyVerificationStatus nvarchar(20) NULL,
        CurrencyEvidenceSource nvarchar(200) NULL,
        CurrencyVerifiedAtUtc datetime2(7) NULL;
    ALTER TABLE dbo.OptionItems ADD
        ValuationAccountId nvarchar(100) NULL,
        ContractIdentity nvarchar(100) NULL,
        CurrencyCode char(3) NULL,
        CurrencyVerificationStatus nvarchar(20) NULL,
        CurrencyEvidenceSource nvarchar(200) NULL,
        CurrencyVerifiedAtUtc datetime2(7) NULL,
        PositionSide varchar(5) NULL,
        PositionState varchar(6) NULL,
        SignedOpenContracts int NULL,
        PositionSource nvarchar(200) NULL,
        PositionAsOfUtc datetime2(7) NULL,
        ContractMultiplier decimal(18,6) NULL,
        TermsSource nvarchar(200) NULL,
        TermsVerifiedAtUtc datetime2(7) NULL,
        QuoteCurrency char(3) NULL,
        QuoteSource nvarchar(200) NULL,
        QuoteAsOfUtc datetime2(7) NULL;

    -- Dynamic SQL compiles after the nullable proposal columns exist.
    EXEC sys.sp_executesql N'
        ALTER TABLE dbo.CashItems ADD CONSTRAINT CK_CashItems_VerifiedCurrency
        CHECK (CurrencyVerificationStatus IS NULL OR CurrencyVerificationStatus <> ''Verified''
            OR (CurrencyCode IS NOT NULL AND CurrencyCode IN (''CAD'',''USD'') AND ValuationAccountId IS NOT NULL
                AND CurrencyEvidenceSource IS NOT NULL AND CurrencyVerifiedAtUtc IS NOT NULL));
        DROP INDEX IX_CashItems_Account_OpeningBalance ON dbo.CashItems;
        CREATE UNIQUE INDEX IX_CashItems_Account_OpeningBalance
            ON dbo.CashItems(AccountType, TransactionDate)
            WHERE CashFlowType = ''OpeningBalance'' AND CurrencyCode IS NULL;
        CREATE UNIQUE INDEX IX_CashItems_Currency_OpeningBalance
            ON dbo.CashItems(ValuationAccountId, CurrencyCode, TransactionDate)
            WHERE CashFlowType = ''OpeningBalance'' AND CurrencyCode IS NOT NULL;
        ALTER TABLE dbo.OptionItems ADD CONSTRAINT CK_OptionItems_PositionSide
            CHECK (PositionSide IS NULL OR PositionSide IN (''LONG'',''SHORT''));
        ALTER TABLE dbo.OptionItems ADD CONSTRAINT CK_OptionItems_Multiplier
            CHECK (ContractMultiplier IS NULL OR ContractMultiplier > 0);
        ALTER TABLE dbo.OptionItems ADD CONSTRAINT CK_OptionItems_VerifiedCurrency
        CHECK (CurrencyVerificationStatus IS NULL OR CurrencyVerificationStatus <> ''Verified''
            OR (CurrencyCode IS NOT NULL AND CurrencyCode IN (''CAD'',''USD'') AND ValuationAccountId IS NOT NULL
                AND CurrencyEvidenceSource IS NOT NULL AND CurrencyVerifiedAtUtc IS NOT NULL));
        ALTER TABLE dbo.OptionItems ADD CONSTRAINT CK_OptionItems_SignedPosition
            CHECK (SignedOpenContracts IS NULL
                OR (PositionState IS NOT NULL AND PositionState = ''Closed'' AND SignedOpenContracts = 0)
                OR (PositionState IS NOT NULL AND PositionState = ''Open'' AND PositionSide IS NOT NULL AND
                    ((PositionSide = ''LONG'' AND SignedOpenContracts > 0)
                    OR (PositionSide = ''SHORT'' AND SignedOpenContracts < 0))));
        CREATE UNIQUE INDEX IX_OptionItems_ValuationOpenContract
            ON dbo.OptionItems(ValuationAccountId, ContractIdentity)
            WHERE PositionState = ''Open'' AND ContractIdentity IS NOT NULL;
    ';
    -- Preserve IX_CashItems_TradeLink unchanged across accounts and currencies.
    CREATE TABLE dbo.ValuationCurrencyEvidence (
        Id bigint IDENTITY PRIMARY KEY,
        EntityKind varchar(10) NOT NULL,
        EntityId int NOT NULL,
        ValuationAccountId nvarchar(100) NULL,
        CurrencyCode char(3) NULL,
        VerificationStatus nvarchar(20) NOT NULL,
        EvidenceSource nvarchar(200) NOT NULL,
        EffectiveFromUtc datetime2(7) NOT NULL,
        ObservedAtUtc datetime2(7) NOT NULL,
        SupersedesEvidenceId bigint NULL REFERENCES dbo.ValuationCurrencyEvidence(Id),
        CONSTRAINT CK_ValuationEvidence_Dates CHECK (ObservedAtUtc >= EffectiveFromUtc)
    );
    COMMIT;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    THROW;
END CATCH;
