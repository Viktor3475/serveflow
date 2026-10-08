namespace ServeFlow.Domain.Entities;

public sealed class OrderItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid RestaurantId { get; init; }
    public Guid OrderId { get; init; }
    public Guid MenuItemId { get; init; }
    public string NameSnapshot { get; init; } = string.Empty;
    public decimal UnitPriceSnapshot { get; init; }
    public int Quantity { get; set; }
    public string? Instructions { get; set; }
    public ICollection<OrderItemOption> Options { get; init; } = new List<OrderItemOption>();
}
