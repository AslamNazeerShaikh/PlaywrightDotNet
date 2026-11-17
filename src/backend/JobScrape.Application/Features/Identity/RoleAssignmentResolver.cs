using JobScrape.Application.Abstractions;
using JobScrape.Application.Results;
using JobScrape.Domain;
using JobScrape.Domain.Identity;

namespace JobScrape.Application.Features.Identity
{
    /// <summary>
    /// The shared preconditions for assigning and revoking a role: a valid
    /// user id, no self-service, no Admin grant from a non-Admin caller, and
    /// the user and role rows must exist.
    /// </summary>
    internal static class RoleAssignmentResolver
    {
        /// <summary>
        /// Resolves the user and role after checking every shared gate.
        /// </summary>
        /// <param name="users">The user port.</param>
        /// <param name="roles">The role port.</param>
        /// <param name="caller">The calling actor.</param>
        /// <param name="userId">The raw user identifier.</param>
        /// <param name="roleName">The closed role name.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The resolved aggregate pair, or the first refusing gate's error.</returns>
        public static async Task<Result<ResolvedAssignment>> ResolveAsync(
            IUserRepository users,
            IRoleRepository roles,
            ICallerContext caller,
            Guid userId,
            RoleName roleName,
            CancellationToken cancellationToken
        )
        {
            UserId id;
            try
            {
                id = UserId.From(userId);
            }
            catch (DomainException ex)
            {
                return Result.Fail<ResolvedAssignment>(
                    Errors.ValidationFailed(
                        ex.Message,
                        new Dictionary<string, string[]> { ["userId"] = [ex.Message] }
                    )
                );
            }

            if (Guid.TryParse(caller.ActorId, out var callerId) && callerId == id.Value)
            {
                return Result.Fail<ResolvedAssignment>(Errors.AccessDenied());
            }

            if (
                roleName == RoleName.Admin
                && !caller.Roles.Any(role =>
                    string.Equals(role, RoleName.Admin.ToString(), StringComparison.Ordinal)
                )
            )
            {
                return Result.Fail<ResolvedAssignment>(Errors.AccessDenied());
            }

            var user = await users.GetAsync(id, cancellationToken);
            if (user is null)
            {
                return Result.Fail<ResolvedAssignment>(Errors.UserNotFound(userId.ToString()));
            }

            var role = await roles.GetByNameAsync(roleName, cancellationToken);
            return role is null
                ? Result.Fail<ResolvedAssignment>(Errors.RoleNotFound(roleName.ToString()))
                : Result.Ok(new ResolvedAssignment(user, role));
        }

        /// <summary>
        /// The user and role a checked role change applies to.
        /// </summary>
        /// <param name="User">The target user.</param>
        /// <param name="Role">The named role.</param>
        internal sealed record ResolvedAssignment(User User, Role Role);
    }
}
