using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ServeFlow.Application.Management;
using ServeFlow.Application.Operations;
using ServeFlow.Application.Ordering;
using ServeFlow.Application.Security;
using ServeFlow.Domain.Entities;
using ServeFlow.Domain.Enums;
using ServeFlow.Infrastructure.Identity;
using ServeFlow.Infrastructure.Persistence;

namespace ServeFlow.Infrastructure.Ordering;

public sealed class OrderingService(ServeFlowDbContext db, UserManager<ApplicationUser> users, IAccountActivity activity,
    IValidator<PlaceOrderRequest> validator, IOrderChangeNotifier notifier) : IOrderingService
{
    public static string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    public async Task<TableAccess?> ExchangeAsync(string token, CancellationToken cancellationToken)
    {
        if (!CapabilityTokens.Valid(token)) return null;
        var hash = Hash(token);
        var table = await ActiveTables().AsNoTracking().SingleOrDefaultAsync(x => x.QrTokenHash == hash, cancellationToken);
        return table is null ? null : new(table.Id, hash);
    }
    private IQueryable<RestaurantTable> ActiveTables() => db.RestaurantTables.Where(x => !x.IsArchived && db.Restaurants.Any(r => r.Id == x.RestaurantId && !r.IsArchived));
    private Task<RestaurantTable?> TableAsync(TableAccess access, CancellationToken token) => ActiveTables().AsNoTracking()
        .SingleOrDefaultAsync(x => x.Id == access.TableId && x.QrTokenHash == access.TokenHash, token);
    private async Task<Guid?> StaffRestaurantAsync(Guid actor, bool admin = false)
    {
        var user = await users.FindByIdAsync(actor.ToString());
        if (user is null || !await activity.IsActiveAsync(user)) return null;
        return await users.IsInRoleAsync(user, StaffRoles.Administrator) || (!admin && await users.IsInRoleAsync(user, StaffRoles.Waiter)) ? user.RestaurantId : null;
    }
    public async Task<TableMenu?> MenuAsync(TableAccess access, CancellationToken cancellationToken)
    {
        var table = await TableAsync(access, cancellationToken);
        return table is null ? null : await MenuForAsync(table, cancellationToken);
    }
    public async Task<TableMenu?> StaffMenuAsync(Guid actor, Guid tableId, CancellationToken cancellationToken)
    {
        var restaurant = await StaffRestaurantAsync(actor);
        var table = await ActiveTables().AsNoTracking().SingleOrDefaultAsync(x => x.Id == tableId && x.RestaurantId == restaurant, cancellationToken);
        return table is null ? null : await MenuForAsync(table, cancellationToken);
    }
    private async Task<TableMenu> MenuForAsync(RestaurantTable table, CancellationToken token)
    {
        var restaurant = await db.Restaurants.AsNoTracking().SingleAsync(x => x.Id == table.RestaurantId, token);
        var rows = await (from item in db.MenuItems.AsNoTracking() join category in db.MenuCategories on item.MenuCategoryId equals category.Id
            where item.RestaurantId == restaurant.Id && !item.IsArchived && !category.IsArchived
            orderby category.SortOrder, category.Name, item.Name select new { Item = item, Category = category }).ToListAsync(token);
        var options = await db.MenuItemOptions.AsNoTracking().Where(x => x.RestaurantId == restaurant.Id && !x.IsArchived && x.IsAvailable).OrderBy(x => x.Name).ToListAsync(token);
        var offers = rows.Select(x => new MenuOffer(x.Item.Id, x.Category.Id, x.Category.Name, x.Item.Name, x.Item.Description, x.Item.Price,
            Convert.ToBase64String(x.Item.Version), x.Item.IsAvailable, x.Item.ImageStorageKey is not null,
            options.Where(o => o.MenuItemId == x.Item.Id).Select(o => new OptionOffer(o.Id, o.Name, o.AdditionalPrice, Convert.ToBase64String(o.Version))).ToArray())).ToArray();
        return new(table.Id, restaurant.Name, table.Number, restaurant.CurrencyCode, offers);
    }
    public Task<OrderingResult> PlaceAsync(TableAccess access, PlaceOrderRequest request, CancellationToken cancellationToken) =>
        PlaceCoreAsync(access, null, access.TableId, request, cancellationToken);
    public Task<OrderingResult> PlaceStaffAsync(Guid actor, Guid tableId, PlaceOrderRequest request, CancellationToken cancellationToken) =>
        PlaceCoreAsync(null, actor, tableId, request, cancellationToken);

    private async Task<OrderingResult> PlaceCoreAsync(TableAccess? access, Guid? actor, Guid tableId, PlaceOrderRequest request, CancellationToken token)
    {
        var validation = await validator.ValidateAsync(request, token);
        if (!validation.IsValid) return new(null, string.Join(" ", validation.Errors.Select(x => x.ErrorMessage).Distinct()));
        var fingerprint = Hash(JsonSerializer.Serialize(new { request.IdempotencyKey, ReceiptHash = Hash(request.ReceiptToken), Notes = request.Notes?.Trim(),
            Lines = request.Lines.Select(x => new { x.ItemId, x.Version, x.Quantity, Instructions = x.Instructions?.Trim(), Options = x.Options.OrderBy(o => o.Id) }) }));
        Guid changedRestaurant = default;
        try
        {
            var result = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
                // SQL Server update locks serialize submissions/status/QR changes for this table; do not lock two tables in one request.
                var table = await db.RestaurantTables.FromSqlInterpolated($"SELECT * FROM [RestaurantTables] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {tableId}").SingleOrDefaultAsync(token);
                if (table is null || table.IsArchived || !await db.Restaurants.AnyAsync(x => x.Id == table.RestaurantId && !x.IsArchived, token)) return Denied();
                if (actor is not null)
                {
                    if (await StaffRestaurantAsync(actor.Value) != table.RestaurantId) return Denied();
                }
                else if (access is null || table.QrTokenHash is null || table.QrTokenHash != access.TokenHash) return Denied();
                var existing = await db.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.RestaurantId == table.RestaurantId && x.RestaurantTableId == table.Id && x.IdempotencyKey == request.IdempotencyKey, token);
                if (existing is not null)
                    return existing.RequestFingerprint == fingerprint && existing.CreatedByUserId == actor
                        ? new(Created(existing, true), null) : new(null, "This submission key was already used for a different order.", Conflict: true);
                if (table.Status == TableStatus.Reserved && actor is null) return new(null, "This table is reserved. Ask a waiter before ordering.", Conflict: true);
                var since = DateTimeOffset.UtcNow.AddMinutes(-15);
                if (actor is null && await db.Orders.CountAsync(x => x.RestaurantTableId == table.Id && x.CreatedAt >= since, token) >= 10)
                    return new(null, "This table has reached its order limit. Please ask a waiter.", Throttled: true);
                var menu = await MenuForAsync(table, token);
                var pricing = OrderCalculator.Calculate(request.Lines, menu.Items);
                if (!pricing.Succeeded) return new(null, pricing.Error, Conflict: true);
                var orderId = Guid.NewGuid();
                var order = new Order { Id = orderId, RestaurantId = table.RestaurantId, RestaurantTableId = table.Id, CreatedByUserId = actor,
                    OrderNumber = "SF-" + orderId.ToString("N")[..26].ToUpperInvariant(), IdempotencyKey = request.IdempotencyKey,
                    RequestFingerprint = fingerprint, ReceiptTokenHash = Hash(request.ReceiptToken), CurrencyCode = menu.CurrencyCode,
                    Total = pricing.Total, Notes = request.Notes?.Trim() };
                foreach (var line in pricing.Lines)
                {
                    var item = new OrderItem { RestaurantId = table.RestaurantId, OrderId = order.Id, MenuItemId = line.ItemId,
                        NameSnapshot = line.Name, UnitPriceSnapshot = line.Price, Quantity = line.Quantity, Instructions = line.Instructions };
                    foreach (var option in line.Options) item.Options.Add(new OrderItemOption { RestaurantId = table.RestaurantId, OrderItemId = item.Id,
                        MenuItemId = line.ItemId, MenuItemOptionId = option.Id, NameSnapshot = option.Name, AdditionalPriceSnapshot = option.Price });
                    order.Items.Add(item);
                }
                order.StatusHistory.Add(new OrderStatusHistory { RestaurantId = table.RestaurantId, OrderId = order.Id, ChangedByUserId = actor, NewStatus = OrderStatus.Pending });
                db.Orders.Add(order); table.Status = TableStatus.Occupied;
                await db.SaveChangesAsync(token);
                await transaction.CommitAsync(token);
                changedRestaurant = order.RestaurantId;
                return new OrderingResult(Created(order, false), null);
            });
            if (result.Order is { Replayed: false } created) await notifier.NotifyAsync(created.Id, changedRestaurant, CancellationToken.None);
            return result;
        }
        catch (DbUpdateConcurrencyException) { return new(null, "The table changed. Refresh and retry with the same submission key.", Conflict: true); }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        { return new(null, "A submission or receipt token has already been used. Keep the original receipt or refresh the cart.", Conflict: true); }
    }
    private static OrderingResult Denied() => new(null, "Table access expired or is unavailable. Scan its current QR code.", Forbidden: true);
    private static OrderCreated Created(Order order, bool replayed) => new(order.Id, order.OrderNumber, order.Total, order.CurrencyCode, replayed);
    public async Task<OrderReceipt?> ReceiptAsync(Guid orderId, string token, CancellationToken cancellationToken)
    {
        if (!CapabilityTokens.Valid(token)) return null;
        var hash = Hash(token);
        var order = await db.Orders.AsNoTracking().Include(x => x.Items).ThenInclude(x => x.Options)
            .SingleOrDefaultAsync(x => (orderId == Guid.Empty || x.Id == orderId) && x.ReceiptTokenHash == hash, cancellationToken);
        if (order is null) return null;
        var restaurant = await db.Restaurants.AsNoTracking().SingleAsync(x => x.Id == order.RestaurantId, cancellationToken);
        var table = await db.RestaurantTables.AsNoTracking().SingleAsync(x => x.Id == order.RestaurantTableId, cancellationToken);
        return new(order.Id, order.OrderNumber, restaurant.Name, table.Number, order.CurrencyCode, order.Total, order.Status, order.CreatedAt, order.Notes,
            order.Items.OrderBy(x => x.Id).Select(x => new ReceiptLine(x.NameSnapshot, x.UnitPriceSnapshot, x.Quantity, x.Instructions,
                x.Options.OrderBy(o => o.Id).Select(o => new ReceiptOption(o.NameSnapshot, o.AdditionalPriceSnapshot)).ToArray())).ToArray());
    }
    public async Task<string?> ImageAsync(TableAccess access, Guid itemId, CancellationToken cancellationToken)
    {
        var table = await TableAsync(access, cancellationToken);
        if (table is null) return null;
        return await db.MenuItems.AsNoTracking().Where(x => x.Id == itemId && x.RestaurantId == table.RestaurantId && !x.IsArchived &&
            db.MenuCategories.Any(c => c.Id == x.MenuCategoryId && !c.IsArchived)).Select(x => x.ImageStorageKey).SingleOrDefaultAsync(cancellationToken);
    }
    public async Task<QrIssued?> RotateQrAsync(Guid actor, Guid tableId, string version, CancellationToken cancellationToken)
    {
        var bytes = ConcurrencyVersion.Decode(version);
        if (bytes is null) return null;
        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                var restaurant = await StaffRestaurantAsync(actor, admin: true);
                var table = await db.RestaurantTables.SingleOrDefaultAsync(x => x.Id == tableId && x.RestaurantId == restaurant && !x.IsArchived, cancellationToken);
                if (table is null || !table.Version.SequenceEqual(bytes)) return null;
                var raw = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
                table.QrTokenHash = Hash(raw);
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new QrIssued(raw, table.Number);
            });
        }
        catch (DbUpdateConcurrencyException) { return null; }
    }
}
