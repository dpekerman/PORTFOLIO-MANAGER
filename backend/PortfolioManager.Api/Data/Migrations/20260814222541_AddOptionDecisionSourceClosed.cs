using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOptionDecisionSourceClosed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // OptionItems.DecisionSourceClosed � new column (safe to add idempotently)
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID('dbo.OptionItems') AND name = 'DecisionSourceClosed'
                )
                    ALTER TABLE dbo.OptionItems ADD DecisionSourceClosed nvarchar(50) NULL;
            ");

            // DailySignals extra columns � may already exist from SQL scripts; guarded
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DailySignals') AND name = 'TrendShift')
                    ALTER TABLE dbo.DailySignals ADD TrendShift nvarchar(50) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DailySignals') AND name = 'RsiDelta1D')
                    ALTER TABLE dbo.DailySignals ADD RsiDelta1D decimal(18,4) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DailySignals') AND name = 'EntryPrice')
                    ALTER TABLE dbo.DailySignals ADD EntryPrice decimal(18,4) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DailySignals') AND name = 'StopLossPrice')
                    ALTER TABLE dbo.DailySignals ADD StopLossPrice decimal(18,4) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DailySignals') AND name = 'RiskPerShare')
                    ALTER TABLE dbo.DailySignals ADD RiskPerShare decimal(18,4) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DailySignals') AND name = 'Sma200')
                    ALTER TABLE dbo.DailySignals ADD Sma200 decimal(18,4) NULL;
            ");

            // StagedSignals � may already exist from SQL script 13; guarded
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'StagedSignals')
                BEGIN
                    CREATE TABLE dbo.StagedSignals (
                        StagedId          INT IDENTITY(1,1) NOT NULL,
                        Symbol            nvarchar(20) NOT NULL,
                        ScanType          nvarchar(20) NOT NULL,
                        BasePrice         decimal(18,4) NOT NULL,
                        BaseRsi           decimal(18,4) NOT NULL,
                        BaseHigh          decimal(18,4) NOT NULL,
                        BaseLow           decimal(18,4) NOT NULL,
                        PreviousPrice     decimal(18,4) NULL,
                        PreviousRsi       decimal(18,4) NULL,
                        CurrentPrice      decimal(18,4) NULL,
                        CurrentRsi        decimal(18,4) NULL,
                        RsiDelta1D        decimal(18,4) NULL,
                        ExtremeLow        decimal(18,4) NULL,
                        ExtremeHigh       decimal(18,4) NULL,
                        StagedDate        date NOT NULL,
                        LastEvaluatedDate date NULL,
                        IsActiveWatch     bit NOT NULL DEFAULT 1,
                        CreatedAt         datetime2 NOT NULL DEFAULT SYSDATETIME(),
                        UpdatedAt         datetime2 NOT NULL DEFAULT SYSDATETIME(),
                        CONSTRAINT PK_StagedSignals PRIMARY KEY (StagedId)
                    );
                    CREATE INDEX IX_StagedSignals_Symbol ON dbo.StagedSignals (Symbol);
                    CREATE INDEX IX_StagedSignals_IsActiveWatch ON dbo.StagedSignals (IsActiveWatch);
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'StagedSignals') DROP TABLE dbo.StagedSignals;");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.OptionItems') AND name = 'DecisionSourceClosed') ALTER TABLE dbo.OptionItems DROP COLUMN DecisionSourceClosed;");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DailySignals') AND name = 'EntryPrice') ALTER TABLE dbo.DailySignals DROP COLUMN EntryPrice, RiskPerShare, RsiDelta1D, Sma200, StopLossPrice, TrendShift;");
        }
    }
}