namespace ServeFlow.Application.Analytics;

public sealed record AnalyticsPeriod(DateOnly From, DateOnly To, DateTimeOffset StartUtc, DateTimeOffset EndUtc, TimeZoneInfo Zone)
{
    public const int MaxDays = 93;
    public static AnalyticsPeriod Create(DateOnly from, DateOnly to, string timeZoneId)
    {
        if (from.Year < 2000 || to.Year > 2100 || to < from || to.DayNumber - from.DayNumber >= MaxDays)
            throw new ArgumentException("Choose a date range of 1–93 days between 2000 and 2100.");
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        return new(from, to, Boundary(from, zone), Boundary(to.AddDays(1), zone), zone);
    }
    // Choose the first real instant of a local calendar date: shift gaps forward and use the earlier instant on overlaps.
    public static DateTimeOffset Boundary(DateOnly date, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var limit = local.AddDays(2);
        while (zone.IsInvalidTime(local) && local < limit) local = local.AddMinutes(1);
        if (zone.IsInvalidTime(local)) throw new ArgumentException("The selected local date cannot be resolved.");
        var offset = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }
    public DateOnly LocalDate(DateTimeOffset instant) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, Zone).DateTime);
    public static decimal Average(decimal orderValue, int nonCancelledCount) => nonCancelledCount == 0 ? 0 : decimal.Round(orderValue / nonCancelledCount, 2, MidpointRounding.AwayFromZero);
    public IReadOnlyList<DailyMetric> Series(IReadOnlyList<(DateTimeOffset Instant, decimal Revenue)> payments, IReadOnlyList<(DateTimeOffset Instant, int Orders)> orders)
    {
        var money = payments.Where(x => x.Instant >= StartUtc && x.Instant < EndUtc).GroupBy(x => LocalDate(x.Instant)).ToDictionary(x => x.Key, x => x.Sum(p => p.Revenue));
        var counts = orders.Where(x => x.Instant >= StartUtc && x.Instant < EndUtc).GroupBy(x => LocalDate(x.Instant)).ToDictionary(x => x.Key, x => x.Sum(o => o.Orders));
        return Enumerable.Range(0, To.DayNumber - From.DayNumber + 1).Select(i => From.AddDays(i))
            .Select(date => new DailyMetric(date, money.GetValueOrDefault(date), counts.GetValueOrDefault(date))).ToArray();
    }
}
