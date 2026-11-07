using PortfolioManager.Api.Services;
using Xunit;

namespace PortfolioManager.Tests;

public sealed class TradingCalendarTests
{
    private static DateOnly D(int y, int m, int d) => new(y, m, d);
    private static DateTime Utc(int y, int m, int d, int h, int min) => new(y, m, d, h, min, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("BRK.TO", null, MarketExchange.Tsx)]
    [InlineData("abc.v", null, MarketExchange.Tsx)]
    [InlineData("AAPL", null, MarketExchange.Nyse)]
    [InlineData("AAPL", "TSX", MarketExchange.Tsx)]
    [InlineData(null, null, MarketExchange.Nyse)]
    public void ExchangeFor_UsesSuffixThenMarket(string? symbol, string? market, MarketExchange expected) =>
        Assert.Equal(expected, TradingCalendar.ExchangeFor(symbol, market));

    [Fact]
    public void Tsx_2026_Holidays()
    {
        var h = TradingCalendar.Holidays(2026, MarketExchange.Tsx);
        foreach (var d in new[] { D(2026, 1, 1), D(2026, 2, 16), D(2026, 4, 3), D(2026, 5, 18), D(2026, 7, 1), D(2026, 8, 3),
                     D(2026, 9, 7), D(2026, 10, 12), D(2026, 12, 25), D(2026, 12, 28) })
            Assert.Contains(d, h);
        Assert.DoesNotContain(D(2026, 6, 19), h);          // Juneteenth is US only
        Assert.DoesNotContain(D(2026, 11, 26), h);         // US Thanksgiving
    }

    [Fact]
    public void Nyse_2026_Holidays()
    {
        var h = TradingCalendar.Holidays(2026, MarketExchange.Nyse);
        foreach (var d in new[] { D(2026, 1, 1), D(2026, 1, 19), D(2026, 2, 16), D(2026, 4, 3), D(2026, 5, 25), D(2026, 6, 19),
                     D(2026, 7, 3), D(2026, 9, 7), D(2026, 11, 26), D(2026, 12, 25) })
            Assert.Contains(d, h);
        Assert.DoesNotContain(D(2026, 5, 18), h);          // Victoria Day is Canadian
        Assert.DoesNotContain(D(2026, 10, 12), h);         // Canadian Thanksgiving
    }

    [Fact]
    public void GoodFriday_2027_And_WeekendHolidayObservance()
    {
        Assert.Contains(D(2027, 3, 26), TradingCalendar.Holidays(2027, MarketExchange.Tsx));
        // 2028: Jan 1 is a Saturday. TSX observes Monday Jan 3; NYSE does not observe it at all (Dec 31 2027 trades).
        Assert.Contains(D(2028, 1, 3), TradingCalendar.Holidays(2028, MarketExchange.Tsx));
        Assert.DoesNotContain(D(2028, 1, 3), TradingCalendar.Holidays(2028, MarketExchange.Nyse));
        Assert.True(TradingCalendar.IsTradingDay(D(2027, 12, 31), MarketExchange.Nyse));
    }

    [Fact]
    public void Tuesday_Morning_ShowsMondaysSession_AsFresh_NotTheCalendarDate()
    {
        Assert.Equal(D(2026, 10, 26), TradingCalendar.LastCompletedSession(Utc(2026, 10, 27, 12, 0), MarketExchange.Tsx));   // 08:00 EDT
    }

    [Fact]
    public void SessionCompletes_OnlyAfterCloseAndGrace()
    {
        Assert.Equal(D(2026, 10, 26), TradingCalendar.LastCompletedSession(Utc(2026, 10, 27, 20, 5), MarketExchange.Nyse));   // 16:05 EDT
        Assert.Equal(D(2026, 10, 27), TradingCalendar.LastCompletedSession(Utc(2026, 10, 27, 22, 5), MarketExchange.Nyse));   // 18:05 EDT
    }

    [Fact]
    public void WeekendAndHolidays_PointToTheLastRealSession()
    {
        Assert.Equal(D(2026, 10, 23), TradingCalendar.LastCompletedSession(Utc(2026, 10, 24, 16, 0), MarketExchange.Tsx));   // Saturday
        Assert.Equal(D(2026, 4, 2), TradingCalendar.LastCompletedSession(Utc(2026, 4, 6, 12, 0), MarketExchange.Tsx));       // Monday after Good Friday
        Assert.Equal(D(2026, 10, 9), TradingCalendar.LastCompletedSession(Utc(2026, 10, 13, 12, 0), MarketExchange.Tsx));    // TSX closed Mon Oct 12
        Assert.Equal(D(2026, 10, 12), TradingCalendar.LastCompletedSession(Utc(2026, 10, 13, 12, 0), MarketExchange.Nyse));  // NYSE traded
    }

