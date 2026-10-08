using ServeFlow.Application.Accounts;
using ServeFlow.Domain.Enums;

namespace ServeFlow.Application.Management;

public sealed record ManagementRead<T>(bool Allowed, T? Value);
public sealed record ManagementPage<T>(IReadOnlyList<T> Items, int Page, int TotalCount, int PageSize, string Search, bool IncludeArchived, string CurrencyCode)
{
    public int TotalPages => Math.Max(1, (TotalCount + PageSize - 1) / PageSize);
}
public sealed record RestaurantSettings(string Name, string CurrencyCode, string TimeZoneId, string Version);
public sealed record CategoryDetails(Guid Id, string Name, int SortOrder, bool IsArchived, string Version);
public sealed record CategoryChoice(Guid Id, string Name, bool IsArchived);
public sealed record MenuDetails(Guid Id, Guid CategoryId, string CategoryName, string Name, string Description,
    decimal Price, bool IsAvailable, bool IsArchived, bool HasImage, string Version);
public sealed record TableDetails(Guid Id, int Number, int Capacity, TableStatus Status, bool IsArchived, string Version);
public sealed record SettingsInput(string Name, string TimeZoneId, string Version);
public sealed record CategoryInput(Guid? Id, string Name, int SortOrder, bool IsArchived, string? Version);
public sealed record MenuInput(Guid? Id, Guid CategoryId, string Name, string Description, decimal Price, bool IsAvailable, bool IsArchived, string? Version);
public sealed record TableInput(Guid? Id, int Number, int Capacity, TableStatus Status, bool IsArchived, string? Version);
public sealed record TableStatusInput(Guid Id, TableStatus Status, string Version);

public interface IRestaurantManagementService
{
    Task<ManagementRead<RestaurantSettings>> GetSettingsAsync(Guid actorId, CancellationToken token);
    Task<OperationResult> SaveSettingsAsync(Guid actorId, SettingsInput input, CancellationToken token);
    Task<ManagementRead<ManagementPage<CategoryDetails>>> CategoriesAsync(Guid actorId, int page, string? search, bool includeArchived, CancellationToken token);
    Task<ManagementRead<CategoryDetails>> GetCategoryAsync(Guid actorId, Guid id, CancellationToken token);
    Task<OperationResult> SaveCategoryAsync(Guid actorId, CategoryInput input, CancellationToken token);
    Task<ManagementRead<IReadOnlyList<CategoryChoice>>> CategoryChoicesAsync(Guid actorId, CancellationToken token);
    Task<ManagementRead<ManagementPage<MenuDetails>>> MenuAsync(Guid actorId, int page, string? search, bool includeArchived, CancellationToken token);
    Task<ManagementRead<MenuDetails>> GetMenuAsync(Guid actorId, Guid id, CancellationToken token);
    Task<OperationResult> SaveMenuAsync(Guid actorId, MenuInput input, CancellationToken token);
    Task<ManagementRead<ManagementPage<TableDetails>>> TablesAsync(Guid actorId, int page, bool includeArchived, CancellationToken token);
    Task<ManagementRead<TableDetails>> GetTableAsync(Guid actorId, Guid id, CancellationToken token);
    Task<OperationResult> SaveTableAsync(Guid actorId, TableInput input, CancellationToken token);
    Task<OperationResult> SetTableStatusAsync(Guid actorId, TableStatusInput input, CancellationToken token);
    Task<OperationResult> UploadImageAsync(Guid actorId, Guid itemId, string version, Stream content, string fileName, string contentType, CancellationToken token);
    Task<OperationResult> RemoveImageAsync(Guid actorId, Guid itemId, string version, CancellationToken token);
    Task<ManagementRead<string>> ImageKeyAsync(Guid actorId, Guid itemId, CancellationToken token);
}

public sealed record ImageUploadResult(bool Succeeded, string? StorageKey, string? Error);
public interface IProductImageStore
{
    Task<ImageUploadResult> SaveAsync(Stream input, string fileName, string contentType, CancellationToken token);
    Stream? OpenRead(string key);
}
