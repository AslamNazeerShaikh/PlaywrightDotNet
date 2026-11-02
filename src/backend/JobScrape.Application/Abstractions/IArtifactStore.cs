using JobScrape.Application.Features.Scraping;
using JobScrape.Domain.Access;

namespace JobScrape.Application.Abstractions
{
    /// <summary>
    /// Captures evidence (screenshot, capped HTML, trace) of the page the run
    /// is on when it hits a wall or a changed page. Never stores cookies or
    /// authorization headers.
    /// </summary>
    public interface IArtifactStore
    {
        /// <summary>
        /// Captures the current page of the run's browser session.
        /// </summary>
        /// <param name="context">The run whose page to capture.</param>
        /// <param name="assessment">Why the capture is taken.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The artifact directory, relative to the artifacts root.</returns>
        Task<string> CaptureAsync(
            SourceRunContext context,
            ChallengeAssessment assessment,
            CancellationToken cancellationToken
        );
    }
}
