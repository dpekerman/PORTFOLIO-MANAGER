using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Tests;

public sealed class AppDbContextUtcConverterTests
{
    [Theory]
    [InlineData(typeof(PortfolioSnapshot), "UpdatedAt")]
    [InlineData(typeof(CashItem), "AddedAt")]
    [InlineData(typeof(CashItem), "ModifiedAt")]
    [InlineData(typeof(OptionItem), "AddedAt")]
    [InlineData(typeof(DailySignal), "ScannedAt")]
    public void RelevantSnapshotAndLedgerReadersRestoreUtcWithoutShiftingSqlTicks(Type entity, string name)
    {
        using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var converter = context.Model.FindEntityType(entity)!.FindProperty(name)!.GetValueConverter()!;
        var raw = new DateTime(2026, 11, 15, 10, 38, 22, DateTimeKind.Unspecified);
        var restored = Assert.IsType<DateTime>(converter.ConvertFromProvider(raw));
        Assert.Equal(raw.Ticks, restored.ToUniversalTime().Ticks);
        Assert.Equal(DateTimeKind.Utc, restored.Kind);
        var parsed = JsonSerializer.Deserialize<DateTime>(JsonSerializer.Serialize(restored));
        Assert.Equal(DateTimeKind.Utc, parsed.Kind);
        Assert.Equal(raw.Ticks, parsed.Ticks);
    }

    [Fact]
    public void SqlDateTime2RoundTripPreservesUtcTicksAndRestoresUtcKind()
    {
        using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        var property = context.Model.FindEntityType(typeof(OpportunitySnapshot))!
            .FindProperty(nameof(OpportunitySnapshot.ComputedAtUtc))!;
        var converter = property.GetValueConverter()!;
        var source = new DateTime(2026, 11, 15, 10, 38, 22, DateTimeKind.Utc).AddTicks(7272988);

        var providerValue = Assert.IsType<DateTime>(converter.ConvertToProvider(source));
        Assert.Equal(source.Ticks, providerValue.Ticks);

        var sqlDateTime2Value = DateTime.SpecifyKind(providerValue, DateTimeKind.Unspecified);
        var restored = Assert.IsType<DateTime>(converter.ConvertFromProvider(sqlDateTime2Value));
        Assert.Equal(source.Ticks, restored.Ticks);
        Assert.Equal(DateTimeKind.Utc, restored.Kind);

        var json = JsonSerializer.Serialize(new { ComputedAtUtc = restored },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.EndsWith("Z\"}", json);
    }
}
