using ServeFlow.Domain.Enums;

namespace ServeFlow.Application.Ordering;

public sealed record TableAccess(Guid TableId, string TokenHash);
public sealed record TableMenu(Guid TableId, string RestaurantName, int TableNumber, string CurrencyCode, IReadOnlyList<MenuOffer> Items);
public sealed record OptionOffer(Guid Id, string Name, decimal Price, string Version);
public sealed record MenuOffer(Guid Id, Guid CategoryId, string Category, string Name, string Description, decimal Price,
    string Version, bool Available, bool HasImage, IReadOnlyList<OptionOffer> Options);
public sealed record OptionSelection(Guid Id, string Version);
public sealed record CartLine(Guid ItemId, string Version, int Quantity, string? Instructions, IReadOnlyList<OptionSelection> Options);
public sealed record PlaceOrderRequest(string IdempotencyKey, string ReceiptToken, string? Notes, IReadOnlyList<CartLine> Lines);
public sealed record PricedLine(Guid ItemId, string Name, decimal Price, int Quantity, string? Instructions, IReadOnlyList<OptionOffer> Options)
{
    public decimal Total => (Price + Options.Sum(x => x.Price)) * Quantity;
}
public sealed record PricingResult(IReadOnlyList<PricedLine> Lines, string? Error)
{
    public bool Succeeded => Error is null;
    public decimal Total => Lines.Sum(x => x.Total);
}
public sealed record OrderCreated(Guid Id, string Number, decimal Total, string CurrencyCode, bool Replayed);
public sealed record OrderingResult(OrderCreated? Order, string? Error, bool Conflict = false, bool Forbidden = false, bool Throttled = false);
public sealed record ReceiptLine(string Name, decimal Price, int Quantity, string? Instructions, IReadOnlyList<ReceiptOption> Options);
public sealed record ReceiptOption(string Name, decimal Price);
public sealed record OrderReceipt(Guid Id, string Number, string RestaurantName, int TableNumber, string CurrencyCode,
    decimal Total, OrderStatus Status, DateTimeOffset CreatedAt, string? Notes, IReadOnlyList<ReceiptLine> Lines);
public sealed record QrIssued(string Token, int TableNumber);
public interface IOrderingService
{
    Task<TableAccess?> ExchangeAsync(string token, CancellationToken cancellationToken);
    Task<TableMenu?> MenuAsync(TableAccess access, CancellationToken cancellationToken);
    Task<TableMenu?> StaffMenuAsync(Guid actor, Guid tableId, CancellationToken cancellationToken);
    Task<OrderingResult> PlaceAsync(TableAccess access, PlaceOrderRequest request, CancellationToken cancellationToken);
    Task<OrderingResult> PlaceStaffAsync(Guid actor, Guid tableId, PlaceOrderRequest request, CancellationToken cancellationToken);
    Task<OrderReceipt?> ReceiptAsync(Guid orderId, string token, CancellationToken cancellationToken);
    Task<string?> ImageAsync(TableAccess access, Guid itemId, CancellationToken cancellationToken);
    Task<QrIssued?> RotateQrAsync(Guid actor, Guid tableId, string version, CancellationToken cancellationToken);
}
