using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPortfolioAndWatchlistSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // All four tables use IF NOT EXISTS — safe on fresh DB (Azure) and existing local DB

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'RsiScanSnapshots')
BEGIN
    CREATE TABLE [RsiScanSnapshots] (
        [Id]              INT           NOT NULL CONSTRAINT [PK_RsiScanSnapshots] PRIMARY KEY,
        [SnapshotJson]    NVARCHAR(MAX) NOT NULL CONSTRAINT [DF_RsiSnap_Json]      DEFAULT N'{}',
        [ScannedAt]       DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
        [SymbolCount]     INT           NOT NULL DEFAULT 0,
        [OversoldCount]   INT           NOT NULL DEFAULT 0,
        [OverboughtCount] INT           NOT NULL DEFAULT 0
    );
END");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'UserPreferences')
BEGIN
    CREATE TABLE [UserPreferences] (
        [Id]              INT IDENTITY(1,1) NOT NULL,
        [UserId]          NVARCHAR(450)     NOT NULL,
        [PreferenceKey]   NVARCHAR(100)     NOT NULL,
        [PreferenceValue] NVARCHAR(MAX)     NOT NULL CONSTRAINT [DF_UserPref_Value] DEFAULT N'',
        [UpdatedAt]       DATETIME2         NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_UserPreferences] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [IX_UserPreferences_UserId_PreferenceKey]
        ON [UserPreferences] ([UserId], [PreferenceKey]);
END");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PortfolioSnapshots')
BEGIN
    CREATE TABLE [PortfolioSnapshots] (
        [UserId]       NVARCHAR(450) NOT NULL CONSTRAINT [PK_PortfolioSnapshots] PRIMARY KEY,
        [SnapshotJson] NVARCHAR(MAX) NOT NULL CONSTRAINT [DF_PortSnap_Json] DEFAULT N'[]',
        [UpdatedAt]    DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
        [ItemCount]    INT           NOT NULL DEFAULT 0
    );
END");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'WatchlistSnapshots')
BEGIN
    CREATE TABLE [WatchlistSnapshots] (
        [UserId]       NVARCHAR(450) NOT NULL CONSTRAINT [PK_WatchlistSnapshots] PRIMARY KEY,
        [SnapshotJson] NVARCHAR(MAX) NOT NULL CONSTRAINT [DF_WlSnap_Json] DEFAULT N'[]',
        [UpdatedAt]    DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
        [ItemCount]    INT           NOT NULL DEFAULT 0
    );
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS [WatchlistSnapshots];");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [PortfolioSnapshots];");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [UserPreferences];");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [RsiScanSnapshots];");
        }
    }
}
