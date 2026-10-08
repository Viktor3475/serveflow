using Microsoft.Extensions.Configuration;

namespace ServeFlow.Infrastructure.Identity;

public sealed class DevelopmentDemoSettings
{
    public required string WaiterEmail { get; init; }
    public required string WaiterPassword { get; init; }
    public required string KitchenEmail { get; init; }
    public required string KitchenPassword { get; init; }
    public required Guid RestaurantId { get; init; }

    public static DevelopmentDemoSettings Read(IConfiguration configuration, bool isDevelopment)
    {
        if (!isDevelopment) throw new InvalidOperationException("Demo provisioning is allowed only in Development.");
        string Required(string key) => string.IsNullOrWhiteSpace(configuration[key])
            ? throw new InvalidOperationException($"Configure {key} externally before demo provisioning.") : configuration[key]!;
        if (!Guid.TryParse(Required("Bootstrap:RestaurantId"), out var restaurant) || restaurant == Guid.Empty)
            throw new InvalidOperationException("Bootstrap:RestaurantId must be a nonempty GUID.");
        var settings = new DevelopmentDemoSettings
        {
            RestaurantId = restaurant, WaiterEmail = Required("Demo:WaiterEmail").Trim(), WaiterPassword = Required("Demo:WaiterPassword"),
            KitchenEmail = Required("Demo:KitchenEmail").Trim(), KitchenPassword = Required("Demo:KitchenPassword")
        };
        var administrator = Required("Bootstrap:Email").Trim();
        if (new[] { administrator, settings.WaiterEmail, settings.KitchenEmail }.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 3)
            throw new InvalidOperationException("Demo account email addresses must be distinct.");
        return settings;
    }
}
