namespace JobScrape.Api.Http.Auth
{
    /// <summary>
    /// The policy names the API edge enforces. The admin policy gates the
    /// administration surface; one policy per v1 permission key gates the
    /// matching reads and writes. Registration loops the permission catalog
    /// so these names cannot drift from the domain keys.
    /// </summary>
    public static class AuthPolicies
    {
        /// <summary>
        /// Full access: user, role, permission, and database administration.
        /// </summary>
        public const string RequireAdmin = "Admin";

        /// <summary>
        /// Reads search profiles.
        /// </summary>
        public const string ProfilesRead = "profiles.read";

        /// <summary>
        /// Writes search profiles.
        /// </summary>
        public const string ProfilesWrite = "profiles.write";

        /// <summary>
        /// Reads runs and their postings.
        /// </summary>
        public const string RunsRead = "runs.read";

        /// <summary>
        /// Starts and cancels runs.
        /// </summary>
        public const string RunsWrite = "runs.write";

        /// <summary>
        /// Reads users.
        /// </summary>
        public const string UsersRead = "users.read";

        /// <summary>
        /// Writes users and their role assignments.
        /// </summary>
        public const string UsersWrite = "users.write";

        /// <summary>
        /// Reads roles.
        /// </summary>
        public const string RolesRead = "roles.read";

        /// <summary>
        /// Writes roles and their permission grants.
        /// </summary>
        public const string RolesWrite = "roles.write";

        /// <summary>
        /// Reads browser profiles.
        /// </summary>
        public const string SessionsRead = "sessions.read";

        /// <summary>
        /// Writes browser profiles.
        /// </summary>
        public const string SessionsWrite = "sessions.write";

        /// <summary>
        /// Reads configured sources.
        /// </summary>
        public const string SourcesRead = "sources.read";

        /// <summary>
        /// Every permission policy, in catalog order.
        /// </summary>
        public static IReadOnlyList<string> PermissionPolicies { get; } =
        [
            ProfilesRead,
            ProfilesWrite,
            RunsRead,
            RunsWrite,
            UsersRead,
            UsersWrite,
            RolesRead,
            RolesWrite,
            SessionsRead,
            SessionsWrite,
            SourcesRead,
        ];
    }
}
