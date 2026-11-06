using JobScrape.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;

namespace JobScrape.Api.Http.Auth
{
    /// <summary>
    /// Satisfies a <see cref="PermissionRequirement"/> from the token's
    /// <c>perm</c> claims, one per granted permission key. A token bearing
    /// <c>must_change_password</c> never satisfies a permission outside the
    /// auth surface: first login must finish at <c>/api/v1/auth</c> before
    /// any other endpoint authorizes.
    /// </summary>
    public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
    {
        // Injected and never mutated.
        private readonly IHttpContextAccessor _accessor;

        /// <summary>
        /// Initializes a new instance of the <see cref="PermissionAuthorizationHandler"/> class.
        /// </summary>
        /// <param name="accessor">Reads the request path. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="accessor"/> is null.</exception>
        public PermissionAuthorizationHandler(IHttpContextAccessor accessor)
        {
            ArgumentNullException.ThrowIfNull(accessor);
            _accessor = accessor;
        }

        /// <summary>
        /// Succeeds when the token carries the required permission claim and
        /// first login is complete (or the request targets the auth surface).
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

            var path = _accessor.HttpContext?.Request.Path.Value ?? string.Empty;
            var mustChange = context.User.HasClaim(
                claim =>
                    claim.Type == IdentityClaimTypes.MustChangePassword
                    && claim.Value == "true"
            );
            if (
                mustChange
                && !path.StartsWith("/api/v1/auth", StringComparison.OrdinalIgnoreCase)
            )
            {
                return Task.CompletedTask;
            }

            if (context.User.HasClaim(IdentityClaimTypes.Permission, requirement.Permission))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}
