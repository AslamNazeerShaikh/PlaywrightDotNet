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
}
