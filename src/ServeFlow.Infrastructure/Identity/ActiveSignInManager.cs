using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ServeFlow.Infrastructure.Identity;

public sealed class ActiveSignInManager(
    UserManager<ApplicationUser> users, IHttpContextAccessor context,
    IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory, IOptions<IdentityOptions> options,
    ILogger<SignInManager<ApplicationUser>> logger, IAuthenticationSchemeProvider schemes,
    IUserConfirmation<ApplicationUser> confirmation, IAccountActivity activity)
    : SignInManager<ApplicationUser>(users, context, claimsFactory, options, logger, schemes, confirmation)
{
    public override async Task<bool> CanSignInAsync(ApplicationUser user) =>
        await base.CanSignInAsync(user) && await activity.IsActiveAsync(user);
}
