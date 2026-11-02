using JobScrape.Application.Abstractions;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;

namespace JobScrape.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// EF Core <see cref="ISourceCircuitBreakerRepository"/>.
    /// </summary>
    internal sealed class SourceCircuitBreakerRepository(JobScrapeDbContext db)
        : ISourceCircuitBreakerRepository
    {
        public async Task<SourceCircuitBreaker?> FindAsync(
            SourceKey source,
            CancellationToken cancellationToken
        )
        {
            return await db.SourceCircuitBreakers.FindAsync([source], cancellationToken);
        }

        public async Task AddAsync(
            SourceCircuitBreaker breaker,
            CancellationToken cancellationToken
        )
        {
            await db.SourceCircuitBreakers.AddAsync(breaker, cancellationToken);
        }
    }
}
