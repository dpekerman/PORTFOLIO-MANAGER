-- ============================================================
-- SCRIPTS/04_SeedNotificationRecipients.sql
-- Adds email addresses to the NotificationRecipients table.
-- Edit the VALUES list before running – do NOT commit real
-- email addresses to git.  This file is intentionally empty
-- of real addresses and serves as the template.
-- ============================================================

USE PortfolioManagerDb;
GO

-- Replace the placeholder addresses with real ones before executing.
MERGE [dbo].[NotificationRecipients] AS target
USING (
    VALUES
    ('your-email@example.com',  1),
    ('another-user@example.com', 1)
) AS source ([Email], [IsActive])
ON target.[Email] = source.[Email]
WHEN NOT MATCHED AND source.[IsActive] = 1 THEN
    INSERT ([Email], [IsActive], [AddedAt])
    VALUES (source.[Email], source.[IsActive], GETUTCDATE())
WHEN MATCHED THEN
    UPDATE SET [IsActive] = source.[IsActive];

PRINT 'NotificationRecipients seed complete.';
PRINT 'Rows now in table: ' + CAST((SELECT COUNT(*) FROM [dbo].[NotificationRecipients]) AS NVARCHAR);
GO
