using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services.Risk;

namespace PortfolioManager.Tests;

/// <summary>
/// Opt-in integration run against an ISOLATED SQL Server copy (restored from a backup, migration script applied).
/// Set PM033_SCRATCH_CONNECTION to the scratch database; never point it at the live database.
/// Optional PM033_REPORT_DIR receives the dry-run reports. Skipped (passes trivially) when the variable is not set.
/// </summary>
public class Pm033ScratchDbTests
{
    private sealed class Accessor : IHttpContextAccessor { public HttpContext? HttpContext { get; set; } }

    private static IHttpContextAccessor Admin() => new Accessor
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "a250c394-a67a-4713-b6e9-54c8c9efcbce"), new Claim(ClaimTypes.Name, "scratch-admin"), new Claim(ClaimTypes.Role, "Admin")], "test")),
        },
    };

    private static readonly (string Sym, decimal Stop)[] Sheet =
        [("DIS.TO", 11.85m), ("NVDA.TO", 46.50m), ("NOVO.TO", 16.20m), ("EMP-A.TO", 48.25m),
         ("EFR.TO", 10.75m), ("TA.TO", 16.75m), ("WELL.TO", 3.95m), ("MAL.TO", 32.40m)];

    private static StopSheetRequest Request(bool dryRun) => new("user-stop-sheet", "2026-10-28",
        Sheet.Select((s, i) => new StopSheetRow(s.Sym, s.Stop, "StopMarket", "CAD", null, i + 1)).ToList(), dryRun);

    [Fact]
    public async Task Scratch_database_backfill_report_stop_mapping_and_apply_are_safe_and_idempotent()
    {
        var cs = Environment.GetEnvironmentVariable("PM033_SCRATCH_CONNECTION");
        if (string.IsNullOrWhiteSpace(cs)) return;
        cs.Should().Contain("PM033Test", "this test writes data and must only run against the isolated scratch copy");

        var opts = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(cs).Options;
        await using var db = new AppDbContext(opts);
        var access = Admin();
        var reportDir = Environment.GetEnvironmentVariable("PM033_REPORT_DIR");
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };

        var before = await db.PortfolioItems.AsNoTracking().OrderBy(i => i.Id)
            .Select(i => new { i.Id, i.Symbol, i.Shares, i.AverageCostBasis, i.AccountType, i.TransactionType, i.OpenDate, i.CloseDate, i.ClosingPrice, i.HoldingRole }).ToListAsync();

        // G1: backfill is planned (dry run) and never changes financial values.
        var risk = new RiskService(db, access);
        var backfill = await risk.BackfillEpisodesAsync(dryRun: true);
        backfill.Applied.Should().BeFalse();
        (await db.TradeEpisodes.CountAsync()).Should().Be(0);
        if (reportDir is not null) await File.WriteAllTextAsync(Path.Combine(reportDir, "g1-backfill-dry-run.json"), JsonSerializer.Serialize(backfill, json));

        // G2: exact eight-record dry-run mapping from the real data copy.
        var import = new StopSheetImportService(db, access);
        var dry = await import.ImportAsync(Request(true));
        dry.Rows.Should().HaveCount(8);
        if (reportDir is not null) await File.WriteAllTextAsync(Path.Combine(reportDir, "g2-stop-import-dry-run.json"), JsonSerializer.Serialize(dry, json));
        dry.Blocked.Should().Be(0);

        // Apply on the scratch copy: atomic, audited, idempotent, unique ImportKey enforced by SQL Server.
        var applied = await import.ImportAsync(Request(false));
        applied.Applied.Should().BeTrue();
        (await db.BrokerStopRecords.CountAsync()).Should().Be(8);
        (await db.RiskAuditEvents.CountAsync()).Should().Be(8);
        (await import.ImportAsync(Request(false))).Skipped.Should().Be(8);
        (await db.BrokerStopRecords.CountAsync()).Should().Be(8);

        var trades = await new SwingTradingService(db, access, new ConfigurationBuilder().Build()).GetOpenTradesAsync();
        var withStops = trades.Rows.Where(r => r.BrokerStops.Count == 1).ToList();
        withStops.Should().HaveCount(8);
        withStops.Should().OnlyContain(r => r.CoveredQuantity == null && r.Protection == "Broker Stop Recorded");
        trades.Rows.Where(r => r.BrokerStops.Count == 0).Should().OnlyContain(r => r.Protection == "No Stop" || r.Protection == "Alert-Only Stop (no broker order)");

        // Real row-version concurrency on SQL Server.
        var pos = await risk.SavePlanAsync(new SaveRiskPlanRequest("TFSA_L_TD", "DIS.TO", 11.9m, null, null, null, null, null, null, null));
        var stale = pos.Plan!.RowVersion;
        await risk.SavePlanAsync(new SaveRiskPlanRequest("TFSA_L_TD", "DIS.TO", 11.95m, null, null, null, null, null, null, stale));
        await using var db2 = new AppDbContext(opts);
        var act = () => new RiskService(db2, access).SavePlanAsync(new SaveRiskPlanRequest("TFSA_L_TD", "DIS.TO", 12m, null, null, null, null, null, null, stale));
        await act.Should().ThrowAsync<RiskConcurrencyException>();

        var after = await db.PortfolioItems.AsNoTracking().OrderBy(i => i.Id)
            .Select(i => new { i.Id, i.Symbol, i.Shares, i.AverageCostBasis, i.AccountType, i.TransactionType, i.OpenDate, i.CloseDate, i.ClosingPrice, i.HoldingRole }).ToListAsync();
        after.Should().BeEquivalentTo(before, "no financial value, date or quantity may change");
    }
}
