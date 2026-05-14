using System.Security.Claims;
using System.Threading.Tasks;
using BlogWebApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace BlogWebApp.Services
{
    /// <summary>
    /// Emits role claims into the auth cookie for <see cref="CosmicBlogUser"/>.
    ///
    /// <para>The base <c>UserClaimsPrincipalFactory&lt;TUser&gt;</c> (registered by
    /// <c>AddIdentityCore&lt;TUser&gt;</c>) does NOT emit role claims even when the
    /// store implements <c>IUserRoleStore</c>. The standard way to get role claims
    /// is <c>AddRoles&lt;TRole&gt;()</c>, which swaps in
    /// <c>UserClaimsPrincipalFactory&lt;TUser, TRole&gt;</c> — but that also
    /// requires an <c>IRoleStore&lt;TRole&gt;</c> registration, which CosmicBlog
    /// doesn't have (roles are stored as strings on the user document, not as
    /// separate role entities).</para>
    ///
    /// <para>This factory bridges the gap: it inherits the base behavior and
    /// adds role claims from <see cref="UserManager{TUser}.GetRolesAsync"/>,
    /// which flows through our Cosmos-backed <c>IUserRoleStore</c>.</para>
    /// </summary>
    public class CosmicBlogUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<CosmicBlogUser>
    {
        public CosmicBlogUserClaimsPrincipalFactory(
            UserManager<CosmicBlogUser> userManager,
            IOptions<IdentityOptions> optionsAccessor)
            : base(userManager, optionsAccessor)
        {
        }

        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(CosmicBlogUser user)
        {
            var id = await base.GenerateClaimsAsync(user);

            if (UserManager.SupportsUserRole)
            {
                var roles = await UserManager.GetRolesAsync(user);
                foreach (var role in roles)
                {
                    id.AddClaim(new Claim(Options.ClaimsIdentity.RoleClaimType, role));
                }
            }

            return id;
        }
    }
}
