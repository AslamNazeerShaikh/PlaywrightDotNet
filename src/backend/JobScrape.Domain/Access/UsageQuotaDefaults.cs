using JobScrape.Domain.Access;

namespace JobScrape.Domain.Access
{
    /// <summary>
    /// The starting budgets seeded when the definitions table is empty.
    /// Per-user budgets: REST gets 10000 requests per day, MCP gets 1000
    /// per day. Starting points only; admins tune them at runtime through
    /// <c>PUT /api/v1/quotas/{scope}</c> and restarts never reset them.
    /// </summary>
    public static class UsageQuotaDefaults
    {
        /// <summary>
        /// Builds fresh definitions for every scope.
        /// </summary>
        /// <returns>One definition per scope.</returns>
        public static IReadOnlyList<UsageQuota> All()
        {
            return
            [
                UsageQuota.Create(QuotaScope.Api, 10000, TimeSpan.FromHours(24)),
                UsageQuota.Create(QuotaScope.Mcp, 1000, TimeSpan.FromHours(24)),
            ];
        }
    }
}
