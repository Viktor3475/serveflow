using Microsoft.EntityFrameworkCore;
using ServeFlow.Application.Analytics;
using ServeFlow.Domain.Enums;
using ServeFlow.Infrastructure.Analytics;
using ServeFlow.Infrastructure.Persistence;

namespace ServeFlow.UnitTests;

public sealed class AnalyticsTests
{
    [Theory]
    [InlineData(2026, 3, 29, 23)] [InlineData(2026, 10, 25, 25)] [InlineData(2026, 10, 8, 24)]
    public void SofiaCalendarDaysUseActualDstDuration(int year, int month, int day, int hours)
    {
        var date = new DateOnly(year, month, day); var period = AnalyticsPeriod.Create(date, date, "Europe/Sofia");
        Assert.Equal(hours, (period.EndUtc - period.StartUtc).TotalHours);
    }
    [Fact]
    public void RangeIncludesStartAndExcludesNextMidnightAndFillsEmptyDates()
    {
        var period = AnalyticsPeriod.Create(new(2026, 10, 7), new(2026, 10, 8), "Europe/Sofia");
        var days = period.Series([(period.StartUtc.AddTicks(-1), 500m), (period.StartUtc, 12.34m), (period.EndUtc.AddTicks(-1), 7.66m), (period.EndUtc, 900m)], [(period.StartUtc, 2), (period.EndUtc, 9)]);
        Assert.Equal(2, days.Count); Assert.Equal(12.34m, days[0].Revenue); Assert.Equal(7.66m, days[1].Revenue); Assert.Equal(2, days[0].Orders); Assert.Equal(0, days[1].Orders);
    }
    [Fact]
    public void FractionalOffsetCalendarBoundaryRemainsExact()
    {
        var period = AnalyticsPeriod.Create(new(2026, 10, 8), new(2026, 10, 8), "Asia/Kathmandu");
        Assert.Equal(new DateTimeOffset(2026, 10, 7, 18, 15, 0, TimeSpan.Zero), period.StartUtc);
    }
    [Fact]
    public void MidnightGapMovesToFirstRealInstant()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("TestMidnightGap", TimeSpan.Zero, "Test", "Standard", "Summer", [TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(new(2026, 1, 1), new(2026, 12, 31), TimeSpan.FromHours(1),
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(new(1, 1, 1, 0, 0, 0), 3, 1), TimeZoneInfo.TransitionTime.CreateFixedDateRule(new(1, 1, 1, 2, 0, 0), 10, 1))]);
        Assert.Equal(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), AnalyticsPeriod.Boundary(new(2026, 3, 1), zone));
    }
    [Theory]
    [InlineData(0, 0, 0)] [InlineData(10, 3, 3.33)] [InlineData(1.005, 1, 1.01)]
    public void AverageOrderValueUsesDecimalAndDefinedRounding(decimal value, int count, decimal expected) => Assert.Equal(expected, AnalyticsPeriod.Average(value, count));
    [Fact] public void EmptyOccupancyHasZeroPercent() => Assert.Equal(0m, new Occupancy(0, 0, 0).Percent);
    [Fact] public void OccupancyIncludesReservedTablesInDenominator() => Assert.Equal(25m, new Occupancy(2, 1, 1).Percent);
    [Fact] public void OverlongRangesAreRejected() => Assert.Throws<ArgumentException>(() => AnalyticsPeriod.Create(new(2026, 1, 1), new(2026, 4, 4), "UTC"));
    [Fact] public void ReversedRangesAreRejected() => Assert.Throws<ArgumentException>(() => AnalyticsPeriod.Create(new(2026, 10, 8), new(2026, 10, 1), "UTC"));
    [Fact] public void InvalidTimeZoneIsRejected() => Assert.Throws<TimeZoneNotFoundException>(() => AnalyticsPeriod.Create(new(2026, 10, 8), new(2026, 10, 8), "Invalid/Test"));
    [Fact]
    public void SqlRevenueAndOrderAggregationTranslateWithoutExecutingADatabase()
    {
        using var db = new ServeFlowDbContext(new DbContextOptionsBuilder<ServeFlowDbContext>().UseSqlServer("Server=localhost;Database=QueryOnly;Integrated Security=true;Encrypt=true").Options);
        var restaurant = Guid.NewGuid(); var start = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero); var end = start.AddDays(7);
        var payments = db.Payments.Where(x => x.RestaurantId == restaurant && x.Status == PaymentStatus.Succeeded && x.PaidAt >= start && x.PaidAt < end);
        var money = AnalyticsQueries.Revenue(payments).ToQueryString();
        Assert.Contains("DATEDIFF(minute", money); Assert.Contains("SUM(", money); Assert.Contains("Succeeded", money); Assert.Contains("GROUP BY", money);
        var orders = db.Orders.Where(x => x.RestaurantId == restaurant && x.CreatedAt >= start && x.CreatedAt < end);
        Assert.Contains("GROUP BY", AnalyticsQueries.Volume(orders).ToQueryString());
        Assert.Contains("CASE", AnalyticsQueries.Totals(orders).ToQueryString());
    }
}
