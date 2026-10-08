using Microsoft.EntityFrameworkCore;
using ServeFlow.Application.Accounts;
using ServeFlow.Application.Management;

namespace ServeFlow.Infrastructure.Management;

public sealed partial class RestaurantManagementService
{
    public async Task<OperationResult> UploadImageAsync(Guid actorId, Guid itemId, string version, Stream content, string fileName, string contentType, CancellationToken token)
    {
        // Verify ownership/version before processing an untrusted image.
        var restaurantId = await RestaurantAsync(actorId);
        if (restaurantId is null) return OperationResult.Denied();
        var item = await db.MenuItems.AsNoTracking().SingleOrDefaultAsync(x => x.Id == itemId && x.RestaurantId == restaurantId, token);
        if (item is null) return OperationResult.Denied();
        var bytes = ConcurrencyVersion.Decode(version);
        if (bytes is null || !item.Version.SequenceEqual(bytes)) return OperationResult.Stale("This record changed. Reload the page and try again.");
        var upload = await images.SaveAsync(content, fileName, contentType, token);
        if (!upload.Succeeded) return OperationResult.Failure(upload.Error!);
        // Files are immutable. Retain replaced/orphaned files until a separately reviewed retention cleanup.
        return await SetImageAsync(actorId, itemId, version, upload.StorageKey, token);
    }

    public Task<OperationResult> RemoveImageAsync(Guid actorId, Guid itemId, string version, CancellationToken token) =>
        SetImageAsync(actorId, itemId, version, null, token);

    private Task<OperationResult> SetImageAsync(Guid actorId, Guid itemId, string version, string? key, CancellationToken token) =>
        WriteAsync(actorId, async restaurantId =>
        {
            var item = await db.MenuItems.SingleOrDefaultAsync(x => x.Id == itemId && x.RestaurantId == restaurantId, token);
            if (item is null) return OperationResult.Denied();
            if (!CheckVersion(db, item, version)) return OperationResult.Stale("This record changed. Reload the page and try again.");
            item.ImageStorageKey = key;
            await db.SaveChangesAsync(token);
            return OperationResult.Success();
        }, token);

    public async Task<ManagementRead<string>> ImageKeyAsync(Guid actorId, Guid itemId, CancellationToken token)
    {
        var restaurantId = await RestaurantAsync(actorId, anyStaff: true);
        if (restaurantId is null) return new(false, null);
        var key = await db.MenuItems.AsNoTracking().Where(x => x.Id == itemId && x.RestaurantId == restaurantId)
            .Select(x => x.ImageStorageKey).SingleOrDefaultAsync(token);
        return new(true, key);
    }
}
