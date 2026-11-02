using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;
using PortfolioManager.Api.Services.Risk;

namespace PortfolioManager.Tests;

// Backlog #18: new CLOSE rows and OPEN->CLOSE edits require a valid Close Date. Synthetic data, EF InMemory.
public class Pm018CloseDateValidationTests
{
    private sealed class TestAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }

    private static readonly DateTime OpenDate = new(2026, 9, 1);

    private static (AppDbContext Db, PortfolioService Svc) Setup()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        Pm033RiskTests.SimulateRowVersions(db);
        db.AccountTypes.Add(new AccountType { Name = "A", NormalizedName = "A" });
        db.SaveChanges();
        var accessor = new TestAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, "u1"), new Claim(ClaimTypes.Role, "Trader")], "test")),
            },
        };
        return (db, new PortfolioService(db, null!, new MutationClock(), accessor));
    }

    private static AddPortfolioItemRequest Add(string type, DateTime? close = null, DateTime? open = null) =>
        new("SYNTH.TO", "Synthetic", 100, 10, type, "A", open ?? OpenDate, close, type == "CLOSE" ? 12 : null,
            "Manual", "Swing", type == "CLOSE" ? "Risk Control" : null);

    private static UpdatePortfolioItemRequest Update(string type, DateTime? close, decimal shares = 100) =>
        new("Synthetic", shares, 10, TransactionType: type, AccountType: "A", OpenDate: OpenDate,
            CloseDate: close, ClosingPrice: type == "CLOSE" ? 12 : null, HoldingRole: "Swing");

    [Fact]
    public async Task New_open_without_close_date_is_allowed()
    {
        var (db, svc) = Setup();
        await using var _ = db;
        (await svc.AddAsync(Add("OPEN"))).Should().NotBeNull();
    }

    [Fact]
    public async Task New_close_without_close_date_is_rejected()
    {
        var (db, svc) = Setup();
        await using var _ = db;
        var act = () => svc.AddAsync(Add("CLOSE"));
        await act.Should().ThrowAsync<RiskValidationException>().WithMessage("*requires a Close Date*");
        (await db.PortfolioItems.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task New_close_before_open_is_rejected()
    {
        var (db, svc) = Setup();
        await using var _ = db;
        var act = () => svc.AddAsync(Add("CLOSE", OpenDate.AddDays(-1)));
        await act.Should().ThrowAsync<RiskValidationException>().WithMessage("*cannot close before*");
    }

    [Fact]
    public async Task New_close_with_date_persists_both_decision_sources()
    {
        var (db, svc) = Setup();
        await using var _ = db;
        var dto = await svc.AddAsync(Add("CLOSE", OpenDate.AddDays(3)));
        var row = await db.PortfolioItems.AsNoTracking().SingleAsync(x => x.Id == dto.Id);
        row.CloseDate.Should().Be(OpenDate.AddDays(3));
        row.DecisionSource.Should().Be("Manual");
        row.DecisionSourceClosed.Should().Be("Risk Control");
    }

    [Fact]
    public async Task Open_to_close_without_date_is_rejected_and_row_unchanged()
    {
        var (db, svc) = Setup();
        await using var _ = db;
        var added = await svc.AddAsync(Add("OPEN"));
        var act = () => svc.UpdateAsync(added.Id, Update("CLOSE", null));
        await act.Should().ThrowAsync<RiskValidationException>().WithMessage("*requires a Close Date*");
        db.ChangeTracker.Clear();
        (await db.PortfolioItems.AsNoTracking().SingleAsync(x => x.Id == added.Id)).TransactionType.Should().Be("OPEN");
    }

    [Fact]
    public async Task Open_to_close_with_date_is_allowed()
    {
        var (db, svc) = Setup();
        await using var _ = db;
        var added = await svc.AddAsync(Add("OPEN"));
        (await svc.UpdateAsync(added.Id, Update("CLOSE", OpenDate.AddDays(2)))).Should().NotBeNull();
    }

    [Fact]
    public async Task Legacy_close_with_null_date_can_be_corrected_but_not_resized_without_a_date()
    {
        var (db, svc) = Setup();
        await using var _ = db;
        var added = await svc.AddAsync(Add("CLOSE", OpenDate.AddDays(1)));
        var row = await db.PortfolioItems.FindAsync(added.Id);
        row!.CloseDate = null;
        await db.SaveChangesAsync();

        (await svc.UpdateAsync(added.Id, Update("CLOSE", null))).Should().NotBeNull("unchanged legacy row stays editable");
        (await svc.UpdateAsync(added.Id, Update("CLOSE", OpenDate.AddDays(4)))).Should().NotBeNull("NULL -> date correction (#21)");
        (await db.PortfolioItems.AsNoTracking().SingleAsync(x => x.Id == added.Id)).CloseDate.Should().Be(OpenDate.AddDays(4));
    }

    [Fact]
    public async Task Legacy_close_with_changed_shares_requires_a_date()
    {
        var (db, svc) = Setup();
        await using var _ = db;
        var added = await svc.AddAsync(Add("CLOSE", OpenDate.AddDays(1)));
        var row = await db.PortfolioItems.FindAsync(added.Id);
        row!.CloseDate = null;
        await db.SaveChangesAsync();
        var act = () => svc.UpdateAsync(added.Id, Update("CLOSE", null, shares: 50));
        await act.Should().ThrowAsync<RiskValidationException>().WithMessage("*requires a Close Date*");
    }
}
