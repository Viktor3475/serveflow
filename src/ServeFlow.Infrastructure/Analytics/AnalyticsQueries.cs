using Microsoft.EntityFrameworkCore;
using ServeFlow.Application.Analytics;
using ServeFlow.Domain.Entities;

namespace ServeFlow.Infrastructure.Analytics;

public sealed record RevenueBucket(int Minute, decimal Amount);
public sealed record OrderBucket(int Minute, int Count);
public sealed record OrderTotals(int Count, int Cancelled, decimal Value);
public static class AnalyticsQueries
{
    public static readonly DateTimeOffset Anchor = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
    public static IQueryable<RevenueBucket> Revenue(IQueryable<Payment> payments) => payments.GroupBy(x => EF.Functions.DateDiffMinute(Anchor, x.PaidAt!.Value))
        .Select(x => new RevenueBucket(x.Key, x.Sum(p => p.Amount)));
    public static IQueryable<OrderBucket> Volume(IQueryable<Order> orders) => orders.GroupBy(x => EF.Functions.DateDiffMinute(Anchor, x.CreatedAt))
        .Select(x => new OrderBucket(x.Key, x.Count()));
    public static IQueryable<OrderTotals> Totals(IQueryable<Order> orders) => orders.GroupBy(_ => 1)
        .Select(x => new OrderTotals(x.Count(), x.Count(o => o.Status == Domain.Enums.OrderStatus.Cancelled), x.Sum(o => o.Status == Domain.Enums.OrderStatus.Cancelled ? 0m : o.Total)));
}
