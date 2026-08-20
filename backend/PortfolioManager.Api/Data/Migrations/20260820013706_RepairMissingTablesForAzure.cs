using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RepairMissingTablesForAzure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // AddCashOptionAndAdhocTables (20260618010551) had an empty Up() method.
            // Base columns only — subsequent migrations add the remaining columns incrementally.
            // IF NOT EXISTS guards make this a no-op on local SQL Server.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CashItems')
BEGIN
    CREATE TABLE [CashItems] (
        [Id]          INT IDENTITY(1,1) NOT NULL,
        [Description] NVARCHAR(200)     NOT NULL CONSTRAINT [DF_CashItems_Description] DEFAULT N'CASH',
        [Amount]      DECIMAL(18,4)     NOT NULL DEFAULT 0,
        [AddedAt]     DATETIME2         NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_CashItems] PRIMARY KEY ([Id])
    );
END");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OptionItems')
BEGIN
    CREATE TABLE [OptionItems] (
        [Id]                INT IDENTITY(1,1) NOT NULL,
        [UnderlyingTicker]  NVARCHAR(20)  NOT NULL DEFAULT N'',
        [PositionType]      NVARCHAR(10)  NOT NULL DEFAULT N'',
        [ExpirationDate]    DATETIME2     NOT NULL DEFAULT '0001-01-01',
        [Strike]            DECIMAL(18,4) NOT NULL DEFAULT 0,
        [Premium]           DECIMAL(18,4) NOT NULL DEFAULT 0,
        [NumberOfContracts] INT           NOT NULL DEFAULT 0,
        [MarketPrice]       DECIMAL(18,4) NOT NULL DEFAULT 0,
        [AddedAt]           DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_OptionItems] PRIMARY KEY ([Id])
    );
END");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AdhocAnalysisSessions')
BEGIN
    CREATE TABLE [AdhocAnalysisSessions] (
        [Id]                  INT IDENTITY(1,1) NOT NULL,
        [SessionKey]          NVARCHAR(100) NOT NULL CONSTRAINT [DF_Adhoc_SessionKey]  DEFAULT N'default',
        [Symbols]             NVARCHAR(MAX) NOT NULL CONSTRAINT [DF_Adhoc_Symbols]     DEFAULT N'[]',
        [ResultsJson]         NVARCHAR(MAX) NULL,
        [OversoldThreshold]   DECIMAL(5,2)  NOT NULL CONSTRAINT [DF_Adhoc_Oversold]   DEFAULT 30,
        [OverboughtThreshold] DECIMAL(5,2)  NOT NULL CONSTRAINT [DF_Adhoc_Overbought] DEFAULT 75,
        [LogicMode]           NVARCHAR(20)  NOT NULL CONSTRAINT [DF_Adhoc_LogicMode]  DEFAULT N'Legacy',
        [CreatedAt]           DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAt]           DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_AdhocAnalysisSessions] PRIMARY KEY ([Id])
    );
    CREATE INDEX [IX_AdhocAnalysisSessions_SessionKey_UpdatedAt]
        ON [AdhocAnalysisSessions] ([SessionKey], [UpdatedAt]);
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS [AdhocAnalysisSessions];");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [OptionItems];");
            migrationBuilder.Sql("DROP TABLE IF EXISTS [CashItems];");
        }
    }
}

