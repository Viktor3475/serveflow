using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using ServeFlow.Application.Security;

namespace ServeFlow.Infrastructure.Identity;

public sealed class StaffClaimsPrincipalFactory(
    UserManager<ApplicationUser> users, RoleManager<IdentityRole<Guid>> roles, IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole<Guid>>(users, roles, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(StaffClaims.RestaurantId, user.RestaurantId.ToString()));
        identity.AddClaim(new Claim(StaffClaims.DisplayName, user.DisplayName));
        return identity;
    }
}
