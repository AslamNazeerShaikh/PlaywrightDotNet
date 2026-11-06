using JobScrape.Application.Abstractions;
using JobScrape.Domain.Identity;

namespace JobScrape.Application.Features.Identity
{
    /// <summary>
    /// The one place that maps identity aggregates to admin responses.
    /// Password hashes never leave the store; this mapper never sees one.
    /// </summary>
    internal static class IdentityResponses
    {
        /// <summary>
        /// Maps a user to its admin response, resolving role names.
        /// </summary>
        /// <param name="user">The user. Must not be null.</param>
        /// <param name="roles">The role port. Must not be null.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The admin response.</returns>
        internal static async Task<UserResponse> ToUserResponseAsync(
            User user,
            IRoleRepository roles,
            CancellationToken cancellationToken
        )
        {
            ArgumentNullException.ThrowIfNull(user);
            ArgumentNullException.ThrowIfNull(roles);
            var names = new List<string>();
            foreach (var roleId in user.Roles)
            {
                var role = await roles.GetAsync(roleId, cancellationToken);
                if (role is not null)
                {
                    names.Add(role.Name.ToString());
                }
            }

            return new UserResponse(user.Id.Value, user.Email, user.MustChangePassword, names);
        }

        /// <summary>
        /// Maps a role to its admin response.
        /// </summary>
        /// <param name="role">The role. Must not be null.</param>
        /// <returns>The admin response.</returns>
        internal static RoleResponse ToRoleResponse(Role role)
        {
            ArgumentNullException.ThrowIfNull(role);
            return new RoleResponse(
                role.Id.Value,
                role.Name.ToString(),
                role.PermissionKeys.Select(key => key.Value).ToList()
            );
        }
    }
}
