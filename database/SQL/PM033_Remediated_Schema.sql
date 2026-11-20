-- PM-033 AFTER CORRECTIVE MIGRATION: schema reference. Structurally identical to the live PortfolioManagerLocal schema after the user-approved migration.
-- Generated schema-only by SMO. No financial records, credentials, users or logins.
-- PortfolioManagerLocal received the corrective migration (user-approved, applied 2026-10-28); live schema verified identical.
-- Source catalog: PortfolioManager_PM033Test_20261028-173638 on localhost.
-- Captured UTC: 2026-10-28T21:57:15.1974830Z
-- Database settings, files, permissions and server objects are not included.
-- EF ledger (metadata only):
-- 20260611201226_InitialCreate | EF 8.0.26
-- 20260611235305_AddWatchlistAndSector | EF 8.0.10
-- 20260612030112_AddManualPosition | EF 8.0.10
-- 20260615120000_AddSectorOverride | EF 8.0.10
-- 20260618010551_AddCashOptionAndAdhocTables | EF 8.0.10
-- 20260619000000_AddTransactionFields | EF 8.0.10
-- 20260619000002_RemovePortfolioSymbolUniqueConstraint | EF 8.0.10
-- 20260622183326_AddRoleAndHoldingRole | EF 8.0.10
-- 20260623153856_AddNotesFields | EF 8.0.10
-- 20260624000000_AddDailySignals | EF 8.0.10
-- 20260707000001_AddWatchlistFavorite | EF 8.0.10
-- 20260708000002_AddDecisionSourceAndCashAccount | EF 8.0.10
-- 20260708000003_AddAllocationRiskTables | EF 8.0.10
-- 20260712000001_AddValueScreenerPersistence | EF 8.0.10
-- 20260719000001_AddPortfolioValueHistory | EF 8.0.10
-- 20260729000001_AddCashTransactionDate | EF 8.0.10
-- 20260802231757_AddIdentityAndRefreshTokens | EF 8.0.10
-- 20260803141906_AddUserIdToPrivateData | EF 8.0.10
-- 20260811000001_AddDecisionSourceClosed | EF 8.0.10
-- 20260814222541_AddOptionDecisionSourceClosed | EF 8.0.10
-- 20260815005554_AddEma9AtEntryToDailySignals | EF 8.0.10
-- 20260817202407_AddFibonacciToDailySignals | EF 8.0.10
-- 20260820013706_RepairMissingTablesForAzure | EF 8.0.10
-- 20260821160920_AddRsiSnapshotAndUserPreferences | EF 8.0.10
-- 20260822152539_AddPortfolioAndWatchlistSnapshots | EF 8.0.10
-- 20260824202223_AddSectorIndustryConfig | EF 8.0.10
-- 20260825142656_AddDashboardSnapshot | EF 8.0.10
-- 20260825154106_AddWatchlistEarningsDate | EF 8.0.10
-- 20260825160909_AddEodPositionSizing | EF 8.0.10
-- 20260827004100_FullFeatureSet | EF 8.0.10
-- 20260828201934_AddTechnicalChannels | EF 8.0.10
-- 20260828220959_AddChannelTouchDiagnostics | EF 8.0.10
-- 20260830155509_AddMarketLeadershipTrackers | EF 8.0.10
-- 20260901174524_AddEodTradingDateAndScanTimestamp | EF 8.0.10
-- 20260901201225_FixMarketLeadershipSoftDeleteIndex | EF 8.0.10
-- 20260901214714_AddSecurityAnalysisMappings | EF 8.0.10
-- 20260905154443_AddCashFlowType | EF 8.0.10
-- 20260905154536_MigrateLegacyCashToOpeningBalance | EF 8.0.10
-- 20260905184430_AddSnapshotAuditAndCashModifiedAt | EF 8.0.10
-- 20260907234053_AddAutomationRunLog | EF 8.0.10
-- 20260909180043_AddAutomationNotificationRecords | EF 8.0.10
-- 20260909233823_AddAutomationRunDiagnostics | EF 8.0.10
-- 20260929151645_AddCashSourceLink | EF 8.0.10
-- 20261002140402_AddAnalysisCurrencyToDailySignals | EF 8.0.10
-- 20261006143817_AddAccountTypes | EF 8.0.10
-- 20261006150738_WidenAccountTypeNames | EF 8.0.10
-- 20261008132445_AddUnlinkedTradeBaselines | EF 8.0.10
-- 20261015173504_AddAuditLog | EF 8.0.10
-- 20261023210946_AddPreMarketAgent | EF 8.0.10
-- 20261023221055_AddSecuritySectorCache | EF 8.0.10
-- 20261024210430_AddSignalAuditEvents | EF 8.0.10
-- 20261024221705_AddSignalAuditEligibilityAndBasis | EF 8.0.10
-- 20261026141747_AddRecommendationLifecycle | EF 8.0.10
-- 20261028145833_AddTradeEpisodesAndRiskTracking | EF 8.0.10
-- 20261028213913_CorrectPm033EpisodeIntegrity | EF 8.0.10

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AllocationSectorTargets](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Sector] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[TargetPct] [decimal](5, 2) NOT NULL,
	[DisplayOrder] [int] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AllocationRiskTargets](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Role] [nvarchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[TargetPct] [decimal](5, 2) NOT NULL,
	[DisplayOrder] [int] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AdhocAnalysisSessions](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[SessionKey] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Symbols] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ResultsJson] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[OversoldThreshold] [decimal](5, 2) NOT NULL,
	[OverboughtThreshold] [decimal](5, 2) NOT NULL,
	[LogicMode] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AccountTypes](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](120) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[NormalizedName] [nvarchar](120) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Version] [uniqueidentifier] NOT NULL,
 CONSTRAINT [PK_AccountTypes] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[__EFMigrationsHistory](
	[MigrationId] [nvarchar](150) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ProductVersion] [nvarchar](32) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
 CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY CLUSTERED 
