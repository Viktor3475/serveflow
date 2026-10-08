using Microsoft.AspNetCore.Identity;
using ServeFlow.Application.Accounts;

namespace ServeFlow.Infrastructure.Identity;

public sealed class AccountService(SignInManager<ApplicationUser> signIn, UserManager<ApplicationUser> users,
    IAccountActivity activity) : IAccountService
{
    public async Task<bool> SignInAsync(string email, string password, bool rememberMe)
    {
        var user = await users.FindByEmailAsync(email.Trim());
        if (user is null) return false;
        var result = await signIn.PasswordSignInAsync(user, password, rememberMe, lockoutOnFailure: true);
        // Two-factor accounts are not bypassed; enrollment/challenge UI is a future extension.
        return result.Succeeded;
    }

    public Task SignOutAsync() => signIn.SignOutAsync();

    public async Task<OperationResult> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null || !await activity.IsActiveAsync(user)) return OperationResult.Denied();
        var result = await users.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded) return new OperationResult(false, result.Errors.Select(x => x.Description).ToArray());
        await signIn.RefreshSignInAsync(user);
        return OperationResult.Success();
    }
}
