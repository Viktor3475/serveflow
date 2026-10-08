using ServeFlow.Domain.Enums;

namespace ServeFlow.Domain.Entities;

public sealed class RestaurantTable
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid RestaurantId { get; init; }
    public int Number { get; set; }
    public int Capacity { get; set; }
    public string? QrTokenHash { get; set; }
    public TableStatus Status { get; set; } = TableStatus.Available;
    public bool IsArchived { get; set; }
    public byte[] Version { get; set; } = [];
}