(
	[MigrationId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AspNetRoles](
	[Id] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Name] [nvarchar](256) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[NormalizedName] [nvarchar](256) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[ConcurrencyStamp] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
 CONSTRAINT [PK_AspNetRoles] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AspNetUsers](
	[Id] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[DisplayName] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UserName] [nvarchar](256) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[NormalizedUserName] [nvarchar](256) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Email] [nvarchar](256) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[NormalizedEmail] [nvarchar](256) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[EmailConfirmed] [bit] NOT NULL,
	[PasswordHash] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[SecurityStamp] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[ConcurrencyStamp] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[PhoneNumber] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[PhoneNumberConfirmed] [bit] NOT NULL,
	[TwoFactorEnabled] [bit] NOT NULL,
	[LockoutEnd] [datetimeoffset](7) NULL,
	[LockoutEnabled] [bit] NOT NULL,
	[AccessFailedCount] [int] NOT NULL,
 CONSTRAINT [PK_AspNetUsers] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PreMarketFeedbacks](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[RunId] [int] NOT NULL,
	[Symbol] [nvarchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Rating] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Comment] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[CreatedUtc] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_PreMarketFeedbacks] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PreMarketAgentRuns](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[RunKey] [nvarchar](40) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[AsOfDate] [date] NOT NULL,
	[IsTestRun] [bit] NOT NULL,
	[Status] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[StartedAtUtc] [datetime2](7) NOT NULL,
	[CompletedAtUtc] [datetime2](7) NULL,
	[DurationMs] [int] NOT NULL,
	[ReportJson] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[ReportHtml] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[EmailStatus] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[EmailMessage] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[EmailSubmittedAtUtc] [datetime2](7) NULL,
	[Error] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[AiModel] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[AiCalls] [int] NOT NULL,
	[AiDurationMs] [int] NOT NULL,
	[NewsDurationMs] [int] NOT NULL,
	[ArticlesRetrieved] [int] NOT NULL,
 CONSTRAINT [PK_PreMarketAgentRuns] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PreMarketAgentConfigs](
	[Id] [int] NOT NULL,
	[ScheduleEnabled] [bit] NOT NULL,
	[StartTimeLocal] [nvarchar](5) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[FirstRunDate] [date] NULL,
	[AsOfDate] [date] NULL,
	[DeadlineLocal] [nvarchar](5) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ShadowMode] [bit] NOT NULL,
	[UserId] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[DiscoveryMode] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[AiEnabled] [bit] NOT NULL,
	[AiMaxCalls] [int] NOT NULL,
 CONSTRAINT [PK_PreMarketAgentConfigs] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PortfolioValueHistories](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[RecordedAt] [datetime2](7) NOT NULL,
	[RecordedDate] [nvarchar](10) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[TotalValue] [decimal](18, 4) NOT NULL,
	[StocksValue] [decimal](18, 4) NOT NULL,
	[CashValue] [decimal](18, 4) NOT NULL,
	[OptionsValue] [decimal](18, 4) NOT NULL,
	[LastRecalculatedAt] [datetime2](7) NULL,
	[Source] [int] NOT NULL,
 CONSTRAINT [PK_PortfolioValueHistories] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PortfolioSnapshots](
	[UserId] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[SnapshotJson] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[UpdatedAt] [datetime2](7) NOT NULL,
	[ItemCount] [int] NOT NULL,
 CONSTRAINT [PK_PortfolioSnapshots] PRIMARY KEY CLUSTERED 
(
	[UserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PortfolioItems](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Symbol] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[CompanyName] [nvarchar](200) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Shares] [decimal](18, 6) NOT NULL,
	[AverageCostBasis] [decimal](18, 4) NOT NULL,
	[AddedAt] [datetime2](7) NOT NULL,
	[Industry] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Sector] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[IsManual] [bit] NOT NULL,
	[ManualMarketValue] [decimal](18, 4) NULL,
	[SectorIsOverridden] [bit] NOT NULL,
	[TransactionType] [nvarchar](10) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[AccountType] [nvarchar](120) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[OpenDate] [datetime2](7) NULL,
	[CloseDate] [datetime2](7) NULL,
	[ClosingPrice] [decimal](18, 4) NULL,
	[HoldingRole] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Notes] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[DecisionSource] [nvarchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[UserId] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[DecisionSourceClosed] [nvarchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[TradeEpisodeId] [uniqueidentifier] NULL,
 CONSTRAINT [PK_PortfolioItems] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[OptionItems](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[UnderlyingTicker] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[PositionType] [nvarchar](10) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ExpirationDate] [datetime2](7) NOT NULL,
	[Strike] [decimal](18, 4) NOT NULL,
	[Premium] [decimal](18, 4) NOT NULL,
	[NumberOfContracts] [int] NOT NULL,
	[MarketPrice] [decimal](18, 4) NOT NULL,
	[AddedAt] [datetime2](7) NOT NULL,
	[TransactionType] [nvarchar](10) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[AccountType] [nvarchar](120) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[OpenDate] [datetime2](7) NULL,
	[CloseDate] [datetime2](7) NULL,
	[ClosingPrice] [decimal](18, 4) NULL,
	[Notes] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[DecisionSource] [nvarchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[UserId] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[DecisionSourceClosed] [nvarchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[NewsSources](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](120) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Url] [nvarchar](2048) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ProviderType] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Category] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Priority] [int] NOT NULL,
	[Enabled] [bit] NOT NULL,
	[ApprovalStatus] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Markets] [nvarchar](60) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[RefreshMinutes] [int] NOT NULL,
	[TimeoutSeconds] [int] NOT NULL,
	[MaxItems] [int] NOT NULL,
	[IsArchived] [bit] NOT NULL,
	[LastSuccessUtc] [datetime2](7) NULL,
	[LastFailureUtc] [datetime2](7) NULL,
	[LastError] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[AvgDurationMs] [int] NOT NULL,
	[UsefulArticleCount] [int] NOT NULL,
 CONSTRAINT [PK_NewsSources] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[NewsArticles](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Fingerprint] [nvarchar](64) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Url] [nvarchar](2048) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Headline] [nvarchar](400) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Publisher] [nvarchar](160) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[PublishedUtc] [datetime2](7) NOT NULL,
	[RetrievedUtc] [datetime2](7) NOT NULL,
	[Symbol] [nvarchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Category] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Summary] [nvarchar](600) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[SourceId] [int] NULL,
 CONSTRAINT [PK_NewsArticles] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[MarketLeadershipTrackers](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[UserId] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Symbol] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[DisplayName] [nvarchar](200) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[TrackerType] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[SortOrder] [int] NOT NULL,
	[IsActive] [bit] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_MarketLeadershipTrackers] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[DashboardSnapshots](
	[UserId] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[SnapshotJson] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[UpdatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_DashboardSnapshots] PRIMARY KEY CLUSTERED 
(
	[UserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[DailySignals](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Symbol] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[CompanyName] [nvarchar](200) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ScanType] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[SignalType] [nvarchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Rsi] [decimal](7, 4) NOT NULL,
	[Price] [decimal](18, 4) NOT NULL,
	[TriggerDetails] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[SignalDate] [nvarchar](10) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[RecordedAt] [datetime2](7) NOT NULL,
	[RuleVersion] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[SignalState] [nvarchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Sector] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ReversalProbability] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[VolumeSignal] [nvarchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Notes] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[UpdatedAt] [datetime2](7) NULL,
	[TrendShift] [varchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[RsiDelta1D] [decimal](18, 4) NULL,
	[EntryPrice] [decimal](18, 4) NULL,
	[StopLossPrice] [decimal](18, 4) NULL,
	[RiskPerShare] [decimal](18, 4) NULL,
	[Sma200] [decimal](18, 4) NULL,
	[Ema9AtEntry] [decimal](18, 4) NULL,
	[Ema9ConfirmedAtEntry] [bit] NULL,
	[Fib61_8AtSignal] [decimal](18, 4) NULL,
	[FibStatusAtSignal] [nvarchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[FibZoneAtSignal] [nvarchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[PositionSizingLimitingReason] [nvarchar](200) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[PositionSizingPositionValue] [decimal](18, 4) NULL,
	[PositionSizingRiskAmount] [decimal](18, 4) NULL,
	[PositionSizingShares] [decimal](18, 6) NULL,
	[PreviousSignalState] [nvarchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[ScannedAt] [datetime2](7) NULL,
	[TradingDate] [nvarchar](10) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[AnalysisCurrency] [nvarchar](10) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[AnalysisTicker] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
 CONSTRAINT [PK_DailySignals] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[CashLedgerSettings](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[LedgerStartDate] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_CashLedgerSettings] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[CashItems](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Description] [nvarchar](200) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Amount] [decimal](18, 4) NOT NULL,
	[AddedAt] [datetime2](7) NOT NULL,
	[AccountType] [nvarchar](120) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[TransactionDate] [datetime2](7) NULL,
	[UserId] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[CashFlowType] [nvarchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[ModifiedAt] [datetime2](7) NULL,
	[SourceItemId] [int] NULL,
	[SourceType] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[BrokerStopRecords](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[OrderIdentity] [uniqueidentifier] NOT NULL,
	[ImportKey] [nvarchar](200) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[UserId] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[AccountType] [nvarchar](120) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Symbol] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[OrderType] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[TriggerPrice] [decimal](18, 4) NOT NULL,
	[LimitPrice] [decimal](18, 4) NULL,
	[Currency] [nvarchar](3) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[UserStatus] [nvarchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[BrokerVerificationStatus] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[CoveredQuantity] [decimal](18, 6) NULL,
	[PlacedDate] [datetime2](7) NULL,
	[BrokerOrderRef] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[LastUserVerifiedDate] [datetime2](7) NULL,
	[ConfirmationSource] [nvarchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Notes] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[TradeEpisodeId] [uniqueidentifier] NULL,
	[EpisodeOpenDate] [datetime2](7) NULL,
	[ReplacedByRecordId] [int] NULL,
	[BrokerCancellationUnverified] [bit] NOT NULL,
	[CreatedAtUtc] [datetime2](7) NOT NULL,
	[UpdatedAtUtc] [datetime2](7) NOT NULL,
	[RowVersion] [timestamp] NULL,
 CONSTRAINT [PK_BrokerStopRecords] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AutomationRunLogs](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[RunId] [uniqueidentifier] NOT NULL,
	[TradingDate] [nvarchar](10) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[TriggerType] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ScheduledStartUtc] [datetime2](7) NULL,
	[ActualStartUtc] [datetime2](7) NOT NULL,
	[CompletedAtUtc] [datetime2](7) NULL,
	[OverallStatus] [nvarchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[OwnerUserId] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[RefreshStatus] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[RefreshStartedAtUtc] [datetime2](7) NULL,
	[RefreshCompletedAtUtc] [datetime2](7) NULL,
	[PortfolioSymbolCount] [int] NOT NULL,
	[WatchlistSymbolCount] [int] NOT NULL,
	[RsiStatus] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[RsiCompletedAtUtc] [datetime2](7) NULL,
	[EodSignalsPersistedCount] [int] NOT NULL,
	[SnapshotStatus] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[SnapshotCompletedAtUtc] [datetime2](7) NULL,
	[SnapshotSource] [nvarchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[ValueScreenerStatus] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ValueScreenerLastRunAtUtc] [datetime2](7) NULL,
	[PowerRequestAcquiredAtUtc] [datetime2](7) NULL,
	[PowerRequestReleasedAtUtc] [datetime2](7) NULL,
	[ErrorStep] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[ErrorMessage] [nvarchar](2000) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[MachineName] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[CreatedAtUtc] [datetime2](7) NOT NULL,
	[LastHeartbeatAtUtc] [datetime2](7) NULL,
	[LastHeartbeatStep] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[TriggerCorrelationId] [nvarchar](32) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
 CONSTRAINT [PK_AutomationRunLogs] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AutomationNotificationRecords](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[OperationKey] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Action] [nvarchar](40) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Status] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[AttemptCount] [int] NOT NULL,
	[SentAtUtc] [datetime2](7) NULL,
	[ErrorMessage] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[CreatedAtUtc] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_AutomationNotificationRecords] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AuditLogs](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[OccurredAtUtc] [datetime2](3) NOT NULL,
	[UserId] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[UserName] [nvarchar](256) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Source] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Category] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Action] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[EntityType] [nvarchar](60) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[EntityId] [nvarchar](64) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Symbol] [nvarchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[AccountType] [nvarchar](120) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Summary] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ChangesJson] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[CorrelationId] [nvarchar](64) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
 CONSTRAINT [PK_AuditLogs] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[WatchlistSnapshots](
	[UserId] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[SnapshotJson] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[UpdatedAt] [datetime2](7) NOT NULL,
	[ItemCount] [int] NOT NULL,
 CONSTRAINT [PK_WatchlistSnapshots] PRIMARY KEY CLUSTERED 
(
	[UserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[WatchlistItems](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Symbol] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Notes] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[AddedAt] [datetime2](7) NOT NULL,
	[Role] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[IsFavorite] [bit] NOT NULL,
	[UserId] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[EarningsDate] [datetime2](7) NULL,
	[WatchlistTier] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
 CONSTRAINT [PK_WatchlistItems] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ValueScreenerSnapshots](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Origin] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[RunAt] [datetime2](7) NOT NULL,
	[ResultsJson] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ValueScreenerScheduleConfigs](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[ScheduledTimeEt] [nvarchar](10) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Enabled] [bit] NOT NULL,
	[LastPortfolioRunAt] [datetime2](7) NULL,
	[LastWatchlistRunAt] [datetime2](7) NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[UserPreferences](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[UserId] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[PreferenceKey] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[PreferenceValue] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[UpdatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_UserPreferences] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[UnlinkedTradeBaselines](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[SourceType] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[SourceItemId] [int] NOT NULL,
	[Symbol] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Label] [nvarchar](200) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Amount] [decimal](18, 2) NULL,
	[AccountType] [nvarchar](120) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[TradeDate] [datetime2](7) NOT NULL,
	[Fingerprint] [nvarchar](200) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[AcknowledgedAt] [datetime2](7) NOT NULL,
	[Note] [nvarchar](200) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
 CONSTRAINT [PK_UnlinkedTradeBaselines] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TransactionContextSnapshots](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[TransactionId] [int] NOT NULL,
	[CapturedAt] [datetime2](7) NOT NULL,
	[RsiAtEntry] [decimal](7, 4) NULL,
	[TrendShiftAtEntry] [nvarchar](50) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[FibZoneAtEntry] [nvarchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[VolumeSignalAtEntry] [nvarchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[TurnStrengthAtEntry] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[ValueScoreAtEntry] [decimal](7, 2) NULL,
	[ValueTierAtEntry] [nvarchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[HoldingRoleAtEntry] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[SectorAllocationStatusAtEntry] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
 CONSTRAINT [PK_TransactionContextSnapshots] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TradeEpisodes](
	[Id] [uniqueidentifier] NOT NULL,
	[UserId] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[AccountType] [nvarchar](120) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Symbol] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[OpenDate] [datetime2](7) NOT NULL,
	[ClosedDate] [datetime2](7) NULL,
	[Status] [nvarchar](10) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Source] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[CreatedAtUtc] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_TradeEpisodes] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TechnicalChannels](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Ticker] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Timeframe] [nvarchar](10) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Direction] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Slope] [decimal](18, 8) NOT NULL,
	[LowerRailCurrent] [decimal](18, 4) NOT NULL,
	[UpperRailCurrent] [decimal](18, 4) NOT NULL,
	[ChannelQuality] [int] NOT NULL,
	[LowerTouchCount] [int] NOT NULL,
	[LastLowerTouchDate] [datetime2](7) NULL,
	[DistanceToLowerRailPercent] [decimal](18, 4) NOT NULL,
	[DistanceToLowerRailATR] [decimal](18, 4) NOT NULL,
	[ChannelState] [nvarchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[NearestOpenGapAbove] [decimal](18, 4) NULL,
	[NearestOpenGapBelow] [decimal](18, 4) NULL,
	[DistanceToGapAbovePercent] [decimal](18, 4) NULL,
	[DistanceToGapBelowPercent] [decimal](18, 4) NULL,
	[CalculatedAt] [datetime2](7) NOT NULL,
	[TouchDetailsJson] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
 CONSTRAINT [PK_TechnicalChannels] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[StagedSignals](
	[StagedId] [int] IDENTITY(1,1) NOT NULL,
	[Symbol] [varchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ScanType] [varchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[BasePrice] [decimal](18, 4) NOT NULL,
	[BaseRsi] [decimal](18, 4) NOT NULL,
	[BaseHigh] [decimal](18, 4) NOT NULL,
	[BaseLow] [decimal](18, 4) NOT NULL,
	[PreviousPrice] [decimal](18, 4) NULL,
	[PreviousRsi] [decimal](18, 4) NULL,
	[CurrentPrice] [decimal](18, 4) NULL,
	[CurrentRsi] [decimal](18, 4) NULL,
	[RsiDelta1D] [decimal](18, 4) NULL,
	[ExtremeLow] [decimal](18, 4) NULL,
	[ExtremeHigh] [decimal](18, 4) NULL,
	[StagedDate] [date] NOT NULL,
	[LastEvaluatedDate] [date] NULL,
	[IsActiveWatch] [bit] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[StagedId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[SinglePositionLimits](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Role] [nvarchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[TargetPct] [decimal](5, 2) NOT NULL,
	[DisplayOrder] [int] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[SignalAuditEvents](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[UserId] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Symbol] [nvarchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[AnalysisSymbol] [nvarchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Currency] [nvarchar](10) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[SignalType] [nvarchar](60) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[SignalCategory] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Direction] [nvarchar](10) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[SignalDate] [date] NOT NULL,
	[CapturedAtUtc] [datetime2](3) NOT NULL,
	[SignalPrice] [decimal](18, 6) NOT NULL,
	[Score] [decimal](9, 2) NULL,
	[Role] [nvarchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Source] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Origin] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[RuleVersion] [nvarchar](40) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[EpisodeKey] [nvarchar](120) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[EpisodeEndedDate] [date] NULL,
	[ContextJson] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[MyAction] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[MyActionNote] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[MyActionUpdatedUtc] [datetime2](3) NULL,
	[LinkedTransactionId] [int] NULL,
	[BasisClose] [decimal](18, 6) NULL,
	[EligibilityStatus] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[LastValidatedAtUtc] [datetime2](3) NULL,
	[PerformanceAudited] [bit] NOT NULL,
	[Status] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[StatusChangedAtUtc] [datetime2](3) NULL,
	[StatusReason] [nvarchar](200) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[SupersededByEventId] [bigint] NULL,
 CONSTRAINT [PK_SignalAuditEvents] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[SecuritySectorCaches](
	[Symbol] [nvarchar](30) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Sector] [nvarchar](100) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[UpdatedUtc] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_SecuritySectorCaches] PRIMARY KEY CLUSTERED 
(
	[Symbol] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[SecurityAnalysisMappings](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[TradingTicker] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[UnderlyingTicker] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[UnderlyingMarket] [nvarchar](10) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[UseUnderlyingForAnalysis] [bit] NOT NULL,
	[ResolutionStatus] [int] NOT NULL,
	[MappingSource] [int] NOT NULL,
	[UserId] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[DetectionDetail] [nvarchar](500) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_SecurityAnalysisMappings] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[SectorIndustryConfigs](
	[Id] [int] NOT NULL,
	[SectorsJson] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[IndustriesJson] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[DecisionSourcesJson] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
 CONSTRAINT [PK_SectorIndustryConfigs] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[RsiScanSnapshots](
	[Id] [int] NOT NULL,
	[SnapshotJson] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ScannedAt] [datetime2](7) NOT NULL,
	[SymbolCount] [int] NOT NULL,
	[OversoldCount] [int] NOT NULL,
	[OverboughtCount] [int] NOT NULL,
 CONSTRAINT [PK_RsiScanSnapshots] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[RiskPlans](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[UserId] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[AccountType] [nvarchar](120) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Symbol] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[PlannedStopPrice] [decimal](18, 4) NULL,
	[TargetPrice] [decimal](18, 4) NULL,
	[StopType] [nvarchar](10) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[StopRationale] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[TradeRationale] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[RiskNotes] [nvarchar](2000) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Currency] [nvarchar](3) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[TradeEpisodeId] [uniqueidentifier] NULL,
	[EpisodeOpenDate] [datetime2](7) NULL,
	[CreatedAtUtc] [datetime2](7) NOT NULL,
	[UpdatedAtUtc] [datetime2](7) NOT NULL,
	[RowVersion] [timestamp] NULL,
 CONSTRAINT [PK_RiskPlans] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[RiskAuditEvents](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[OccurredAtUtc] [datetime2](7) NOT NULL,
	[UserId] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[UserName] [nvarchar](256) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Source] [nvarchar](10) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Symbol] [nvarchar](20) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[AccountType] [nvarchar](120) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[RiskPlanId] [int] NULL,
	[BrokerStopRecordId] [int] NULL,
	[EventType] [nvarchar](40) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ChangesJson] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[Reason] [nvarchar](1000) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[CorrelationId] [nvarchar](64) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[TradeEpisodeId] [uniqueidentifier] NULL,
 CONSTRAINT [PK_RiskAuditEvents] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[RefreshTokens](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Token] [nvarchar](64) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[UserId] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ExpiresAt] [datetime2](7) NOT NULL,
	[IsRevoked] [bit] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_RefreshTokens] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AspNetUserTokens](
	[UserId] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[LoginProvider] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Name] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[Value] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
 CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY CLUSTERED 
