namespace JobScrape.Application.Abstractions
{
    /// <summary>
    /// The wire names of the identity claims the token service issues and
    /// the API edge reads. One source of truth so the issuer and every
    /// reader agree; the JWT outbound map shortens role claims to
    /// <c>role</c>, so readers must use these names, not the long
    /// <c>ClaimTypes</c> URIs.
    /// </summary>
    public static class IdentityClaimTypes
    {
        /// <summary>
        /// The user id claim.
        /// </summary>
        public const string Subject = "sub";

        /// <summary>
        /// The user email claim.
        /// </summary>
        public const string Email = "email";

        /// <summary>
        /// The role name claim, one per granted role.
        /// </summary>
        public const string Role = "role";

        /// <summary>
        /// The permission key claim, one per granted permission.
        /// </summary>
        public const string Permission = "perm";

        /// <summary>
        /// The first-login-pending marker claim.
        /// </summary>
        public const string MustChangePassword = "must_change_password";

        /// <summary>
        /// The token version claim. It mirrors <c>User.TokenVersion</c> so
        /// password changes and lockouts revoke outstanding bearer tokens.
        /// </summary>
        public const string TokenVersion = "ver";
    }
}
