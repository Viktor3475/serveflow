namespace ServeFlow.Domain.Entities;

public sealed class MenuItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid RestaurantId { get; init; }
    public Guid MenuCategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsAvailable { get; set; } = true;
    public string? ImageStorageKey { get; set; }
    public bool IsArchived { get; set; }
    public byte[] Version { get; set; } = [];
}
