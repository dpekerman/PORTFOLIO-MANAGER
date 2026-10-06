-- Repairs schema drift when an existing AdhocAnalysisSessions table was
-- imported without the index defined by the EF model.
-- Run against the explicitly selected application database; no USE override.
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.AdhocAnalysisSessions', N'U') IS NULL
    THROW 50000, 'Apply EF migrations before repairing the analysis-session index.', 1;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.AdhocAnalysisSessions')
      AND name = N'IX_AdhocAnalysisSessions_SessionKey_UpdatedAt'
)
BEGIN
    CREATE INDEX [IX_AdhocAnalysisSessions_SessionKey_UpdatedAt]
        ON [dbo].[AdhocAnalysisSessions] ([SessionKey], [UpdatedAt]);
END;