    [Fact]
    public void YearBoundary_SkipsNewYearHoliday()
    {
        Assert.Equal(D(2026, 12, 31), TradingCalendar.LastCompletedSession(Utc(2027, 1, 4, 11, 0), MarketExchange.Tsx));   // Mon 06:00 EST: Jan 1 closed, prior session Thu
        Assert.Equal(1, TradingCalendar.TradingDaysBetween(D(2026, 12, 31), D(2027, 1, 4), MarketExchange.Tsx));
    }

    [Fact]
    public void DstChange_ShiftsTheUtcCutoffButNotTheLocalOne()
    {
        // Fri Oct 30 (EDT): 18:05 local = 22:05Z. Mon Nov 2 (EST, DST ended Nov 1): 18:05 local = 23:05Z.
        Assert.Equal(D(2026, 10, 30), TradingCalendar.LastCompletedSession(Utc(2026, 10, 30, 22, 5), MarketExchange.Tsx));
        Assert.Equal(D(2026, 10, 30), TradingCalendar.LastCompletedSession(Utc(2026, 11, 2, 22, 55), MarketExchange.Tsx));   // 17:55 EST, not yet
        Assert.Equal(D(2026, 11, 2), TradingCalendar.LastCompletedSession(Utc(2026, 11, 2, 23, 5), MarketExchange.Tsx));
        // Spring forward (Mar 8 2026): Mon Mar 9 18:05 EDT = 22:05Z
        Assert.Equal(D(2026, 3, 9), TradingCalendar.LastCompletedSession(Utc(2026, 3, 9, 22, 5), MarketExchange.Nyse));
        Assert.Equal(D(2026, 3, 6), TradingCalendar.LastCompletedSession(Utc(2026, 3, 9, 21, 55), MarketExchange.Nyse));
    }

    [Fact]
    public void EarlyClose_ShortensTheEvidenceWindow()
    {
        Assert.True(TradingCalendar.IsEarlyClose(D(2026, 11, 27), MarketExchange.Nyse));
        Assert.False(TradingCalendar.IsEarlyClose(D(2026, 11, 27), MarketExchange.Tsx));
        Assert.True(TradingCalendar.IsEarlyClose(D(2026, 12, 24), MarketExchange.Tsx));
        Assert.False(TradingCalendar.IsEarlyClose(D(2026, 7, 3), MarketExchange.Nyse));     // that day is a full holiday in 2026
        Assert.Equal(new TimeOnly(13, 0), TradingCalendar.CloseTime(D(2026, 11, 27), MarketExchange.Nyse));
        // Close 13:00 + 2h grace = 15:00 EST (UTC-5)
        Assert.Equal(D(2026, 11, 27), TradingCalendar.LastCompletedSession(Utc(2026, 11, 27, 20, 5), MarketExchange.Nyse));
        Assert.Equal(D(2026, 11, 25), TradingCalendar.LastCompletedSession(Utc(2026, 11, 27, 19, 55), MarketExchange.Nyse));
    }

    [Fact]
    public void TradingDaysBetween_IsHolidayAware_UnlikeWeekdayCounting()
    {
        Assert.Equal(1, TradingCalendar.TradingDaysBetween(D(2026, 10, 9), D(2026, 10, 13), MarketExchange.Tsx));    // Mon Oct 12 closed
        Assert.Equal(2, TradingCalendar.TradingDaysBetween(D(2026, 10, 9), D(2026, 10, 13), MarketExchange.Nyse));
        Assert.Equal(0, TradingCalendar.TradingDaysBetween(D(2026, 10, 13), D(2026, 10, 13), MarketExchange.Tsx));
        Assert.Equal(1, TradingCalendar.TradingDaysBetween(D(2026, 4, 2), D(2026, 4, 6), MarketExchange.Tsx));       // over Good Friday
    }

