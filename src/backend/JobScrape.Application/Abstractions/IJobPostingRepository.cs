using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;

namespace JobScrape.Application.Abstractions
{
    /// <summary>
    /// Write-side port for <see cref="JobPosting"/> aggregates, keyed by
    /// their immutable <c>(source, external id)</c> identity. The store
    /// enforces that identity as unique.
    /// </summary>
    public interface IJobPostingRepository
    {
        /// <summary>
        /// Finds a posting by identity, including postings staged but not yet saved.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="externalJobId">The source's job id.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The posting, or null.</returns>
        Task<JobPosting?> FindAsync(
            SourceKey source,
            ExternalJobId externalJobId,
            CancellationToken cancellationToken
        );

        /// <summary>
        /// Stages a new posting; persisted by <see cref="IUnitOfWork.SaveChangesAsync"/>.
        /// </summary>
        /// <param name="posting">The posting.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>A task that completes when the posting is staged.</returns>
        Task AddAsync(JobPosting posting, CancellationToken cancellationToken);
    }
}
