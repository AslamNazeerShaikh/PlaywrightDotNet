using JobScrape.Application.Features.Scraping;
using JobScrape.Application.Results;

namespace JobScrape.Application.Features.Runs
{
    /// <summary>
    /// Executes runs in the background, one at a time. The host owns the
    /// implementation because a run outlives the request that starts it.
    /// </summary>
    public interface IRunLauncher
    {
        /// <summary>
        /// Starts a run and returns once it is recorded (or refused by a
        /// gate); the scrape itself continues in the background.
        /// </summary>
        /// <param name="request">The run request. Must not be null.</param>
        /// <param name="cancellationToken">Token to observe while waiting for the run to start.</param>
        /// <returns>The new run's id; <c>RUN_IN_PROGRESS</c> while another run executes; or the gate's error.</returns>
        Task<Result<Guid>> StartAsync(
            RunScrapeRequest request,
            CancellationToken cancellationToken
        );

        /// <summary>
        /// Requests cooperative cancellation of an executing run. The run
        /// records partial results and ends <c>Cancelled</c>.
        /// </summary>
        /// <param name="runId">The run identifier.</param>
        /// <returns>True when the run is executing here and was signalled.</returns>
        bool TryCancel(Guid runId);
    }
}
