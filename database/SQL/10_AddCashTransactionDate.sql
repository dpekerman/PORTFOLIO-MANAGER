-- Migration: Add TransactionDate to CashItems table
-- Run this script after the existing 03_SeedData.sql

IF NOT EXISTS (
    SELECT 1
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'CashItems'
    AND COLUMN_NAME = 'TransactionDate'
)
BEGIN
    ALTER TABLE CashItems
    ADD TransactionDate DATETIME2 NULL;

    PRINT 'TransactionDate column added to CashItems.';
END
ELSE
BEGIN
    PRINT 'TransactionDate column already exists in CashItems — skipped.';
END
