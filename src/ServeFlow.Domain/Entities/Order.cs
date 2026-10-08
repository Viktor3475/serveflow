using ServeFlow.Domain.Enums;

namespace ServeFlow.Domain.Entities;

public sealed class Order
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid RestaurantId { get; init; }
    public Guid RestaurantTableId { get; init; }
    public Guid? CreatedByUserId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string IdempotencyKey { get; init; } = string.Empty;
    public string RequestFingerprint { get; init; } = string.Empty;
    public string ReceiptTokenHash { get; init; } = string.Empty;
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal Total { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public byte[] Version { get; set; } = [];
    public ICollection<OrderItem> Items { get; init; } = new List<OrderItem>();
    public ICollection<Payment> Payments { get; init; } = new List<Payment>();
    public ICollection<OrderStatusHistory> StatusHistory { get; init; } = new List<OrderStatusHistory>();
}
