namespace JobScrape.Domain.Identity
{
    /// <summary>
    /// The closed set of role names. New roles are introduced by adding a
    /// member here, never by free-form strings at the application edge.
    /// </summary>
    public enum RoleName
    {
        /// <summary>
        /// Full access: user, role, and permission administration.
        /// </summary>
        Admin = 0,

        /// <summary>
        /// Day-to-day operation: profiles and scrape runs, no administration.
        /// </summary>
        Operator = 1,

        /// <summary>
        /// Read-only observation: profiles and runs, no writes.
        /// </summary>
        Viewer = 2,
    }
}
