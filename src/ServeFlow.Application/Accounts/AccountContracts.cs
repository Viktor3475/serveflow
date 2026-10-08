namespace ServeFlow.Application.Accounts;

public sealed record OperationResult(bool Succeeded, IReadOnlyList<string> Errors, bool Forbidden = false, bool Conflict = false)
{
    public static OperationResult Success() => new(true, []);
    public static OperationResult Failure(params string[] errors) => new(false, errors);
    public static OperationResult Denied() => new(false, [], Forbidden: true);
    public static OperationResult Stale(string message = "This account changed. Reload the page and try again.") => new(false, [message], Conflict: true);
}

public interface IAccountService
{
    Task<bool> SignInAsync(string email, string password, bool rememberMe);
    Task SignOutAsync();
    Task<OperationResult> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword);
}

public sealed record EmployeeSummary(Guid Id, string Email, string DisplayName, string Role, bool IsArchived, string ConcurrencyStamp);
public sealed record EmployeeDirectory(bool Allowed, IReadOnlyList<EmployeeSummary> Employees);
public sealed record CreateEmployeeRequest(string Email, string DisplayName, string Password, string Role);
public sealed record EditEmployeeRequest(Guid Id, string DisplayName, string Role, bool IsArchived, string ConcurrencyStamp);

public interface IEmployeeService
{
    Task<EmployeeDirectory> ListAsync(Guid actorId, CancellationToken cancellationToken);
    Task<OperationResult> CreateAsync(Guid actorId, CreateEmployeeRequest request, CancellationToken cancellationToken);
    Task<OperationResult> UpdateAsync(Guid actorId, EditEmployeeRequest request, CancellationToken cancellationToken);
}
