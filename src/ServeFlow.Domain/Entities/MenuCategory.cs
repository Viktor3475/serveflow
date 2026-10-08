namespace ServeFlow.Domain.Entities;

public sealed class MenuCategory
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid RestaurantId { get; init; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsArchived { get; set; }
    public byte[] Version { get; set; } = [];
}
