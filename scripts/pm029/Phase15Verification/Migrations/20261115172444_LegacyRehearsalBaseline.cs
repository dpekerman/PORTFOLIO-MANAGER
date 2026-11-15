using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Phase15Verification.Migrations
{
    /// <inheritdoc />
    public partial class LegacyRehearsalBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdhocAnalysisSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "default"),
                    Symbols = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    ResultsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OversoldThreshold = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 30m),
                    OverboughtThreshold = table.Column<decimal>(type: "decimal(5,2)", nullable: false, defaultValue: 75m),
                    LogicMode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Legacy"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdhocAnalysisSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AllocationRiskTargets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Role = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TargetPct = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AllocationRiskTargets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AllocationSectorTargets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Sector = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TargetPct = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AllocationSectorTargets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Symbol = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    AccountType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ChangesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AutomationNotificationRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationNotificationRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AutomationRunLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TradingDate = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TriggerType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TriggerCorrelationId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    ScheduledStartUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualStartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OverallStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Running"),
                    OwnerUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RefreshStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: ""),
                    RefreshStartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RefreshCompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PortfolioSymbolCount = table.Column<int>(type: "int", nullable: false),
                    WatchlistSymbolCount = table.Column<int>(type: "int", nullable: false),
                    RsiStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: ""),
                    RsiCompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EodSignalsPersistedCount = table.Column<int>(type: "int", nullable: false),
                    SnapshotStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: ""),
                    SnapshotCompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SnapshotSource = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ValueScreenerStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: ""),
                    ValueScreenerLastRunAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PowerRequestAcquiredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PowerRequestReleasedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastHeartbeatAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastHeartbeatStep = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ErrorStep = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    MachineName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationRunLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BrokerStopRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderIdentity = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImportKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    AccountType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OrderType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TriggerPrice = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    LimitPrice = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    UserStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    BrokerVerificationStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CoveredQuantity = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    PlacedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BrokerOrderRef = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastUserVerifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmationSource = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TradeEpisodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EpisodeOpenDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReplacedByRecordId = table.Column<int>(type: "int", nullable: true),
                    BrokerCancellationUnverified = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrokerStopRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CashItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, defaultValue: "CASH"),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AccountType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CashFlowType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SourceType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SourceItemId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CashLedgerSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LedgerStartDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashLedgerSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DailySignals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, defaultValue: ""),
                    ScanType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SignalType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Rsi = table.Column<decimal>(type: "decimal(7,4)", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TriggerDetails = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false, defaultValue: ""),
                    SignalDate = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TradingDate = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ScannedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RuleVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Legacy"),
                    SignalState = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Active"),
                    PreviousSignalState = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Sector = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    ReversalProbability = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: ""),
                    VolumeSignal = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: ""),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TrendShift = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RsiDelta1D = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    EntryPrice = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    StopLossPrice = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    RiskPerShare = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    PositionSizingShares = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    PositionSizingRiskAmount = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    PositionSizingPositionValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    PositionSizingLimitingReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Sma200 = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    Ema9AtEntry = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    Ema9ConfirmedAtEntry = table.Column<bool>(type: "bit", nullable: true),
                    Fib61_8AtSignal = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    FibZoneAtSignal = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    FibStatusAtSignal = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    AnalysisTicker = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    AnalysisCurrency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailySignals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DashboardSnapshots",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "{}"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DashboardSnapshots", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "IndexConstituents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IndexCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "date", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    SourceAsOfDate = table.Column<DateTime>(type: "date", nullable: false),
                    VerifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VerifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndexConstituents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketLeadershipTrackers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TrackerType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketLeadershipTrackers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OpportunityRefreshStates",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    LastAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastAttemptStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ErrorSummary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LastSuccessfulSnapshotId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpportunityRefreshStates", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "OpportunitySnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ScoringVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EvaluationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ComputedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinalActionComputedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataAsOfSession = table.Column<DateTime>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ErrorSummary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpportunitySnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OptionItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UnderlyingTicker = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PositionType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Strike = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Premium = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    NumberOfContracts = table.Column<int>(type: "int", nullable: false),
                    MarketPrice = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TransactionType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    AccountType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    OpenDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CloseDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosingPrice = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DecisionSource = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DecisionSourceClosed = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OptionItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PortfolioItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Shares = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    AverageCostBasis = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Sector = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    Industry = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    SectorIsOverridden = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsManual = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ManualMarketValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TransactionType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    AccountType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    OpenDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CloseDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosingPrice = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    HoldingRole = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DecisionSource = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DecisionSourceClosed = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TradeEpisodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortfolioItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PortfolioSnapshots",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ItemCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortfolioSnapshots", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "PortfolioValueHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecordedDate = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TotalValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    StocksValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CashValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    OptionsValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    LastRecalculatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortfolioValueHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RiskPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    AccountType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PlannedStopPrice = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    TargetPrice = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    StopType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    StopRationale = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TradeRationale = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RiskNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    TradeEpisodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EpisodeOpenDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RsiScanSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "{}"),
                    ScannedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SymbolCount = table.Column<int>(type: "int", nullable: false),
                    OversoldCount = table.Column<int>(type: "int", nullable: false),
                    OverboughtCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RsiScanSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SectorBenchmarkMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Sector = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Market = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    BenchmarkSymbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BenchmarkName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ProviderSectorAliasesJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SourceAsOfDate = table.Column<DateTime>(type: "date", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectorBenchmarkMappings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SectorIndustryConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    SectorsJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    IndustriesJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    DecisionSourcesJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectorIndustryConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SecurityAnalysisMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TradingTicker = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UnderlyingTicker = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    UnderlyingMarket = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UseUnderlyingForAnalysis = table.Column<bool>(type: "bit", nullable: false),
                    ResolutionStatus = table.Column<int>(type: "int", nullable: false),
                    MappingSource = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DetectionDetail = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityAnalysisMappings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SignalAuditEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AnalysisSymbol = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    SignalType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    SignalCategory = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    SignalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    SignalPrice = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    Score = table.Column<decimal>(type: "decimal(9,2)", nullable: true),
                    Role = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Origin = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RuleVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    EpisodeKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    EpisodeEndedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ContextJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MyAction = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MyActionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MyActionUpdatedUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    LinkedTransactionId = table.Column<int>(type: "int", nullable: true),
                    EligibilityStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Unverified"),
                    BasisClose = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Active"),
                    StatusChangedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    StatusReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SupersededByEventId = table.Column<long>(type: "bigint", nullable: true),
                    LastValidatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    PerformanceAudited = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignalAuditEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SignalAuditEvents_SignalAuditEvents_SupersededByEventId",
                        column: x => x.SupersededByEventId,
                        principalTable: "SignalAuditEvents",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SinglePositionLimits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Role = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TargetPct = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SinglePositionLimits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StagedSignals",
                columns: table => new
                {
                    StagedId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ScanType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BasePrice = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    BaseRsi = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    BaseHigh = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    BaseLow = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    PreviousPrice = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    PreviousRsi = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    CurrentPrice = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    CurrentRsi = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    RsiDelta1D = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    ExtremeLow = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    ExtremeHigh = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    StagedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    LastEvaluatedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActiveWatch = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StagedSignals", x => x.StagedId);
                });

            migrationBuilder.CreateTable(
                name: "TechnicalChannels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ticker = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Timeframe = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Slope = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    LowerRailCurrent = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UpperRailCurrent = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ChannelQuality = table.Column<int>(type: "int", nullable: false),
                    LowerTouchCount = table.Column<int>(type: "int", nullable: false),
                    LastLowerTouchDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DistanceToLowerRailPercent = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    DistanceToLowerRailATR = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ChannelState = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    NearestOpenGapAbove = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    NearestOpenGapBelow = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    DistanceToGapAbovePercent = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    DistanceToGapBelowPercent = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    CalculatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TouchDetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicalChannels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TradeEpisodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    AccountType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OpenDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradeEpisodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TransactionContextSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransactionId = table.Column<int>(type: "int", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RsiAtEntry = table.Column<decimal>(type: "decimal(7,4)", nullable: true),
                    TrendShiftAtEntry = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FibZoneAtEntry = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    VolumeSignalAtEntry = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    TurnStrengthAtEntry = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ValueScoreAtEntry = table.Column<decimal>(type: "decimal(7,2)", nullable: true),
                    ValueTierAtEntry = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    HoldingRoleAtEntry = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SectorAllocationStatusAtEntry = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionContextSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UnlinkedTradeBaselines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SourceItemId = table.Column<int>(type: "int", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AccountType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    TradeDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AcknowledgedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnlinkedTradeBaselines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserPreferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    PreferenceKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PreferenceValue = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: ""),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPreferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ValueScreenerScheduleConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ScheduledTimeEt = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "17:00"),
                    Enabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    LastPortfolioRunAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastWatchlistRunAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValueScreenerScheduleConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ValueScreenerSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Origin = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RunAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResultsJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValueScreenerSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WatchlistItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false, defaultValue: ""),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Strategic"),
                    IsFavorite = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    EarningsDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WatchlistTier = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Strategic")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchlistItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WatchlistSnapshots",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ItemCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchlistSnapshots", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Token = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsRevoked = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RiskAuditEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    TradeEpisodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AccountType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    RiskPlanId = table.Column<int>(type: "int", nullable: true),
                    BrokerStopRecordId = table.Column<int>(type: "int", nullable: true),
                    EventType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ChangesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskAuditEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiskAuditEvents_BrokerStopRecords_BrokerStopRecordId",
                        column: x => x.BrokerStopRecordId,
                        principalTable: "BrokerStopRecords",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RiskAuditEvents_RiskPlans_RiskPlanId",
                        column: x => x.RiskPlanId,
                        principalTable: "RiskPlans",
                        principalColumn: "Id");
                });

            migrationBuilder.InsertData(
                table: "AccountTypes",
                columns: new[] { "Id", "Name", "NormalizedName", "Version" },
                values: new object[,]
                {
                    { 1, "TFSA_L_RBC", "TFSA_L_RBC", new Guid("00000000-0000-0000-0000-000000000001") },
                    { 2, "TFSA_L_TD", "TFSA_L_TD", new Guid("00000000-0000-0000-0000-000000000002") },
                    { 3, "TFSA_D_TD", "TFSA_D_TD", new Guid("00000000-0000-0000-0000-000000000003") },
                    { 4, "Margin_L_TD", "MARGIN_L_TD", new Guid("00000000-0000-0000-0000-000000000004") },
                    { 5, "Margin_L_RBC", "MARGIN_L_RBC", new Guid("00000000-0000-0000-0000-000000000005") },
                    { 6, "Margin_D_TD", "MARGIN_D_TD", new Guid("00000000-0000-0000-0000-000000000006") },
                    { 7, "Corp_TD", "CORP_TD", new Guid("00000000-0000-0000-0000-000000000007") }
                });

            migrationBuilder.InsertData(
                table: "SecurityAnalysisMappings",
                columns: new[] { "Id", "CreatedAt", "DetectionDetail", "MappingSource", "ResolutionStatus", "TradingTicker", "UnderlyingMarket", "UnderlyingTicker", "UpdatedAt", "UseUnderlyingForAnalysis", "UserId" },
                values: new object[,]
                {
                    { -3, new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Managed CDR reference data", 0, 1, "MU.TO", "US", "MU", new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, null },
                    { -2, new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Managed CDR reference data", 0, 1, "DIS.TO", "US", "DIS", new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, null },
                    { -1, new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Managed CDR reference data", 0, 1, "SPGI.TO", "US", "SPGI", new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountTypes_NormalizedName",
                table: "AccountTypes",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdhocAnalysisSessions_SessionKey_UpdatedAt",
                table: "AdhocAnalysisSessions",
                columns: new[] { "SessionKey", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Category_OccurredAtUtc",
                table: "AuditLogs",
                columns: new[] { "Category", "OccurredAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_OccurredAtUtc",
                table: "AuditLogs",
                column: "OccurredAtUtc",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Symbol_OccurredAtUtc",
                table: "AuditLogs",
                columns: new[] { "Symbol", "OccurredAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId_OccurredAtUtc",
                table: "AuditLogs",
                columns: new[] { "UserId", "OccurredAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationNotificationRecords_OperationKey",
                table: "AutomationNotificationRecords",
                column: "OperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AutomationRunLogs_RunId",
                table: "AutomationRunLogs",
                column: "RunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AutomationRunLogs_TradingDate",
                table: "AutomationRunLogs",
                column: "TradingDate");

            migrationBuilder.CreateIndex(
                name: "IX_AutomationRunLogs_TriggerCorrelationId",
                table: "AutomationRunLogs",
                column: "TriggerCorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_BrokerStopRecords_AccountType_Symbol_UserStatus",
                table: "BrokerStopRecords",
                columns: new[] { "AccountType", "Symbol", "UserStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_BrokerStopRecords_OrderIdentity",
                table: "BrokerStopRecords",
                column: "OrderIdentity",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_BrokerStopRecords_ImportKey",
                table: "BrokerStopRecords",
                column: "ImportKey",
                unique: true,
                filter: "[ImportKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CashItems_Account_OpeningBalance",
                table: "CashItems",
                columns: new[] { "AccountType", "TransactionDate" },
                unique: true,
                filter: "[CashFlowType] = 'OpeningBalance'");

            migrationBuilder.CreateIndex(
                name: "IX_CashItems_TradeLink",
                table: "CashItems",
                columns: new[] { "SourceType", "SourceItemId" },
                unique: true,
                filter: "[SourceType] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DailySignals_SignalDate",
                table: "DailySignals",
                column: "SignalDate");

            migrationBuilder.CreateIndex(
                name: "IX_DailySignals_Symbol",
                table: "DailySignals",
                column: "Symbol");

            migrationBuilder.CreateIndex(
                name: "IX_DailySignals_Symbol_ScanType_SignalType_TradingDate",
                table: "DailySignals",
                columns: new[] { "Symbol", "ScanType", "SignalType", "TradingDate" },
                unique: true,
                filter: "[TradingDate] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DailySignals_Symbol_SignalDate",
                table: "DailySignals",
                columns: new[] { "Symbol", "SignalDate" });

            migrationBuilder.CreateIndex(
                name: "IX_IndexConstituents_IndexCode_Symbol_EffectiveFrom",
                table: "IndexConstituents",
                columns: new[] { "IndexCode", "Symbol", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketLeadershipTrackers_UserId_Symbol",
                table: "MarketLeadershipTrackers",
                columns: new[] { "UserId", "Symbol" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_OpportunitySnapshots_UserId_ComputedAtUtc",
                table: "OpportunitySnapshots",
                columns: new[] { "UserId", "ComputedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioItems_Symbol",
                table: "PortfolioItems",
                column: "Symbol");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioItems_TradeEpisodeId",
                table: "PortfolioItems",
                column: "TradeEpisodeId");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioValueHistories_RecordedDate",
                table: "PortfolioValueHistories",
                column: "RecordedDate");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_Token",
                table: "RefreshTokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskAuditEvents_AccountType_Symbol_OccurredAtUtc",
                table: "RiskAuditEvents",
                columns: new[] { "AccountType", "Symbol", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RiskAuditEvents_BrokerStopRecordId",
                table: "RiskAuditEvents",
                column: "BrokerStopRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskAuditEvents_RiskPlanId",
                table: "RiskAuditEvents",
                column: "RiskPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskAuditEvents_UserId_TradeEpisodeId_OccurredAtUtc",
                table: "RiskAuditEvents",
                columns: new[] { "UserId", "TradeEpisodeId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RiskPlans_UserId_AccountType_Symbol_TradeEpisodeId",
                table: "RiskPlans",
                columns: new[] { "UserId", "AccountType", "Symbol", "TradeEpisodeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SectorBenchmarkMappings_Sector_Market",
                table: "SectorBenchmarkMappings",
                columns: new[] { "Sector", "Market" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityAnalysisMappings_TradingTicker_UserId",
                table: "SecurityAnalysisMappings",
                columns: new[] { "TradingTicker", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityAnalysisMappings_UnderlyingTicker",
                table: "SecurityAnalysisMappings",
                column: "UnderlyingTicker");

            migrationBuilder.CreateIndex(
                name: "IX_SignalAuditEvents_SupersededByEventId",
                table: "SignalAuditEvents",
                column: "SupersededByEventId");

            migrationBuilder.CreateIndex(
                name: "IX_SignalAuditEvents_UserId_EpisodeKey",
                table: "SignalAuditEvents",
                columns: new[] { "UserId", "EpisodeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SignalAuditEvents_UserId_SignalDate",
                table: "SignalAuditEvents",
                columns: new[] { "UserId", "SignalDate" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_SignalAuditEvents_UserId_Status",
                table: "SignalAuditEvents",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SignalAuditEvents_UserId_Symbol_SignalType",
                table: "SignalAuditEvents",
                columns: new[] { "UserId", "Symbol", "SignalType" });

            migrationBuilder.CreateIndex(
                name: "UX_SignalAuditEvents_OpenIdentity",
                table: "SignalAuditEvents",
                columns: new[] { "UserId", "Symbol", "SignalType", "Source" },
                unique: true,
                filter: "[EpisodeEndedDate] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StagedSignals_IsActiveWatch",
                table: "StagedSignals",
                column: "IsActiveWatch");

            migrationBuilder.CreateIndex(
                name: "IX_StagedSignals_Symbol",
                table: "StagedSignals",
                column: "Symbol");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalChannels_Ticker_Timeframe",
                table: "TechnicalChannels",
                columns: new[] { "Ticker", "Timeframe" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TradeEpisodes_AccountType_Symbol_Status",
                table: "TradeEpisodes",
                columns: new[] { "AccountType", "Symbol", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionContextSnapshots_TransactionId",
                table: "TransactionContextSnapshots",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_UnlinkedTradeBaselines_SourceType_SourceItemId",
                table: "UnlinkedTradeBaselines",
                columns: new[] { "SourceType", "SourceItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserPreferences_UserId_PreferenceKey",
                table: "UserPreferences",
                columns: new[] { "UserId", "PreferenceKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ValueScreenerSnapshots_Origin_RunAt",
                table: "ValueScreenerSnapshots",
                columns: new[] { "Origin", "RunAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WatchlistItems_Symbol_UserId",
                table: "WatchlistItems",
                columns: new[] { "Symbol", "UserId" },
                unique: true,
                filter: "[UserId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountTypes");

            migrationBuilder.DropTable(
                name: "AdhocAnalysisSessions");

            migrationBuilder.DropTable(
                name: "AllocationRiskTargets");

            migrationBuilder.DropTable(
                name: "AllocationSectorTargets");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "AutomationNotificationRecords");

            migrationBuilder.DropTable(
                name: "AutomationRunLogs");

            migrationBuilder.DropTable(
                name: "CashItems");

            migrationBuilder.DropTable(
                name: "CashLedgerSettings");

            migrationBuilder.DropTable(
                name: "DailySignals");

            migrationBuilder.DropTable(
                name: "DashboardSnapshots");

            migrationBuilder.DropTable(
                name: "IndexConstituents");

            migrationBuilder.DropTable(
                name: "MarketLeadershipTrackers");

            migrationBuilder.DropTable(
                name: "OpportunityRefreshStates");

            migrationBuilder.DropTable(
                name: "OpportunitySnapshots");

            migrationBuilder.DropTable(
                name: "OptionItems");

            migrationBuilder.DropTable(
                name: "PortfolioItems");

            migrationBuilder.DropTable(
                name: "PortfolioSnapshots");

            migrationBuilder.DropTable(
                name: "PortfolioValueHistories");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "RiskAuditEvents");

            migrationBuilder.DropTable(
                name: "RsiScanSnapshots");

            migrationBuilder.DropTable(
                name: "SectorBenchmarkMappings");

            migrationBuilder.DropTable(
                name: "SectorIndustryConfigs");

            migrationBuilder.DropTable(
                name: "SecurityAnalysisMappings");

            migrationBuilder.DropTable(
                name: "SignalAuditEvents");

            migrationBuilder.DropTable(
                name: "SinglePositionLimits");

            migrationBuilder.DropTable(
                name: "StagedSignals");

            migrationBuilder.DropTable(
                name: "TechnicalChannels");

            migrationBuilder.DropTable(
                name: "TradeEpisodes");

            migrationBuilder.DropTable(
                name: "TransactionContextSnapshots");

            migrationBuilder.DropTable(
                name: "UnlinkedTradeBaselines");

            migrationBuilder.DropTable(
                name: "UserPreferences");

            migrationBuilder.DropTable(
                name: "ValueScreenerScheduleConfigs");

            migrationBuilder.DropTable(
                name: "ValueScreenerSnapshots");

            migrationBuilder.DropTable(
                name: "WatchlistItems");

            migrationBuilder.DropTable(
                name: "WatchlistSnapshots");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "BrokerStopRecords");

            migrationBuilder.DropTable(
                name: "RiskPlans");
        }
    }
}
