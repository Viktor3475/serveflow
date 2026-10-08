namespace ServeFlow.Application.Security;

public static class StaffRoles
{
    public const string Administrator = "Administrator";
    public const string Waiter = "Waiter";
    public const string KitchenStaff = "KitchenStaff";
    public const string Customer = "Customer";
    public static IReadOnlyList<string> All { get; } = [Administrator, Waiter, KitchenStaff, Customer];
    public static IReadOnlyList<string> EmployeeRoles { get; } = [Administrator, Waiter, KitchenStaff];
}

public static class StaffPolicies
{
    public const string Administration = "Administration";
    public const string DiningRoom = "DiningRoom";
    public const string Kitchen = "Kitchen";
    public const string Staff = "Staff";
}

public static class StaffClaims
{
    public const string RestaurantId = "serveflow:restaurant_id";
    public const string DisplayName = "serveflow:display_name";
}
