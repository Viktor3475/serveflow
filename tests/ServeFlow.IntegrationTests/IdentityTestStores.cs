using Microsoft.AspNetCore.Identity;
using ServeFlow.Infrastructure.Identity;

namespace ServeFlow.IntegrationTests;

// Tests exercise real Identity managers, hashing, cookies and antiforgery; only persistence is substituted.
internal sealed class IdentityTestUserStore : IUserPasswordStore<ApplicationUser>, IUserEmailStore<ApplicationUser>,
    IUserRoleStore<ApplicationUser>, IUserSecurityStampStore<ApplicationUser>, IUserLockoutStore<ApplicationUser>
{
    public Dictionary<Guid, ApplicationUser> Users { get; } = [];
    private readonly Dictionary<Guid, HashSet<string>> _roles = [];
    public void Dispose() { }
    public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken token) { Users[user.Id] = user; return Task.FromResult(IdentityResult.Success); }
    public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken token) { Users[user.Id] = user; return Task.FromResult(IdentityResult.Success); }
    public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken token) { Users.Remove(user.Id); return Task.FromResult(IdentityResult.Success); }
    public Task<ApplicationUser?> FindByIdAsync(string id, CancellationToken token) => Task.FromResult(Users.GetValueOrDefault(Guid.Parse(id)));
    public Task<ApplicationUser?> FindByNameAsync(string name, CancellationToken token) => Task.FromResult(Users.Values.SingleOrDefault(x => x.NormalizedUserName == name));
    public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken token) => Task.FromResult(user.Id.ToString());
    public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken token) => Task.FromResult(user.UserName);
    public Task SetUserNameAsync(ApplicationUser user, string? name, CancellationToken token) { user.UserName = name; return Task.CompletedTask; }
    public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken token) => Task.FromResult(user.NormalizedUserName);
    public Task SetNormalizedUserNameAsync(ApplicationUser user, string? name, CancellationToken token) { user.NormalizedUserName = name; return Task.CompletedTask; }
    public Task SetPasswordHashAsync(ApplicationUser user, string? hash, CancellationToken token) { user.PasswordHash = hash; return Task.CompletedTask; }
    public Task<string?> GetPasswordHashAsync(ApplicationUser user, CancellationToken token) => Task.FromResult(user.PasswordHash);
    public Task<bool> HasPasswordAsync(ApplicationUser user, CancellationToken token) => Task.FromResult(user.PasswordHash is not null);
    public Task SetEmailAsync(ApplicationUser user, string? email, CancellationToken token) { user.Email = email; return Task.CompletedTask; }
    public Task<string?> GetEmailAsync(ApplicationUser user, CancellationToken token) => Task.FromResult(user.Email);
    public Task<bool> GetEmailConfirmedAsync(ApplicationUser user, CancellationToken token) => Task.FromResult(user.EmailConfirmed);
    public Task SetEmailConfirmedAsync(ApplicationUser user, bool value, CancellationToken token) { user.EmailConfirmed = value; return Task.CompletedTask; }
    public Task<ApplicationUser?> FindByEmailAsync(string email, CancellationToken token) => Task.FromResult(Users.Values.SingleOrDefault(x => x.NormalizedEmail == email));
    public Task<string?> GetNormalizedEmailAsync(ApplicationUser user, CancellationToken token) => Task.FromResult(user.NormalizedEmail);
    public Task SetNormalizedEmailAsync(ApplicationUser user, string? email, CancellationToken token) { user.NormalizedEmail = email; return Task.CompletedTask; }
    private HashSet<string> Roles(ApplicationUser user) { if (!_roles.TryGetValue(user.Id, out var roles)) _roles[user.Id] = roles = []; return roles; }
    public Task AddToRoleAsync(ApplicationUser user, string role, CancellationToken token) { Roles(user).Add(role); return Task.CompletedTask; }
    public Task RemoveFromRoleAsync(ApplicationUser user, string role, CancellationToken token) { Roles(user).Remove(role); return Task.CompletedTask; }
    public Task<IList<string>> GetRolesAsync(ApplicationUser user, CancellationToken token) => Task.FromResult<IList<string>>(Roles(user).Select(x => x switch
    { "ADMINISTRATOR" => "Administrator", "WAITER" => "Waiter", "KITCHENSTAFF" => "KitchenStaff", "CUSTOMER" => "Customer", _ => x }).ToList());
    public Task<bool> IsInRoleAsync(ApplicationUser user, string role, CancellationToken token) => Task.FromResult(Roles(user).Contains(role));
    public Task<IList<ApplicationUser>> GetUsersInRoleAsync(string role, CancellationToken token) => Task.FromResult<IList<ApplicationUser>>(Users.Values.Where(x => Roles(x).Contains(role)).ToList());
    public Task SetSecurityStampAsync(ApplicationUser user, string stamp, CancellationToken token) { user.SecurityStamp = stamp; return Task.CompletedTask; }
    public Task<string?> GetSecurityStampAsync(ApplicationUser user, CancellationToken token) => Task.FromResult(user.SecurityStamp);
    public Task<DateTimeOffset?> GetLockoutEndDateAsync(ApplicationUser user, CancellationToken token) => Task.FromResult(user.LockoutEnd);
    public Task SetLockoutEndDateAsync(ApplicationUser user, DateTimeOffset? value, CancellationToken token) { user.LockoutEnd = value; return Task.CompletedTask; }
    public Task<int> IncrementAccessFailedCountAsync(ApplicationUser user, CancellationToken token) => Task.FromResult(++user.AccessFailedCount);
    public Task ResetAccessFailedCountAsync(ApplicationUser user, CancellationToken token) { user.AccessFailedCount = 0; return Task.CompletedTask; }
    public Task<int> GetAccessFailedCountAsync(ApplicationUser user, CancellationToken token) => Task.FromResult(user.AccessFailedCount);
    public Task<bool> GetLockoutEnabledAsync(ApplicationUser user, CancellationToken token) => Task.FromResult(user.LockoutEnabled);
    public Task SetLockoutEnabledAsync(ApplicationUser user, bool value, CancellationToken token) { user.LockoutEnabled = value; return Task.CompletedTask; }
}

