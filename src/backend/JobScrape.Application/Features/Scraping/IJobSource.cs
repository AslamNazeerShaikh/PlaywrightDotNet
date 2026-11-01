using JobScrape.Domain.Common;

namespace JobScrape.Application.Features.Scraping
{
    /// <summary>
    /// A job-board adapter (docs/02-architecture.md §8.1). Implementations
    /// live in Infrastructure, classify walls instead of throwing for them,
    /// and never retry on their own: retries are the handler's decision.
    /// </summary>
    public interface IJobSource
    {
        /// <summary>
        /// Gets the source this adapter serves.
        /// </summary>
        SourceKey Key { get; }

        /// <summary>
        /// Gets what the adapter supports.
        /// </summary>
        SourceCapabilities Capabilities { get; }

        /// <summary>
        /// Fetches and parses one result page.
        /// </summary>
        /// <param name="context">The run being served.</param>
        /// <param name="request">Which page to fetch.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The classified page.</returns>
        Task<SearchPageResult> SearchPageAsync(
            SourceRunContext context,
            SearchPageRequest request,
            CancellationToken cancellationToken
        );

        /// <summary>
        /// Fetches and parses one posting's detail page.
        /// </summary>
        /// <param name="context">The run being served.</param>
        /// <param name="job">The posting to enrich.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The classified detail.</returns>
        Task<JobDetailResult> GetDetailAsync(
            SourceRunContext context,
            JobReference job,
            CancellationToken cancellationToken
        );
    }
}
