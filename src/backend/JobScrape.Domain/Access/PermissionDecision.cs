namespace JobScrape.Domain.Access
{
    /// <summary>
    /// The single permission gate both edges (HTTP and MCP) delegate to.
    /// A caller pending first login never satisfies any permission: first
    /// login must finish at <c>/api/v1/auth</c> before any endpoint
    /// authorizes. Comparison is ordinal, matching how the token's
    /// <c>perm</c> claims are stored and matched.
    /// </summary>
    public static class PermissionDecision
    {
        /// <summary>
        /// Reports whether the caller may exercise the permission.
        /// </summary>
        /// <param name="mustChangePassword">
        /// True while first login is pending; always denies.
        /// </param>
        /// <param name="permissions">
        /// The permission keys granted to the caller. Must not be null.
        /// </param>
        /// <param name="permission">The required catalog key, e.g. <c>runs.write</c>. Must not be null.</param>
        /// <returns>True when first login is complete and the grant is present.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="permissions"/> or <paramref name="permission"/> is null.
        /// </exception>
        public static bool IsAllowed(
            bool mustChangePassword,
            IReadOnlyCollection<string> permissions,
            string permission
        )
        {
            ArgumentNullException.ThrowIfNull(permissions);
            ArgumentNullException.ThrowIfNull(permission);
            return !mustChangePassword && permissions.Contains(permission, StringComparer.Ordinal);
        }
    }
}
