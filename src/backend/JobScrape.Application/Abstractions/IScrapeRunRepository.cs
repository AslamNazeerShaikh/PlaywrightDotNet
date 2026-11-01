using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;

namespace JobScrape.Application.Abstractions
{
    /// <summary>
    /// Write-side port for <see cref="ScrapeRun"/> aggregates.
    /// </summary>
    public interface IScrapeRunRepository
    {
        /// <summary>
        /// Loads a run, or null when absent.
        /// </summary>
        /// <param name="id">The run identifier.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The run, or null.</returns>
        Task<ScrapeRun?> GetAsync(ScrapeRunId id, CancellationToken cancellationToken);

        /// <summary>
        /// Stages a new run; persisted by <see cref="IUnitOfWork.SaveChangesAsync"/>.
        /// </summary>
        /// <param name="run">The run.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>A task that completes when the run is staged.</returns>
        Task AddAsync(ScrapeRun run, CancellationToken cancellationToken);
    }
}
