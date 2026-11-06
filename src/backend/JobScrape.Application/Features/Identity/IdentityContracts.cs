namespace JobScrape.Application.Features.Identity
{
    /// <summary>
    /// The lockout policy a login attempt enforces. Built once at the
    /// composition root from the JWT section; handlers never read options.
    /// </summary>
    /// <param name="MaxFailedAttempts">Failures allowed before lockout. Must be positive.</param>
    /// <param name="Lockout">How long a lockout lasts.</param>
    public sealed record LoginPolicy(int MaxFailedAttempts, TimeSpan Lockout);

    /// <summary>
    /// A successful login: the bearer token plus whether the account must
    /// still set a new password (always false on this path today, kept so
    /// the API edge has one contract for both login outcomes).
    /// </summary>
    /// <param name="Token">The opaque bearer token.</param>
    /// <param name="MustChangePassword">Whether a password change is still required.</param>
    public sealed record LoginResponse(string Token, bool MustChangePassword);

    /// <summary>
    /// One user for the admin surface: identity plus assigned role names in
    /// assignment order. The password hash never leaves the store.
    /// </summary>
    /// <param name="Id">The user identifier.</param>
    /// <param name="Email">The normalized email.</param>
    /// <param name="MustChangePassword">Whether first login is still pending.</param>
    /// <param name="Roles">The assigned role names.</param>
    public sealed record UserResponse(
        Guid Id,
        string Email,
        bool MustChangePassword,
        IReadOnlyList<string> Roles
    );

    /// <summary>
    /// One role for the admin surface: name plus granted permission keys in
    /// grant order.
    /// </summary>
    /// <param name="Id">The role identifier.</param>
    /// <param name="Name">The role name.</param>
    /// <param name="Permissions">The granted permission keys.</param>
    public sealed record RoleResponse(
        Guid Id,
        string Name,
        IReadOnlyList<string> Permissions
    );

    /// <summary>
    /// The user list payload.
    /// </summary>
    /// <param name="Users">All users, in store order.</param>
    public sealed record UserListResponse(IReadOnlyList<UserResponse> Users);

    /// <summary>
    /// The role list payload.
    /// </summary>
    /// <param name="Roles">All roles, in store order.</param>
    public sealed record RoleListResponse(IReadOnlyList<RoleResponse> Roles);
}
