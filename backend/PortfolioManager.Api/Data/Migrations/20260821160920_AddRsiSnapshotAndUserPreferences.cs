using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRsiSnapshotAndUserPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'RsiScanSnapshots')
BEGIN
    CREATE TABLE [RsiScanSnapshots] (
        [Id]             INT           NOT NULL CONSTRAINT [PK_RsiScanSnapshots] PRIMARY KEY,
        [SnapshotJson]   NVARCHAR(MAX) NOT NULL CONSTRAINT [DF_RsiSnap_Json] DEFAULT N'{}',
        [ScannedAt]      DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
        [SymbolCount]    INT           NOT NULL DEFAULT 0,
        [OversoldCount]  INT           NOT NULL DEFAULT 0,
        [OverboughtCount] INT          NOT NULL DEFAULT 0
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS [UserPreferences];");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [RsiScanSnapshots];");
        }
    }
}
