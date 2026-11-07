using JobScrape.Application.Abstractions;
using JobScrape.Application.Results;
using JobScrape.Domain;
using JobScrape.Domain.Identity;

namespace JobScrape.Application.Features.Identity
{
    /// <summary>
    /// Reads one user for the admin surface.
    /// </summary>
    public sealed class GetUserHandler
    {
        private readonly IUserRepository _users;
        private readonly IRoleRepository _roles;

        /// <summary>
        /// Initializes a new instance of the <see cref="GetUserHandler"/> class.
        /// </summary>
        /// <param name="users">The user port. Must not be null.</param>
        /// <param name="roles">The role port. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public GetUserHandler(IUserRepository users, IRoleRepository roles)
        {
            ArgumentNullException.ThrowIfNull(users);
            ArgumentNullException.ThrowIfNull(roles);
            _users = users;
            _roles = roles;
        }

        /// <summary>
        /// Gets one user with resolved role names.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>
        /// Success with the user; <c>VALIDATION_FAILED</c> for an empty id,
        /// or <c>USER_NOT_FOUND</c> when absent.
        /// </returns>
        public async Task<Result<UserResponse>> HandleAsync(
            Guid userId,
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
                return Result.Fail<UserResponse>(
                    Errors.ValidationFailed(
                        ex.Message,
                        new Dictionary<string, string[]> { ["userId"] = [ex.Message] }
                    )
                );
            }

            var user = await _users.GetAsync(id, cancellationToken);
            if (user is null)
            {
                return Result.Fail<UserResponse>(Errors.UserNotFound(userId.ToString()));
            }

            return Result.Ok(
                await IdentityResponses.ToUserResponseAsync(user, _roles, cancellationToken)
            );
        }
    }
}
