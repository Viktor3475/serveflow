namespace ServeFlow.Domain.Entities;

public sealed class OrderItemOption
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid RestaurantId { get; init; }
    public Guid OrderItemId { get; init; }
    public Guid MenuItemId { get; init; }
    public Guid MenuItemOptionId { get; init; }
    public string NameSnapshot { get; init; } = string.Empty;
    public decimal AdditionalPriceSnapshot { get; init; }
}
