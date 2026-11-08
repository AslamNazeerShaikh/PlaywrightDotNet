using JobScrape.Domain.Identity;

namespace JobScrape.Application.Abstractions
{
    /// <summary>
    /// Issues opaque bearer tokens for authenticated users. The contract is
    /// a string only: no JWT library type crosses into the application
    /// layer, so handlers stay testable with fakes and the edge owns the
    /// wire format.
    /// </summary>
    public interface ITokenService
    {
        /// <summary>
        /// Creates a bearer token for <paramref name="user"/> carrying the
        /// given roles and permissions, valid from <paramref name="now"/>.
        /// </summary>
        /// <param name="user">The authenticated user.</param>
        /// <param name="roles">The user's roles, in assignment order.</param>
        /// <param name="permissions">The union of the roles' permissions.</param>
        /// <param name="now">The issuance time.</param>
        /// <returns>An opaque bearer token.</returns>
        string CreateToken(
            User user,
            IReadOnlyList<Role> roles,
            IReadOnlyList<PermissionKey> permissions,
            DateTimeOffset now
        );
    }
}
