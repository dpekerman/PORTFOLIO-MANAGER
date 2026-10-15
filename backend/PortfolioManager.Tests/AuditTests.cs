using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Tests;

public class AuditTests
{
    private static (AppDbContext db, HttpContextAccessor http) Create(string? userId = "u1", string role = "User")
    {
        var http = new HttpContextAccessor();
        if (userId is not null)
        {
            var id = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Name, userId + "-name"),
                 new Claim(ClaimTypes.Role, role)], "test");
            http.HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(id) };
        }
        var interceptor = new AuditSaveChangesInterceptor(http, NullLogger<AuditSaveChangesInterceptor>.Instance);
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).AddInterceptors(interceptor).Options);
        return (db, http);
    }

    [Fact]
    public async Task Add_Update_Delete_AreAudited_WithOldNewValues()
    {
        var (db, _) = Create();
        var item = new OptionItem { UnderlyingTicker = "AAPL", PositionType = "CALL", Strike = 100, MarketPrice = 1.5m };
        db.OptionItems.Add(item);
        await db.SaveChangesAsync();

        item.MarketPrice = 2.25m;
        await db.SaveChangesAsync();

        db.OptionItems.Remove(item);
        await db.SaveChangesAsync();

        var logs = await db.AuditLogs.OrderBy(x => x.Id).ToListAsync();
        logs.Select(l => l.Action).Should().Equal("Create", "Update", "Delete");
        logs.Should().OnlyContain(l => l.Symbol == "AAPL" && l.Category == "Option" && l.Source == "User");
        logs[1].ChangesJson.Should().Contain("MarketPrice").And.Contain("1.5").And.Contain("2.25");
        logs[0].EntityId.Should().Be(item.Id.ToString());
    }

    [Fact]
    public async Task NoOpUpdate_IsNotAudited()
    {
        var (db, _) = Create();
        var item = new PortfolioItem { Symbol = "MSFT", CompanyName = "Microsoft", Shares = 10 };
        db.PortfolioItems.Add(item);
        await db.SaveChangesAsync();

        item.Shares = 10;
        await db.SaveChangesAsync();

        (await db.AuditLogs.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task NonWhitelistedEntity_IsNotAudited()
    {
        var (db, _) = Create();
        db.RefreshTokens.Add(new RefreshToken { Token = "x", UserId = "u1" });
        await db.SaveChangesAsync();
        (await db.AuditLogs.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task NoUser_IsTaggedSystem()
    {
        var (db, _) = Create(userId: null);
        db.CashItems.Add(new CashItem());
        await db.SaveChangesAsync();
        var log = await db.AuditLogs.SingleAsync();
        log.Source.Should().Be("System");
        log.UserId.Should().BeNull();
    }

    [Fact]
    public async Task Query_ScopesNonAdminToOwnRows_AndAdminSeesAll()
    {
        var (db, http) = Create("u1");
        db.AuditLogs.AddRange(
            new AuditLog { UserId = "u1", Category = "Stock", Action = "Create", EntityType = "PortfolioItem", Summary = "a", Symbol = "AAA" },
            new AuditLog { UserId = "u2", Category = "Cash", Action = "Update", EntityType = "CashItem", Summary = "b" });
        await db.SaveChangesAsync();

        var svc = new AuditService(db, http, NullLogger<AuditService>.Instance);
        (await svc.QueryAsync(new AuditQuery())).Items.Should().ContainSingle(x => x.UserId == "u1");

        var admin = Create("admin", "Admin");
        admin.db.AuditLogs.AddRange(
            new AuditLog { UserId = "u1", Category = "Stock", Action = "Create", EntityType = "T", Summary = "a" },
            new AuditLog { UserId = "u2", Category = "Cash", Action = "Update", EntityType = "T", Summary = "b" });
        await admin.db.SaveChangesAsync();
        var adminSvc = new AuditService(admin.db, admin.http, NullLogger<AuditService>.Instance);
        (await adminSvc.QueryAsync(new AuditQuery())).Total.Should().Be(2);
        (await adminSvc.QueryAsync(new AuditQuery(Category: ["Cash"], SortBy: "category", SortDir: "asc")))
            .Items.Should().ContainSingle();
    }
}
