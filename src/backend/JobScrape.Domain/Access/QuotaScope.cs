namespace JobScrape.Domain.Access
{
    /// <summary>
    /// The request surface a usage quota governs. One definition per scope
    /// lives in the database; enforcement counters stay ephemeral at the
    /// edge, so a restart resets usage but never the definitions.
    /// </summary>
    public enum QuotaScope
    {
        /// <summary>
        /// REST calls under <c>/api/v1/*</c>, except auth and health.
        /// </summary>
        Api = 0,

        /// <summary>
        /// MCP calls under <c>/mcp</c>.
        /// </summary>
        Mcp = 1,
    }
}