    [Fact]
    public void EvidenceGraceBoundary_IsExactToTheSecond_OnRegularAndEarlyCloseDays()
    {
        // Regular close 16:00 EDT + 2 h = 18:00 EDT = 22:00Z.
        Assert.Equal(D(2026, 10, 26), TradingCalendar.LastCompletedSession(new DateTime(2026, 10, 27, 21, 59, 59, DateTimeKind.Utc), MarketExchange.Tsx));
        Assert.Equal(D(2026, 10, 27), TradingCalendar.LastCompletedSession(new DateTime(2026, 10, 27, 22, 0, 0, DateTimeKind.Utc), MarketExchange.Tsx));
        // Early close 13:00 EST + 2 h = 15:00 EST = 20:00Z (NYSE, day after US Thanksgiving).
        Assert.Equal(D(2026, 11, 25), TradingCalendar.LastCompletedSession(new DateTime(2026, 11, 27, 19, 59, 59, DateTimeKind.Utc), MarketExchange.Nyse));
        Assert.Equal(D(2026, 11, 27), TradingCalendar.LastCompletedSession(new DateTime(2026, 11, 27, 20, 0, 0, DateTimeKind.Utc), MarketExchange.Nyse));
        // DST start (Mar 14 2027): Mon Mar 15 18:00 EDT = 22:00Z; the same wall-clock time the Friday before (EST) was 23:00Z.
        Assert.Equal(D(2027, 3, 12), TradingCalendar.LastCompletedSession(new DateTime(2027, 3, 15, 21, 59, 59, DateTimeKind.Utc), MarketExchange.Nyse));
        Assert.Equal(D(2027, 3, 15), TradingCalendar.LastCompletedSession(new DateTime(2027, 3, 15, 22, 0, 0, DateTimeKind.Utc), MarketExchange.Nyse));
        Assert.Equal(D(2027, 3, 11), TradingCalendar.LastCompletedSession(new DateTime(2027, 3, 12, 22, 59, 59, DateTimeKind.Utc), MarketExchange.Nyse));   // Fri 17:59 EST
        Assert.Equal(D(2027, 3, 12), TradingCalendar.LastCompletedSession(new DateTime(2027, 3, 12, 23, 0, 0, DateTimeKind.Utc), MarketExchange.Nyse));      // Fri 18:00 EST
    }

    [Fact]
    public void ValidPriorSessionEvidence_IsNotStaleNextMorning_NorOverWeekendsAndHolidays()
    {
        // Evidence for the Monday session is the *latest completed* one all through Tuesday morning, and through Sat/Sun.
        var monday = D(2026, 10, 26);
        foreach (var utc in new[] { new DateTime(2026, 10, 27, 6, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 27, 13, 30, 0, DateTimeKind.Utc), new DateTime(2026, 10, 27, 21, 0, 0, DateTimeKind.Utc) })
            Assert.Equal(monday, TradingCalendar.LastCompletedSession(utc, MarketExchange.Tsx));
        Assert.Equal(D(2026, 10, 30), TradingCalendar.LastCompletedSession(new DateTime(2026, 11, 1, 15, 0, 0, DateTimeKind.Utc), MarketExchange.Tsx));      // Sunday
        Assert.Equal(D(2027, 3, 25), TradingCalendar.LastCompletedSession(new DateTime(2027, 3, 29, 12, 0, 0, DateTimeKind.Utc), MarketExchange.Tsx));      // Mon after Good Friday 2027
    }

    [Fact]
    public void NewYearObservance_AcrossTheYearBoundary_DiffersByExchange()
    {
        // 2028-01-01 is a Saturday: TSX closes Mon Jan 3; NYSE does not observe it and traded Fri Dec 31 2027.
        Assert.Equal(D(2027, 12, 31), TradingCalendar.LastCompletedSession(new DateTime(2028, 1, 4, 13, 0, 0, DateTimeKind.Utc), MarketExchange.Tsx));
        Assert.Equal(D(2028, 1, 3), TradingCalendar.LastCompletedSession(new DateTime(2028, 1, 4, 13, 0, 0, DateTimeKind.Utc), MarketExchange.Nyse));
    }

    [Theory]
    [InlineData("AAPL", "Toronto", MarketExchange.Tsx, TradingCalendar.ExchangeBasis.Metadata)]
    [InlineData("SHOP.TO", "NasdaqGS", MarketExchange.Nyse, TradingCalendar.ExchangeBasis.Metadata)]   // analysis security trades in the US
    [InlineData("BRK.TO", "", MarketExchange.Tsx, TradingCalendar.ExchangeBasis.Suffix)]
    [InlineData("BRK.TO", null, MarketExchange.Tsx, TradingCalendar.ExchangeBasis.Suffix)]
    [InlineData("AAPL", null, MarketExchange.Nyse, TradingCalendar.ExchangeBasis.Assumed)]
    [InlineData("CABA", "unknown-market", MarketExchange.Nyse, TradingCalendar.ExchangeBasis.Assumed)]
    public void ExchangeResolution_PrefersMetadata_ThenSuffix_AndNeverHidesAnAssumption(
        string symbol, string? market, MarketExchange exchange, TradingCalendar.ExchangeBasis basis)
    {
        var r = TradingCalendar.Resolve(symbol, market);
        Assert.Equal(exchange, r.Exchange);
        Assert.Equal(basis, r.Basis);
    }

    [Fact]
    public void HolidayTable_CoversCurrentAndNextYear()
    {
        var year = DateTime.UtcNow.Year;
        foreach (var y in new[] { year, year + 1 })
        {
            Assert.True(TradingCalendar.HasHolidayTable(y), $"No holiday table for {y}; extend TradingCalendar.");
            Assert.NotEmpty(TradingCalendar.Holidays(y, MarketExchange.Tsx));
            Assert.NotEmpty(TradingCalendar.Holidays(y, MarketExchange.Nyse));
        }
    }
}
