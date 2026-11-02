using JobScrape.Application.Abstractions;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;

namespace JobScrape.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// EF Core <see cref="ISourceQuotaRepository"/>.
    /// </summary>
    internal sealed class SourceQuotaRepository(JobScrapeDbContext db) : ISourceQuotaRepository
    {
        public async Task<SourceQuota?> FindAsync(
            SourceKey source,
            DateOnly quotaDate,
            CancellationToken cancellationToken
        )
        {
            // FindAsync checks staged entities before querying.
            return await db.SourceQuotas.FindAsync([source, quotaDate], cancellationToken);
        }

        public async Task AddAsync(SourceQuota quota, CancellationToken cancellationToken)
        {
            await db.SourceQuotas.AddAsync(quota, cancellationToken);
        }
    }
}
