using ServeFlow.Domain.Enums;

namespace ServeFlow.Application.Analytics;

public sealed record AnalyticsFilter(DateOnly? From, DateOnly? To);
public sealed record DailyMetric(DateOnly Date, decimal Revenue, int Orders);
public sealed record PopularProduct(Guid Id, string Name, int Units);
public sealed record RecentOrder(Guid Id, string Number, int TableNumber, OrderStatus Status, DateTimeOffset CreatedAt, decimal Total, decimal Collected);
public sealed record Occupancy(int Available, int Occupied, int Reserved)
{
    public int Total => Available + Occupied + Reserved;
    public decimal Percent => Total == 0 ? 0 : decimal.Round(100m * Occupied / Total, 1);
}
public sealed record DashboardData(string RestaurantName, string CurrencyCode, string TimeZoneId, DateOnly From, DateOnly To,
    DateTimeOffset RefreshedAt, decimal Revenue, decimal TodayRevenue, int OrderCount, int CancelledCount, decimal AverageOrderValue,
    Occupancy Tables, IReadOnlyList<DailyMetric> Days, IReadOnlyList<PopularProduct> Popular, IReadOnlyList<RecentOrder> Recent);
public sealed record DashboardResult(bool Allowed, DashboardData? Data, string? Error = null);
public interface IAnalyticsService
{
    Task<DashboardResult> ReadAsync(Guid actor, AnalyticsFilter filter, CancellationToken token);
}
