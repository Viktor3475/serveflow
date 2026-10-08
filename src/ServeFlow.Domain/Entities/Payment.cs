using ServeFlow.Domain.Enums;

namespace ServeFlow.Domain.Entities;

public sealed class Payment
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid RestaurantId { get; init; }
    public Guid OrderId { get; init; }
    public decimal Amount { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public PaymentMethod Method { get; init; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? Provider { get; init; }
    public string? ProviderReference { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PaidAt { get; set; }
    public byte[] Version { get; set; } = [];
}
