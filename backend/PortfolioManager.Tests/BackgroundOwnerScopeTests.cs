using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Tests;

/// <summary>
/// The unattended EOD run has no HTTP user. Its refresh must still see the owner's own rows, otherwise the
/// portfolio snapshot loses those holdings and their recommendations can never be revalidated.
/// </summary>
public class BackgroundOwnerScopeTests
{
    private const string Owner = "owner-1";

    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task PortfolioGetAllForUser_ReturnsOwnAndSharedRows_WithoutAnHttpUser()
    {
        var db = NewDb();
        db.PortfolioItems.AddRange(
            new PortfolioItem { Symbol = "SHARED.TO", UserId = null },
            new PortfolioItem { Symbol = "MINE.TO", UserId = Owner },
            new PortfolioItem { Symbol = "THEIRS.TO", UserId = "someone-else" });
        await db.SaveChangesAsync();
        var svc = new PortfolioService(db, null!, null!, new HttpContextAccessor());

        Assert.Equal(["SHARED.TO"], (await svc.GetAllAsync()).Select(i => i.Symbol));
        Assert.Equal(["MINE.TO", "SHARED.TO"], (await svc.GetAllForUserAsync(Owner)).Select(i => i.Symbol));
    }

    [Fact]
    public async Task WatchlistGetAllForUser_ReturnsOwnAndSharedRows_WithoutAnHttpUser()
    {
        var db = NewDb();
        db.WatchlistItems.AddRange(
            new WatchlistItem { Symbol = "SHARED", UserId = null },
            new WatchlistItem { Symbol = "MINE", UserId = Owner },
            new WatchlistItem { Symbol = "THEIRS", UserId = "someone-else" });
        await db.SaveChangesAsync();
        var svc = new WatchlistService(db, new HttpContextAccessor());

        Assert.Equal(["SHARED"], (await svc.GetAllAsync()).Select(i => i.Symbol));
        Assert.Equal(["MINE", "SHARED"], (await svc.GetAllForUserAsync(Owner)).Select(i => i.Symbol));
    }
}
