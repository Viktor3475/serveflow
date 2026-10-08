namespace ServeFlow.Domain.Entities;

public sealed class MenuItemOption
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid RestaurantId { get; init; }
    public Guid MenuItemId { get; init; }
    public string Name { get; set; } = string.Empty;
    public decimal AdditionalPrice { get; set; }
    public bool IsAvailable { get; set; } = true;
    public bool IsArchived { get; set; }
    public byte[] Version { get; set; } = [];
}
