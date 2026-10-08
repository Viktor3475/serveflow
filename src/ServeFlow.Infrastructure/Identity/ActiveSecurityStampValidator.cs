using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ServeFlow.Infrastructure.Identity;

public sealed class ActiveSecurityStampValidator(
    IOptions<SecurityStampValidatorOptions> options, SignInManager<ApplicationUser> signIn,
    ILoggerFactory loggerFactory, IAccountActivity activity)
    : SecurityStampValidator<ApplicationUser>(options, signIn, loggerFactory)
{
    public override async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        await base.ValidateAsync(context);
        if (context.Principal is null) return;
        var user = await SignInManager.UserManager.GetUserAsync(context.Principal);
        if (user is null || !await activity.IsActiveAsync(user))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        }
    }
}
