using ServeFlow.Domain.Enums;

namespace ServeFlow.Domain.Entities;

public sealed class OrderStatusHistory
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid RestaurantId { get; init; }
    public Guid OrderId { get; init; }
    public Guid? ChangedByUserId { get; init; }
    public OrderStatus? PreviousStatus { get; init; }
    public OrderStatus NewStatus { get; init; }
    public DateTimeOffset ChangedAt { get; init; } = DateTimeOffset.UtcNow;
    public string? Reason { get; init; }
}
