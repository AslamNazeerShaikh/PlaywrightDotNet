using JobScrape.Domain.Access;
using JobScrape.Domain.Common;

namespace JobScrape.Application.Abstractions
{
    /// <summary>
    /// Port for per-source daily <see cref="SourceQuota"/> rows; unique per
    /// <c>(source, UTC day)</c>, so restarts never reset a day.
    /// </summary>
    public interface ISourceQuotaRepository
    {
        /// <summary>
        /// Finds the quota of a source for a UTC day, or null when none was started.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="quotaDate">The UTC day.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The quota, or null.</returns>
        Task<SourceQuota?> FindAsync(
            SourceKey source,
            DateOnly quotaDate,
            CancellationToken cancellationToken
        );

        /// <summary>
        /// Stages a new day's quota; persisted by <see cref="IUnitOfWork.SaveChangesAsync"/>.
        /// </summary>
        /// <param name="quota">The quota.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>A task that completes when the quota is staged.</returns>
        Task AddAsync(SourceQuota quota, CancellationToken cancellationToken);
    }
}
