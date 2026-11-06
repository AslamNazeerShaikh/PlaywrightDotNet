using System.ComponentModel;
using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.Sources;
using ModelContextProtocol.Server;

namespace JobScrape.Mcp.Tools
{
    /// <summary>
    /// The source snapshot tool. Read-only and infallible by contract.
    /// </summary>
    [McpServerToolType]
    public sealed class SourcesTools
    {
        private readonly ListSourcesHandler _sources;
        private readonly ICallerContext _caller;

        /// <summary>
        /// Initializes a new instance of the <see cref="SourcesTools"/> class.
        /// </summary>
        /// <param name="sources">The source list handler. Must not be null.</param>
        /// <param name="caller">
        /// The calling actor. Must hold <c>sources.read</c>. Must not be null.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public SourcesTools(ListSourcesHandler sources, ICallerContext caller)
        {
            ArgumentNullException.ThrowIfNull(sources);
            ArgumentNullException.ThrowIfNull(caller);
            _sources = sources;
            _caller = caller;
        }

        /// <summary>
        /// Returns every configured source's current status.
        /// </summary>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The sources ordered by key.</returns>
        [McpServerTool(Name = "list_sources")]
        [Description("Returns every configured source with its policy, quota, and circuit state.")]
        public Task<SourceListResponse> ListSourcesAsync(
            CancellationToken cancellationToken = default
        )
        {
            McpAuth.RequirePermission(_caller, "sources.read");
            return _sources.HandleAsync(cancellationToken);
        }
    }
}
