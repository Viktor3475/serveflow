namespace ServeFlow.Domain.Entities;

public sealed class Restaurant
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = "EUR";
    public string TimeZoneId { get; set; } = "Europe/Sofia";
    public bool IsArchived { get; set; }
    public byte[] Version { get; set; } = [];
}
