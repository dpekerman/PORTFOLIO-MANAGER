-- ============================================================
-- SCRIPTS/11_AddIdentityAndAuth.sql
-- Adds ASP.NET Core Identity tables + RefreshTokens to an
-- existing PortfolioManagerDb database.
-- Safe to re-run (all statements guarded by IF NOT EXISTS).
-- Run AFTER: 01_CreateDatabase.sql + 02_CreateTables.sql
-- ============================================================

USE PortfolioManagerDb;
GO

-- ── AspNetRoles ──────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AspNetRoles')
BEGIN
    CREATE TABLE [dbo].[AspNetRoles] (
        [Id]               NVARCHAR(450)  NOT NULL,
        [Name]             NVARCHAR(256)  NULL,
        [NormalizedName]   NVARCHAR(256)  NULL,
        [ConcurrencyStamp] NVARCHAR(MAX)  NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [RoleNameIndex] ON [dbo].[AspNetRoles] ([NormalizedName])
        WHERE [NormalizedName] IS NOT NULL;
    PRINT 'Created table: AspNetRoles';
END
GO

-- ── AspNetUsers ──────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AspNetUsers')
BEGIN
    CREATE TABLE [dbo].[AspNetUsers] (
        [Id]                   NVARCHAR(450)    NOT NULL,
        [DisplayName]          NVARCHAR(MAX)    NOT NULL DEFAULT '',
        [CreatedAt]            DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        [UserName]             NVARCHAR(256)    NULL,
        [NormalizedUserName]   NVARCHAR(256)    NULL,
        [Email]                NVARCHAR(256)    NULL,
        [NormalizedEmail]      NVARCHAR(256)    NULL,
        [EmailConfirmed]       BIT              NOT NULL DEFAULT 0,
        [PasswordHash]         NVARCHAR(MAX)    NULL,
        [SecurityStamp]        NVARCHAR(MAX)    NULL,
        [ConcurrencyStamp]     NVARCHAR(MAX)    NULL,
        [PhoneNumber]          NVARCHAR(MAX)    NULL,
        [PhoneNumberConfirmed] BIT              NOT NULL DEFAULT 0,
        [TwoFactorEnabled]     BIT              NOT NULL DEFAULT 0,
        [LockoutEnd]           DATETIMEOFFSET   NULL,
        [LockoutEnabled]       BIT              NOT NULL DEFAULT 1,
        [AccessFailedCount]    INT              NOT NULL DEFAULT 0,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
    CREATE INDEX [EmailIndex]     ON [dbo].[AspNetUsers] ([NormalizedEmail]);
    CREATE UNIQUE INDEX [UserNameIndex] ON [dbo].[AspNetUsers] ([NormalizedUserName])
        WHERE [NormalizedUserName] IS NOT NULL;
    PRINT 'Created table: AspNetUsers';
END
GO

-- ── AspNetRoleClaims ─────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AspNetRoleClaims')
BEGIN
    CREATE TABLE [dbo].[AspNetRoleClaims] (
        [Id]         INT           NOT NULL IDENTITY(1,1),
        [RoleId]     NVARCHAR(450) NOT NULL,
        [ClaimType]  NVARCHAR(MAX) NULL,
        [ClaimValue] NVARCHAR(MAX) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles] FOREIGN KEY ([RoleId])
            REFERENCES [dbo].[AspNetRoles] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [dbo].[AspNetRoleClaims] ([RoleId]);
    PRINT 'Created table: AspNetRoleClaims';
END
GO

-- ── AspNetUserClaims ─────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AspNetUserClaims')
BEGIN
    CREATE TABLE [dbo].[AspNetUserClaims] (
        [Id]         INT           NOT NULL IDENTITY(1,1),
        [UserId]     NVARCHAR(450) NOT NULL,
        [ClaimType]  NVARCHAR(MAX) NULL,
        [ClaimValue] NVARCHAR(MAX) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers] FOREIGN KEY ([UserId])
            REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [dbo].[AspNetUserClaims] ([UserId]);
    PRINT 'Created table: AspNetUserClaims';
END
GO

-- ── AspNetUserLogins ─────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AspNetUserLogins')
BEGIN
    CREATE TABLE [dbo].[AspNetUserLogins] (
        [LoginProvider]       NVARCHAR(450) NOT NULL,
        [ProviderKey]         NVARCHAR(450) NOT NULL,
        [ProviderDisplayName] NVARCHAR(MAX) NULL,
        [UserId]              NVARCHAR(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers] FOREIGN KEY ([UserId])
            REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [dbo].[AspNetUserLogins] ([UserId]);
    PRINT 'Created table: AspNetUserLogins';
END
GO

-- ── AspNetUserRoles ──────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AspNetUserRoles')
BEGIN
    CREATE TABLE [dbo].[AspNetUserRoles] (
        [UserId] NVARCHAR(450) NOT NULL,
        [RoleId] NVARCHAR(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles] FOREIGN KEY ([RoleId])
            REFERENCES [dbo].[AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers] FOREIGN KEY ([UserId])
            REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [dbo].[AspNetUserRoles] ([RoleId]);
    PRINT 'Created table: AspNetUserRoles';
END
GO

-- ── AspNetUserTokens ─────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AspNetUserTokens')
BEGIN
    CREATE TABLE [dbo].[AspNetUserTokens] (
        [UserId]        NVARCHAR(450) NOT NULL,
        [LoginProvider] NVARCHAR(450) NOT NULL,
        [Name]          NVARCHAR(450) NOT NULL,
        [Value]         NVARCHAR(MAX) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers] FOREIGN KEY ([UserId])
            REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
    );
    PRINT 'Created table: AspNetUserTokens';
END
GO

-- ── RefreshTokens ────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'RefreshTokens')
BEGIN
    CREATE TABLE [dbo].[RefreshTokens] (
        [Id]        INT            NOT NULL IDENTITY(1,1),
        [Token]     NVARCHAR(64)   NOT NULL,   -- SHA-256 hex of the raw cookie value
        [UserId]    NVARCHAR(450)  NOT NULL,
        [ExpiresAt] DATETIME2      NOT NULL,
        [IsRevoked] BIT            NOT NULL DEFAULT 0,
        [CreatedAt] DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RefreshTokens_AspNetUsers] FOREIGN KEY ([UserId])
            REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX [IX_RefreshTokens_Token]  ON [dbo].[RefreshTokens] ([Token]);
    CREATE INDEX        [IX_RefreshTokens_UserId] ON [dbo].[RefreshTokens] ([UserId]);
    PRINT 'Created table: RefreshTokens';
END
GO

-- ── Record migration in EF history ───────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = '20260802231757_AddIdentityAndRefreshTokens'
)
BEGIN
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES ('20260802231757_AddIdentityAndRefreshTokens', '8.0.10');
    PRINT 'EF migration history record inserted.';
END
GO

PRINT 'Script 11 complete: Identity + Auth tables verified/created.';
GO
