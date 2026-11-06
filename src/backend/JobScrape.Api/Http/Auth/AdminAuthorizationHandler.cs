using JobScrape.Application.Abstractions;
using JobScrape.Domain.Identity;
using Microsoft.AspNetCore.Authorization;

namespace JobScrape.Api.Http.Auth
{
    /// <summary>
    /// Satisfies an <see cref="AdminRequirement"/> from the token's
    /// <c>role</c> claims. A token bearing
    /// <c>must_change_password</c> never satisfies it outside the auth
    /// surface: first login must finish at <c>/api/v1/auth</c> before any
    /// admin endpoint authorizes.
    /// </summary>
    public sealed class AdminAuthorizationHandler : AuthorizationHandler<AdminRequirement>
    {
        // Injected and never mutated.
        private readonly IHttpContextAccessor _accessor;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdminAuthorizationHandler"/> class.
        /// </summary>
        /// <param name="accessor">Reads the request path. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="accessor"/> is null.</exception>
        public AdminAuthorizationHandler(IHttpContextAccessor accessor)
        {
            ArgumentNullException.ThrowIfNull(accessor);
            _accessor = accessor;
        }

        /// <summary>
        /// Succeeds when the token carries the admin role and first login
        /// is complete (or the request targets the auth surface).
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

            var path = _accessor.HttpContext?.Request.Path.Value ?? string.Empty;
            var mustChange = context.User.HasClaim(claim =>
                claim.Type == IdentityClaimTypes.MustChangePassword && claim.Value == "true"
            );
            if (mustChange && !path.StartsWith("/api/v1/auth", StringComparison.OrdinalIgnoreCase))
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
