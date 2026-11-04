using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;

namespace JobScrape.Application.Abstractions
{
    /// <summary>
    /// Write-side port for per-run <see cref="ScrapeRunProgress"/> rows.
    /// </summary>
    public interface IScrapeRunProgressRepository
    {
        /// <summary>
        /// Loads a run's saved position, or null when no page was recorded yet.
        /// </summary>
        /// <param name="id">The run identifier.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The position, or null.</returns>
        Task<ScrapeRunProgress?> GetAsync(ScrapeRunId id, CancellationToken cancellationToken);

        /// <summary>
        /// Stages a position insert or update; persisted by
        /// <see cref="IUnitOfWork.SaveChangesAsync"/>.
        /// </summary>
        /// <param name="progress">The position. Must not be null.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>A task that completes when the position is staged.</returns>
        Task SaveAsync(ScrapeRunProgress progress, CancellationToken cancellationToken);
    }
}