(
	[UserId] ASC,
	[LoginProvider] ASC,
	[Name] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AspNetUserRoles](
	[UserId] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[RoleId] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
 CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY CLUSTERED 
(
	[UserId] ASC,
	[RoleId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AspNetUserLogins](
	[LoginProvider] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ProviderKey] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ProviderDisplayName] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[UserId] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
 CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY CLUSTERED 
(
	[LoginProvider] ASC,
	[ProviderKey] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AspNetUserClaims](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[UserId] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ClaimType] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[ClaimValue] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
 CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AspNetRoleClaims](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[RoleId] [nvarchar](450) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL,
	[ClaimType] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
	[ClaimValue] [nvarchar](max) COLLATE SQL_Latin1_General_CP1_CI_AS NULL,
 CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_AdhocAnalysisSessions_SessionKey_UpdatedAt] ON [dbo].[AdhocAnalysisSessions]
(
	[SessionKey] ASC,
	[UpdatedAt] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_AccountTypes_NormalizedName] ON [dbo].[AccountTypes]
(
	[NormalizedName] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE UNIQUE NONCLUSTERED INDEX [RoleNameIndex] ON [dbo].[AspNetRoles]
(
	[NormalizedName] ASC
)
WHERE ([NormalizedName] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [EmailIndex] ON [dbo].[AspNetUsers]
(
	[NormalizedEmail] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE UNIQUE NONCLUSTERED INDEX [UserNameIndex] ON [dbo].[AspNetUsers]
(
	[NormalizedUserName] ASC
)
WHERE ([NormalizedUserName] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_PreMarketFeedbacks_RunId] ON [dbo].[PreMarketFeedbacks]
(
	[RunId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_PreMarketAgentRuns_RunKey] ON [dbo].[PreMarketAgentRuns]
(
	[RunKey] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_PortfolioValueHistories_RecordedDate] ON [dbo].[PortfolioValueHistories]
(
	[RecordedDate] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_PortfolioItems_Symbol] ON [dbo].[PortfolioItems]
(
	[Symbol] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_PortfolioItems_TradeEpisodeId] ON [dbo].[PortfolioItems]
(
	[TradeEpisodeId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_NewsArticles_Fingerprint] ON [dbo].[NewsArticles]
(
	[Fingerprint] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_NewsArticles_Symbol_PublishedUtc] ON [dbo].[NewsArticles]
(
	[Symbol] ASC,
	[PublishedUtc] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_MarketLeadershipTrackers_UserId_Symbol] ON [dbo].[MarketLeadershipTrackers]
(
	[UserId] ASC,
	[Symbol] ASC
)
WHERE ([IsActive]=(1))
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_DailySignals_SignalDate] ON [dbo].[DailySignals]
(
	[SignalDate] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_DailySignals_Symbol] ON [dbo].[DailySignals]
(
	[Symbol] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_DailySignals_Symbol_ScanType_SignalType_TradingDate] ON [dbo].[DailySignals]
(
	[Symbol] ASC,
	[ScanType] ASC,
	[SignalType] ASC,
	[TradingDate] ASC
)
WHERE ([TradingDate] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_DailySignals_Symbol_SignalDate] ON [dbo].[DailySignals]
(
	[Symbol] ASC,
	[SignalDate] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_CashItems_Account_OpeningBalance] ON [dbo].[CashItems]
(
	[AccountType] ASC,
	[TransactionDate] ASC
)
WHERE ([CashFlowType]='OpeningBalance')
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_CashItems_TradeLink] ON [dbo].[CashItems]
(
	[SourceType] ASC,
	[SourceItemId] ASC
)
WHERE ([SourceType] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_BrokerStopRecords_AccountType_Symbol_UserStatus] ON [dbo].[BrokerStopRecords]
(
	[AccountType] ASC,
	[Symbol] ASC,
	[UserStatus] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_BrokerStopRecords_OrderIdentity] ON [dbo].[BrokerStopRecords]
(
	[OrderIdentity] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_BrokerStopRecords_ImportKey] ON [dbo].[BrokerStopRecords]
(
	[ImportKey] ASC
)
WHERE ([ImportKey] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_AutomationRunLogs_RunId] ON [dbo].[AutomationRunLogs]
(
	[RunId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_AutomationRunLogs_TradingDate] ON [dbo].[AutomationRunLogs]
(
	[TradingDate] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_AutomationRunLogs_TriggerCorrelationId] ON [dbo].[AutomationRunLogs]
(
	[TriggerCorrelationId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_AutomationNotificationRecords_OperationKey] ON [dbo].[AutomationNotificationRecords]
(
	[OperationKey] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_AuditLogs_Category_OccurredAtUtc] ON [dbo].[AuditLogs]
(
	[Category] ASC,
	[OccurredAtUtc] DESC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_AuditLogs_OccurredAtUtc] ON [dbo].[AuditLogs]
(
	[OccurredAtUtc] DESC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_AuditLogs_Symbol_OccurredAtUtc] ON [dbo].[AuditLogs]
(
	[Symbol] ASC,
	[OccurredAtUtc] DESC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_AuditLogs_UserId_OccurredAtUtc] ON [dbo].[AuditLogs]
(
	[UserId] ASC,
	[OccurredAtUtc] DESC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_WatchlistItems_Symbol_UserId] ON [dbo].[WatchlistItems]
(
	[Symbol] ASC,
	[UserId] ASC
)
WHERE ([UserId] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_ValueScreenerSnapshots_Origin_RunAt] ON [dbo].[ValueScreenerSnapshots]
(
	[Origin] ASC,
	[RunAt] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_UserPreferences_UserId_PreferenceKey] ON [dbo].[UserPreferences]
(
	[UserId] ASC,
	[PreferenceKey] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_UnlinkedTradeBaselines_SourceType_SourceItemId] ON [dbo].[UnlinkedTradeBaselines]
(
	[SourceType] ASC,
	[SourceItemId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_TransactionContextSnapshots_TransactionId] ON [dbo].[TransactionContextSnapshots]
(
	[TransactionId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_TradeEpisodes_AccountType_Symbol_Status] ON [dbo].[TradeEpisodes]
(
	[AccountType] ASC,
	[Symbol] ASC,
	[Status] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_TechnicalChannels_Ticker_Timeframe] ON [dbo].[TechnicalChannels]
(
	[Ticker] ASC,
	[Timeframe] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_StagedSignals_IsActiveWatch] ON [dbo].[StagedSignals]
(
	[IsActiveWatch] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_StagedSignals_Symbol] ON [dbo].[StagedSignals]
(
	[Symbol] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_StagedSignals_Symbol_ScanType_Active] ON [dbo].[StagedSignals]
(
	[Symbol] ASC,
	[ScanType] ASC
)
WHERE ([IsActiveWatch]=(1))
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_SignalAuditEvents_SupersededByEventId] ON [dbo].[SignalAuditEvents]
(
	[SupersededByEventId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_SignalAuditEvents_UserId_EpisodeKey] ON [dbo].[SignalAuditEvents]
(
	[UserId] ASC,
	[EpisodeKey] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_SignalAuditEvents_UserId_SignalDate] ON [dbo].[SignalAuditEvents]
(
	[UserId] ASC,
	[SignalDate] DESC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_SignalAuditEvents_UserId_Status] ON [dbo].[SignalAuditEvents]
(
	[UserId] ASC,
	[Status] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_SignalAuditEvents_UserId_Symbol_SignalType] ON [dbo].[SignalAuditEvents]
(
	[UserId] ASC,
	[Symbol] ASC,
	[SignalType] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_SignalAuditEvents_OpenIdentity] ON [dbo].[SignalAuditEvents]
(
	[UserId] ASC,
	[Symbol] ASC,
	[SignalType] ASC,
	[Source] ASC
)
WHERE ([EpisodeEndedDate] IS NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_SecurityAnalysisMappings_TradingTicker_UserId] ON [dbo].[SecurityAnalysisMappings]
(
	[TradingTicker] ASC,
	[UserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_SecurityAnalysisMappings_UnderlyingTicker] ON [dbo].[SecurityAnalysisMappings]
(
	[UnderlyingTicker] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_RiskPlans_UserId_AccountType_Symbol_TradeEpisodeId] ON [dbo].[RiskPlans]
(
	[UserId] ASC,
	[AccountType] ASC,
	[Symbol] ASC,
	[TradeEpisodeId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_RiskAuditEvents_AccountType_Symbol_OccurredAtUtc] ON [dbo].[RiskAuditEvents]
(
	[AccountType] ASC,
	[Symbol] ASC,
	[OccurredAtUtc] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_RiskAuditEvents_BrokerStopRecordId] ON [dbo].[RiskAuditEvents]
(
	[BrokerStopRecordId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX [IX_RiskAuditEvents_RiskPlanId] ON [dbo].[RiskAuditEvents]
(
	[RiskPlanId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_RiskAuditEvents_UserId_TradeEpisodeId_OccurredAtUtc] ON [dbo].[RiskAuditEvents]
(
	[UserId] ASC,
	[TradeEpisodeId] ASC,
	[OccurredAtUtc] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_RefreshTokens_Token] ON [dbo].[RefreshTokens]
(
	[Token] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_RefreshTokens_UserId] ON [dbo].[RefreshTokens]
(
	[UserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_AspNetUserRoles_RoleId] ON [dbo].[AspNetUserRoles]
(
	[RoleId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_AspNetUserLogins_UserId] ON [dbo].[AspNetUserLogins]
(
	[UserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_AspNetUserClaims_UserId] ON [dbo].[AspNetUserClaims]
(
	[UserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
SET ANSI_PADDING ON

GO
CREATE NONCLUSTERED INDEX [IX_AspNetRoleClaims_RoleId] ON [dbo].[AspNetRoleClaims]
(
	[RoleId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
ALTER TABLE [dbo].[AdhocAnalysisSessions] ADD  DEFAULT ('default') FOR [SessionKey]
GO
ALTER TABLE [dbo].[AdhocAnalysisSessions] ADD  DEFAULT ('[]') FOR [Symbols]
GO
ALTER TABLE [dbo].[AdhocAnalysisSessions] ADD  DEFAULT ((30.0)) FOR [OversoldThreshold]
GO
ALTER TABLE [dbo].[AdhocAnalysisSessions] ADD  DEFAULT ((75.0)) FOR [OverboughtThreshold]
GO
ALTER TABLE [dbo].[AdhocAnalysisSessions] ADD  DEFAULT ('Legacy') FOR [LogicMode]
GO
ALTER TABLE [dbo].[PortfolioValueHistories] ADD  DEFAULT (getutcdate()) FOR [RecordedAt]
GO
ALTER TABLE [dbo].[PortfolioValueHistories] ADD  DEFAULT ('') FOR [RecordedDate]
GO
ALTER TABLE [dbo].[PortfolioValueHistories] ADD  DEFAULT ((0)) FOR [TotalValue]
GO
ALTER TABLE [dbo].[PortfolioValueHistories] ADD  DEFAULT ((0)) FOR [StocksValue]
GO
ALTER TABLE [dbo].[PortfolioValueHistories] ADD  DEFAULT ((0)) FOR [CashValue]
GO
ALTER TABLE [dbo].[PortfolioValueHistories] ADD  DEFAULT ((0)) FOR [OptionsValue]
GO
ALTER TABLE [dbo].[PortfolioValueHistories] ADD  DEFAULT ((0)) FOR [Source]
GO
ALTER TABLE [dbo].[PortfolioSnapshots] ADD  CONSTRAINT [DF_PortSnap_Json]  DEFAULT (N'[]') FOR [SnapshotJson]
GO
ALTER TABLE [dbo].[PortfolioSnapshots] ADD  DEFAULT (getutcdate()) FOR [UpdatedAt]
GO
ALTER TABLE [dbo].[PortfolioSnapshots] ADD  DEFAULT ((0)) FOR [ItemCount]
GO
ALTER TABLE [dbo].[PortfolioItems] ADD  DEFAULT (getutcdate()) FOR [AddedAt]
GO
ALTER TABLE [dbo].[PortfolioItems] ADD  DEFAULT (N'') FOR [Industry]
GO
ALTER TABLE [dbo].[PortfolioItems] ADD  DEFAULT (N'') FOR [Sector]
GO
ALTER TABLE [dbo].[PortfolioItems] ADD  DEFAULT (CONVERT([bit],(0))) FOR [IsManual]
GO
ALTER TABLE [dbo].[PortfolioItems] ADD  DEFAULT ((0)) FOR [SectorIsOverridden]
GO
ALTER TABLE [dbo].[OptionItems] ADD  DEFAULT ((1)) FOR [NumberOfContracts]
GO
ALTER TABLE [dbo].[OptionItems] ADD  DEFAULT ((0)) FOR [MarketPrice]
GO
ALTER TABLE [dbo].[OptionItems] ADD  DEFAULT (getutcdate()) FOR [AddedAt]
GO
ALTER TABLE [dbo].[DashboardSnapshots] ADD  DEFAULT (N'{}') FOR [SnapshotJson]
GO
ALTER TABLE [dbo].[DailySignals] ADD  DEFAULT (N'') FOR [CompanyName]
GO
ALTER TABLE [dbo].[DailySignals] ADD  DEFAULT (N'') FOR [TriggerDetails]
GO
ALTER TABLE [dbo].[DailySignals] ADD  DEFAULT (N'Legacy') FOR [RuleVersion]
GO
ALTER TABLE [dbo].[DailySignals] ADD  DEFAULT (N'Active') FOR [SignalState]
GO
ALTER TABLE [dbo].[DailySignals] ADD  DEFAULT (N'') FOR [Sector]
GO
ALTER TABLE [dbo].[DailySignals] ADD  DEFAULT (N'') FOR [ReversalProbability]
GO
ALTER TABLE [dbo].[DailySignals] ADD  DEFAULT (N'') FOR [VolumeSignal]
GO
ALTER TABLE [dbo].[CashItems] ADD  DEFAULT ('CASH') FOR [Description]
GO
ALTER TABLE [dbo].[CashItems] ADD  DEFAULT (getutcdate()) FOR [AddedAt]
GO
ALTER TABLE [dbo].[AutomationRunLogs] ADD  DEFAULT (N'Running') FOR [OverallStatus]
GO
ALTER TABLE [dbo].[AutomationRunLogs] ADD  DEFAULT (N'') FOR [RefreshStatus]
GO
ALTER TABLE [dbo].[AutomationRunLogs] ADD  DEFAULT (N'') FOR [RsiStatus]
GO
ALTER TABLE [dbo].[AutomationRunLogs] ADD  DEFAULT (N'') FOR [SnapshotStatus]
GO
ALTER TABLE [dbo].[AutomationRunLogs] ADD  DEFAULT (N'') FOR [ValueScreenerStatus]
GO
ALTER TABLE [dbo].[AutomationRunLogs] ADD  DEFAULT (N'') FOR [MachineName]
GO
ALTER TABLE [dbo].[WatchlistSnapshots] ADD  CONSTRAINT [DF_WlSnap_Json]  DEFAULT (N'[]') FOR [SnapshotJson]
GO
ALTER TABLE [dbo].[WatchlistSnapshots] ADD  DEFAULT (getutcdate()) FOR [UpdatedAt]
GO
ALTER TABLE [dbo].[WatchlistSnapshots] ADD  DEFAULT ((0)) FOR [ItemCount]
GO
ALTER TABLE [dbo].[WatchlistItems] ADD  DEFAULT (N'') FOR [Notes]
GO
ALTER TABLE [dbo].[WatchlistItems] ADD  DEFAULT (N'Strategic') FOR [Role]
GO
ALTER TABLE [dbo].[WatchlistItems] ADD  DEFAULT ((0)) FOR [IsFavorite]
GO
ALTER TABLE [dbo].[WatchlistItems] ADD  DEFAULT (N'Strategic') FOR [WatchlistTier]
GO
ALTER TABLE [dbo].[ValueScreenerSnapshots] ADD  DEFAULT ('[]') FOR [ResultsJson]
GO
ALTER TABLE [dbo].[ValueScreenerScheduleConfigs] ADD  DEFAULT ('17:00') FOR [ScheduledTimeEt]
GO
ALTER TABLE [dbo].[ValueScreenerScheduleConfigs] ADD  DEFAULT ((1)) FOR [Enabled]
GO
ALTER TABLE [dbo].[UserPreferences] ADD  CONSTRAINT [DF_UserPref_Value]  DEFAULT (N'') FOR [PreferenceValue]
GO
ALTER TABLE [dbo].[UserPreferences] ADD  DEFAULT (getutcdate()) FOR [UpdatedAt]
GO
ALTER TABLE [dbo].[TechnicalChannels] ADD  DEFAULT (N'') FOR [TouchDetailsJson]
GO
ALTER TABLE [dbo].[StagedSignals] ADD  DEFAULT ((1)) FOR [IsActiveWatch]
GO
ALTER TABLE [dbo].[StagedSignals] ADD  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[StagedSignals] ADD  DEFAULT (sysdatetime()) FOR [UpdatedAt]
GO
ALTER TABLE [dbo].[SignalAuditEvents] ADD  DEFAULT (N'Unverified') FOR [EligibilityStatus]
GO
ALTER TABLE [dbo].[SignalAuditEvents] ADD  DEFAULT (CONVERT([bit],(1))) FOR [PerformanceAudited]
GO
ALTER TABLE [dbo].[SignalAuditEvents] ADD  DEFAULT (N'Active') FOR [Status]
GO
ALTER TABLE [dbo].[SectorIndustryConfigs] ADD  DEFAULT (N'[]') FOR [SectorsJson]
GO
ALTER TABLE [dbo].[SectorIndustryConfigs] ADD  DEFAULT (N'[]') FOR [IndustriesJson]
GO
ALTER TABLE [dbo].[SectorIndustryConfigs] ADD  DEFAULT (N'[]') FOR [DecisionSourcesJson]
GO
ALTER TABLE [dbo].[RsiScanSnapshots] ADD  CONSTRAINT [DF_RsiSnap_Json]  DEFAULT (N'{}') FOR [SnapshotJson]
GO
ALTER TABLE [dbo].[RsiScanSnapshots] ADD  DEFAULT (getutcdate()) FOR [ScannedAt]
GO
ALTER TABLE [dbo].[RsiScanSnapshots] ADD  DEFAULT ((0)) FOR [SymbolCount]
GO
ALTER TABLE [dbo].[RsiScanSnapshots] ADD  DEFAULT ((0)) FOR [OversoldCount]
GO
ALTER TABLE [dbo].[RsiScanSnapshots] ADD  DEFAULT ((0)) FOR [OverboughtCount]
GO
ALTER TABLE [dbo].[SignalAuditEvents]  WITH CHECK ADD  CONSTRAINT [FK_SignalAuditEvents_SignalAuditEvents_SupersededByEventId] FOREIGN KEY([SupersededByEventId])
REFERENCES [dbo].[SignalAuditEvents] ([Id])
GO
ALTER TABLE [dbo].[SignalAuditEvents] CHECK CONSTRAINT [FK_SignalAuditEvents_SignalAuditEvents_SupersededByEventId]
GO
ALTER TABLE [dbo].[RiskAuditEvents]  WITH CHECK ADD  CONSTRAINT [FK_RiskAuditEvents_BrokerStopRecords_BrokerStopRecordId] FOREIGN KEY([BrokerStopRecordId])
REFERENCES [dbo].[BrokerStopRecords] ([Id])
GO
ALTER TABLE [dbo].[RiskAuditEvents] CHECK CONSTRAINT [FK_RiskAuditEvents_BrokerStopRecords_BrokerStopRecordId]
GO
ALTER TABLE [dbo].[RiskAuditEvents]  WITH CHECK ADD  CONSTRAINT [FK_RiskAuditEvents_RiskPlans_RiskPlanId] FOREIGN KEY([RiskPlanId])
REFERENCES [dbo].[RiskPlans] ([Id])
GO
ALTER TABLE [dbo].[RiskAuditEvents] CHECK CONSTRAINT [FK_RiskAuditEvents_RiskPlans_RiskPlanId]
GO
ALTER TABLE [dbo].[RefreshTokens]  WITH CHECK ADD  CONSTRAINT [FK_RefreshTokens_AspNetUsers_UserId] FOREIGN KEY([UserId])
REFERENCES [dbo].[AspNetUsers] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[RefreshTokens] CHECK CONSTRAINT [FK_RefreshTokens_AspNetUsers_UserId]
GO
ALTER TABLE [dbo].[AspNetUserTokens]  WITH CHECK ADD  CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY([UserId])
REFERENCES [dbo].[AspNetUsers] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[AspNetUserTokens] CHECK CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId]
GO
ALTER TABLE [dbo].[AspNetUserRoles]  WITH CHECK ADD  CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY([RoleId])
REFERENCES [dbo].[AspNetRoles] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[AspNetUserRoles] CHECK CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId]
GO
ALTER TABLE [dbo].[AspNetUserRoles]  WITH CHECK ADD  CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY([UserId])
REFERENCES [dbo].[AspNetUsers] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[AspNetUserRoles] CHECK CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId]
GO
ALTER TABLE [dbo].[AspNetUserLogins]  WITH CHECK ADD  CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY([UserId])
REFERENCES [dbo].[AspNetUsers] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[AspNetUserLogins] CHECK CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId]
GO
ALTER TABLE [dbo].[AspNetUserClaims]  WITH CHECK ADD  CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY([UserId])
REFERENCES [dbo].[AspNetUsers] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[AspNetUserClaims] CHECK CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId]
GO
ALTER TABLE [dbo].[AspNetRoleClaims]  WITH CHECK ADD  CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY([RoleId])
REFERENCES [dbo].[AspNetRoles] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[AspNetRoleClaims] CHECK CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId]
