-- ============================================================
-- 02_CreateTables.sql
-- Creates all tables required by EF Core migrations.
-- Safe to re-run (uses IF NOT EXISTS guards).
-- ============================================================

USE PortfolioManagerDb;
GO

-- ── PortfolioItems ────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.objects
    WHERE object_id = OBJECT_ID(N'[dbo].[PortfolioItems]')
      AND type IN (N'U')
)
BEGIN
    CREATE TABLE [dbo].[PortfolioItems] (
        [Id]               INT IDENTITY(1,1)    NOT NULL,
        [Symbol]           NVARCHAR(20)         NOT NULL,
        [CompanyName]      NVARCHAR(200)        NOT NULL,
        [Shares]           DECIMAL(18,6)        NOT NULL,
        [AverageCostBasis] DECIMAL(18,4)        NOT NULL,
        [AddedAt]          DATETIME2            NOT NULL DEFAULT GETUTCDATE(),

        CONSTRAINT [PK_PortfolioItems] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    PRINT 'Table PortfolioItems created.';
END
ELSE
BEGIN
    PRINT 'Table PortfolioItems already exists - skipping.';
END
GO

-- ── Unique index on Symbol ─────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_PortfolioItems_Symbol'
      AND object_id = OBJECT_ID(N'[dbo].[PortfolioItems]')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_PortfolioItems_Symbol]
        ON [dbo].[PortfolioItems] ([Symbol] ASC);
    PRINT 'Index IX_PortfolioItems_Symbol created.';
END
ELSE
BEGIN
    PRINT 'Index IX_PortfolioItems_Symbol already exists - skipping.';
END
GO

-- ── EF Core Migrations History table ──────────────────────────
-- EF creates this automatically on first MigrateAsync(), but we
-- include it here for completeness / manual setup scenarios.
IF NOT EXISTS (
    SELECT 1 FROM sys.objects
    WHERE object_id = OBJECT_ID(N'[dbo].[__EFMigrationsHistory]')
      AND type IN (N'U')
)
BEGIN
    CREATE TABLE [dbo].[__EFMigrationsHistory] (
        [MigrationId]    NVARCHAR(150) NOT NULL,
        [ProductVersion] NVARCHAR(32)  NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
    PRINT 'Table __EFMigrationsHistory created.';
END
GO
