using JobScrape.Application.Abstractions;
using JobScrape.Domain.Identity;
using Microsoft.AspNetCore.Authorization;

namespace JobScrape.Api.Http.Auth
{
    /// <summary>
    /// Satisfies an <see cref="AdminRequirement"/> from the token's
    /// <c>role</c> claims. A token bearing
    /// <c>must_change_password</c> never satisfies it: first login must
    /// finish at <c>/api/v1/auth</c> before any admin endpoint authorizes.
    /// </summary>
    public sealed class AdminAuthorizationHandler : AuthorizationHandler<AdminRequirement>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AdminAuthorizationHandler"/> class.
        /// </summary>
        public AdminAuthorizationHandler()
        {
        }

        /// <summary>
        /// Succeeds when the token carries the admin role and first login is
        /// complete.
        /// </summary>
        /// <param name="context">The authorization context.</param>
        /// <param name="requirement">The admin requirement.</param>
        /// <returns>A completed task.</returns>
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            AdminRequirement requirement
        )
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(requirement);

            var mustChange = context.User.HasClaim(claim =>
                claim.Type == IdentityClaimTypes.MustChangePassword && claim.Value == "true"
            );
            if (mustChange)
            {
                return Task.CompletedTask;
            }

            if (context.User.IsInRole(RoleName.Admin.ToString()))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}