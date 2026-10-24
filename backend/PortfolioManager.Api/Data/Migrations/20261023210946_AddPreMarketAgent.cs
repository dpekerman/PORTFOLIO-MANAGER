using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PortfolioManager.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPreMarketAgent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NewsArticles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Fingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    Headline = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Publisher = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    PublishedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RetrievedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    SourceId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsArticles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NewsSources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    ProviderType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    ApprovalStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Markets = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    RefreshMinutes = table.Column<int>(type: "int", nullable: false),
                    TimeoutSeconds = table.Column<int>(type: "int", nullable: false),
                    MaxItems = table.Column<int>(type: "int", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    LastSuccessUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastFailureUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AvgDurationMs = table.Column<int>(type: "int", nullable: false),
                    UsefulArticleCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsSources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PreMarketAgentConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    ScheduleEnabled = table.Column<bool>(type: "bit", nullable: false),
                    StartTimeLocal = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    FirstRunDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AsOfDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DeadlineLocal = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    ShadowMode = table.Column<bool>(type: "bit", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DiscoveryMode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AiEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AiMaxCalls = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreMarketAgentConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PreMarketAgentRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunKey = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AsOfDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsTestRun = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DurationMs = table.Column<int>(type: "int", nullable: false),
                    ReportJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReportHtml = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmailStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EmailMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmailSubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AiModel = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AiCalls = table.Column<int>(type: "int", nullable: false),
                    AiDurationMs = table.Column<int>(type: "int", nullable: false),
                    NewsDurationMs = table.Column<int>(type: "int", nullable: false),
                    ArticlesRetrieved = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreMarketAgentRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PreMarketFeedbacks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunId = table.Column<int>(type: "int", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Rating = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreMarketFeedbacks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NewsArticles_Fingerprint",
                table: "NewsArticles",
                column: "Fingerprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NewsArticles_Symbol_PublishedUtc",
                table: "NewsArticles",
                columns: new[] { "Symbol", "PublishedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PreMarketAgentRuns_RunKey",
                table: "PreMarketAgentRuns",
                column: "RunKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PreMarketFeedbacks_RunId",
                table: "PreMarketFeedbacks",
                column: "RunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NewsArticles");

            migrationBuilder.DropTable(
                name: "NewsSources");

            migrationBuilder.DropTable(
                name: "PreMarketAgentConfigs");

            migrationBuilder.DropTable(
                name: "PreMarketAgentRuns");

            migrationBuilder.DropTable(
                name: "PreMarketFeedbacks");
        }
    }
}
