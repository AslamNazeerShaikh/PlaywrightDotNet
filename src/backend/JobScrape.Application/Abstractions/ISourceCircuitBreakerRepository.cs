using JobScrape.Domain.Access;
using JobScrape.Domain.Common;

namespace JobScrape.Application.Abstractions
{
    /// <summary>
    /// Port for the per-source <see cref="SourceCircuitBreaker"/>; one row per source.
    /// </summary>
    public interface ISourceCircuitBreakerRepository
    {
        /// <summary>
        /// Finds a source's breaker, or null when the source never failed.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The breaker, or null.</returns>
        Task<SourceCircuitBreaker?> FindAsync(
            SourceKey source,
            CancellationToken cancellationToken
        );

        /// <summary>
        /// Stages a new breaker; persisted by <see cref="IUnitOfWork.SaveChangesAsync"/>.
        /// </summary>
        /// <param name="breaker">The breaker.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>A task that completes when the breaker is staged.</returns>
        Task AddAsync(SourceCircuitBreaker breaker, CancellationToken cancellationToken);
    }
}
