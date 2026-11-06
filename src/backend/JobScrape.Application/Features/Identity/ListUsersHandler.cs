using JobScrape.Application.Abstractions;
using JobScrape.Application.Results;
using JobScrape.Domain.Identity;

namespace JobScrape.Application.Features.Identity
{
    /// <summary>
    /// Lists users for the admin surface with resolved role names. The
    /// password hash never leaves the store.
    /// </summary>
    public sealed class ListUsersHandler
    {
        private readonly IUserRepository _users;
        private readonly IRoleRepository _roles;

        /// <summary>
        /// Initializes a new instance of the <see cref="ListUsersHandler"/> class.
        /// </summary>
        /// <param name="users">The user port. Must not be null.</param>
        /// <param name="roles">The role port. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public ListUsersHandler(IUserRepository users, IRoleRepository roles)
        {
            ArgumentNullException.ThrowIfNull(users);
            ArgumentNullException.ThrowIfNull(roles);
            _users = users;
            _roles = roles;
        }

        /// <summary>
        /// Lists every user with role names.
        /// </summary>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>All users, in store order.</returns>
        public async Task<IReadOnlyList<UserResponse>> HandleAsync(
            CancellationToken cancellationToken
        )
        {
            var users = await _users.ListAsync(cancellationToken);
            var responses = new List<UserResponse>(users.Count);
            foreach (var user in users)
            {
                responses.Add(
                    await IdentityResponses.ToUserResponseAsync(user, _roles, cancellationToken)
                );
            }

            return responses;
        }
    }
}
