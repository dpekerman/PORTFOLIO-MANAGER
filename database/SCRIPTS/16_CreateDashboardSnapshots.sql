/* Dashboard snapshot persistence
   Safe to run repeatedly on local SQL Server or Azure SQL.
   EF migration: 20260825142656_AddDashboardSnapshot
*/
IF OBJECT_ID(N'dbo.DashboardSnapshots', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.DashboardSnapshots (
            UserId       NVARCHAR (450) NOT NULL,
            SnapshotJson NVARCHAR (MAX) CONSTRAINT DF_DashboardSnapshots_SnapshotJson DEFAULT N'{}' NOT NULL,
            UpdatedAt    DATETIME2      CONSTRAINT DF_DashboardSnapshots_UpdatedAt DEFAULT SYSUTCDATETIME() NOT NULL,
            CONSTRAINT PK_DashboardSnapshots PRIMARY KEY (UserId)
        );
    END