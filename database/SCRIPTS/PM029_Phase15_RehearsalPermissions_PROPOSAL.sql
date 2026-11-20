-- REVIEW ONLY. Requires a separately approved disposable database and restricted test role.
IF DB_NAME() <> 'PM029_Phase15_Disposable'
    THROW 51017, 'Permissions may only be reviewed/applied in the designated disposable database.', 1;
IF DATABASE_PRINCIPAL_ID('PM029_RehearsalWriter') IS NULL
    THROW 51018, 'A separately provisioned restricted rehearsal role is required.', 1;
IF OBJECT_ID('dbo.ValuationCurrencyEvidence', 'U') IS NULL
    THROW 51019, 'Apply the reviewed isolated EF migration before configuring append-only permissions.', 1;

DENY UPDATE, DELETE ON dbo.ValuationCurrencyEvidence TO PM029_RehearsalWriter;
GRANT SELECT, INSERT ON dbo.ValuationCurrencyEvidence TO PM029_RehearsalWriter;
