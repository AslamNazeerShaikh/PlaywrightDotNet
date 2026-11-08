using JobScrape.Application.Abstractions;
using JobScrape.Domain.Access;
using JobScrape.Domain.Identity;
using Microsoft.AspNetCore.Authorization;

namespace JobScrape.Api.Http.Auth
{
    /// <summary>
    /// Satisfies a <see cref="PermissionRequirement"/> from the token's
    /// <c>perm</c> claims, one per granted permission key. A token bearing
    /// <c>must_change_password</c> never satisfies any permission: first
    /// login must finish at <c>/api/v1/auth</c> before any endpoint
    /// authorizes.
    /// </summary>
    public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PermissionAuthorizationHandler"/> class.
        /// </summary>
        public PermissionAuthorizationHandler() { }

        /// <summary>
        /// Succeeds when the token carries the required permission claim (or
        /// the admin role) and first login is complete.
        /// </summary>
        /// <param name="context">The authorization context.</param>
        /// <param name="requirement">The required permission.</param>
        /// <returns>A completed task.</returns>
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermissionRequirement requirement
        )
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(requirement);

            var mustChange = context.User.HasClaim(claim =>
                claim.Type == IdentityClaimTypes.MustChangePassword && claim.Value == "true"
            );
            var permissionGranted = PermissionDecision.IsAllowed(
                mustChange,
                context
                    .User.FindAll(IdentityClaimTypes.Permission)
                    .Select(claim => claim.Value)
                    .ToArray(),
                requirement.Permission
            );
            if (
                permissionGranted
                || (!mustChange && context.User.IsInRole(RoleName.Admin.ToString()))
            )
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}