internal sealed class IdentityTestRoleStore : IRoleStore<IdentityRole<Guid>>
{
    private readonly Dictionary<Guid, IdentityRole<Guid>> _roles = [];
    public void Dispose() { }
    public Task<IdentityResult> CreateAsync(IdentityRole<Guid> role, CancellationToken token) { _roles[role.Id] = role; return Task.FromResult(IdentityResult.Success); }
    public Task<IdentityResult> UpdateAsync(IdentityRole<Guid> role, CancellationToken token) { _roles[role.Id] = role; return Task.FromResult(IdentityResult.Success); }
    public Task<IdentityResult> DeleteAsync(IdentityRole<Guid> role, CancellationToken token) { _roles.Remove(role.Id); return Task.FromResult(IdentityResult.Success); }
    public Task<string> GetRoleIdAsync(IdentityRole<Guid> role, CancellationToken token) => Task.FromResult(role.Id.ToString());
    public Task<string?> GetRoleNameAsync(IdentityRole<Guid> role, CancellationToken token) => Task.FromResult(role.Name);
    public Task SetRoleNameAsync(IdentityRole<Guid> role, string? value, CancellationToken token) { role.Name = value; return Task.CompletedTask; }
    public Task<string?> GetNormalizedRoleNameAsync(IdentityRole<Guid> role, CancellationToken token) => Task.FromResult(role.NormalizedName);
    public Task SetNormalizedRoleNameAsync(IdentityRole<Guid> role, string? value, CancellationToken token) { role.NormalizedName = value; return Task.CompletedTask; }
    public Task<IdentityRole<Guid>?> FindByIdAsync(string id, CancellationToken token) => Task.FromResult(_roles.GetValueOrDefault(Guid.Parse(id)));
    public Task<IdentityRole<Guid>?> FindByNameAsync(string name, CancellationToken token) => Task.FromResult(_roles.Values.SingleOrDefault(x => x.NormalizedName == name));
}

internal sealed class TestAccountActivity : IAccountActivity
{
    public bool RestaurantActive { get; set; } = true;
    public Task<bool> IsActiveAsync(ApplicationUser user) => Task.FromResult(!user.IsArchived && RestaurantActive);
}
