using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ServeFlow.Application.Analytics;
using ServeFlow.Application.Security;
using ServeFlow.Domain.Enums;
using ServeFlow.Infrastructure.Identity;
using ServeFlow.Infrastructure.Persistence;

namespace ServeFlow.Infrastructure.Analytics;

public sealed class AnalyticsService(ServeFlowDbContext db, UserManager<ApplicationUser> users, IAccountActivity activity, TimeProvider clock) : IAnalyticsService
{
    private static readonly DateTimeOffset Anchor = AnalyticsQueries.Anchor;
    public async Task<DashboardResult> ReadAsync(Guid actor, AnalyticsFilter filter, CancellationToken token)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actor, token);
        if (user is null || !await activity.IsActiveAsync(user) || !await users.IsInRoleAsync(user, StaffRoles.Administrator)) return new(false, null);
        var restaurant = await db.Restaurants.AsNoTracking().SingleAsync(x => x.Id == user.RestaurantId, token);
        var now = clock.GetUtcNow();
        AnalyticsPeriod period;
        DateOnly today;
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(restaurant.TimeZoneId);
            today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
            period = AnalyticsPeriod.Create(filter.From ?? today.AddDays(-6), filter.To ?? today, restaurant.TimeZoneId);
        }
        catch (ArgumentException exception) { return new(true, null, exception.Message); }
        catch (TimeZoneNotFoundException) { return new(true, null, "The restaurant time zone is unavailable. Update it in Settings."); }
        catch (InvalidTimeZoneException) { return new(true, null, "The restaurant time zone is invalid. Update it in Settings."); }
        var payments = db.Payments.AsNoTracking().Where(x => x.RestaurantId == restaurant.Id && x.CurrencyCode == restaurant.CurrencyCode && x.Status == PaymentStatus.Succeeded && x.PaidAt != null);
        var selectedPayments = payments.Where(x => x.PaidAt >= period.StartUtc && x.PaidAt < period.EndUtc);
        // Aggregate in SQL by UTC minute, then assign bounded minute buckets to calendar dates using the application's time-zone rules.
        // This avoids loading individual transactions and avoids relying on SQL Server's Windows time-zone catalog for IANA zones.
        var cashBuckets = await AnalyticsQueries.Revenue(selectedPayments).ToListAsync(token);
        var orders = db.Orders.AsNoTracking().Where(x => x.RestaurantId == restaurant.Id && x.CurrencyCode == restaurant.CurrencyCode && x.CreatedAt >= period.StartUtc && x.CreatedAt < period.EndUtc);
        var orderBuckets = await AnalyticsQueries.Volume(orders).ToListAsync(token);
        var counts = await AnalyticsQueries.Totals(orders).SingleOrDefaultAsync(token);
        var todayPeriod = AnalyticsPeriod.Create(today, today, restaurant.TimeZoneId);
        var todayRevenue = await payments.Where(x => x.PaidAt >= todayPeriod.StartUtc && x.PaidAt < todayPeriod.EndUtc).SumAsync(x => (decimal?)x.Amount, token) ?? 0m;
        var lines = from item in db.OrderItems.AsNoTracking() join order in orders on item.OrderId equals order.Id
            where item.RestaurantId == restaurant.Id && order.Status != OrderStatus.Cancelled select item;
        var popularity = lines.GroupBy(x => x.MenuItemId).Select(x => new { Id = x.Key, Units = x.Sum(i => i.Quantity) });
        var popular = await (from rank in popularity join item in db.MenuItems.AsNoTracking() on rank.Id equals item.Id
            where item.RestaurantId == restaurant.Id orderby rank.Units descending, item.Name, item.Id
            select new PopularProduct(item.Id, item.Name, rank.Units)).Take(10).ToListAsync(token);
        var recent = await (from order in orders join table in db.RestaurantTables.AsNoTracking() on order.RestaurantTableId equals table.Id
            where table.RestaurantId == restaurant.Id orderby order.CreatedAt descending, order.Id descending
            select new RecentOrder(order.Id, order.OrderNumber, table.Number, order.Status, order.CreatedAt, order.Total,
                db.Payments.Where(p => p.OrderId == order.Id && p.RestaurantId == restaurant.Id && p.Status == PaymentStatus.Succeeded && p.CurrencyCode == order.CurrencyCode).Sum(p => (decimal?)p.Amount) ?? 0m))
            .Take(12).ToListAsync(token);
        var tables = await db.RestaurantTables.AsNoTracking().Where(x => x.RestaurantId == restaurant.Id && !x.IsArchived)
            .GroupBy(x => x.Status).Select(x => new { Status = x.Key, Count = x.Count() }).ToListAsync(token);
        var occupancy = new Occupancy(tables.Where(x => x.Status == TableStatus.Available).Sum(x => x.Count), tables.Where(x => x.Status == TableStatus.Occupied).Sum(x => x.Count), tables.Where(x => x.Status == TableStatus.Reserved).Sum(x => x.Count));
        var days = period.Series(cashBuckets.Select(x => (Anchor.AddMinutes(x.Minute), x.Amount)).ToArray(), orderBuckets.Select(x => (Anchor.AddMinutes(x.Minute), x.Count)).ToArray());
        return new(true, new(restaurant.Name, restaurant.CurrencyCode, restaurant.TimeZoneId, period.From, period.To, now,
            days.Sum(x => x.Revenue), todayRevenue, counts?.Count ?? 0, counts?.Cancelled ?? 0, AnalyticsPeriod.Average(counts?.Value ?? 0, (counts?.Count ?? 0) - (counts?.Cancelled ?? 0)), occupancy, days, popular, recent));
    }
}
