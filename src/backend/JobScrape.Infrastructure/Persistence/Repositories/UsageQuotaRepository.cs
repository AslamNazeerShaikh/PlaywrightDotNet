using JobScrape.Application.Abstractions;
using JobScrape.Domain.Access;
using Microsoft.EntityFrameworkCore;

namespace JobScrape.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// EF Core <see cref="IUsageQuotaRepository"/>.
    /// </summary>
    internal sealed class UsageQuotaRepository(JobScrapeDbContext db) : IUsageQuotaRepository
    {
        /// <inheritdoc/>
        public async Task<UsageQuota?> GetAsync(
            QuotaScope scope,
            CancellationToken cancellationToken
        )
        {
            return await db.UsageQuotas.FindAsync([scope], cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<UsageQuota>> ListAsync(CancellationToken cancellationToken)
        {
            var quotas = await db.UsageQuotas
                .AsNoTracking()
                .OrderBy(quota => quota.Scope)
                .ToListAsync(cancellationToken);
            return quotas;
        }

        /// <inheritdoc/>
        public async Task AddAsync(UsageQuota quota, CancellationToken cancellationToken)
        {
            await db.UsageQuotas.AddAsync(quota, cancellationToken);
        }
    }
}
