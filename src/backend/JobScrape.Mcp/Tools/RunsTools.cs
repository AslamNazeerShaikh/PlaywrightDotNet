using System.ComponentModel;
using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.Runs;
using ModelContextProtocol.Server;

namespace JobScrape.Mcp.Tools
{
    /// <summary>
    /// The run inspection tools. Read-only; single-run reads report
    /// <c>VALIDATION_FAILED</c> and <c>RUN_NOT_FOUND</c> as tool errors.
    /// </summary>
    [McpServerToolType]
    public sealed class RunsTools
    {
        private readonly ListRunsHandler _list;
        private readonly GetRunHandler _get;
        private readonly ListRunPostingsHandler _postings;

        /// <summary>
        /// Initializes a new instance of the <see cref="RunsTools"/> class.
        /// </summary>
        /// <param name="list">The run list handler. Must not be null.</param>
        /// <param name="get">The single-run handler. Must not be null.</param>
        /// <param name="postings">The run postings handler. Must not be null.</param>
        /// <param name="caller">
        /// The calling actor. Read tools ignore it; the parameter exists so
        /// future authorization needs no signature change. Must not be null.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public RunsTools(
            ListRunsHandler list,
            GetRunHandler get,
            ListRunPostingsHandler postings,
            ICallerContext caller
        )
        {
            ArgumentNullException.ThrowIfNull(list);
            ArgumentNullException.ThrowIfNull(get);
            ArgumentNullException.ThrowIfNull(postings);
            ArgumentNullException.ThrowIfNull(caller);
            _list = list;
            _get = get;
            _postings = postings;
        }

        /// <summary>
        /// Lists the most recent runs, newest first.
        /// </summary>
        /// <param name="offset">How many newest runs to skip.</param>
        /// <param name="limit">How many runs to return; 1..500.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The runs, newest first, and the total count.</returns>
        [McpServerTool(Name = "list_runs")]
        [Description("Lists the most recent runs, newest first.")]
        public Task<RunListResponse> ListRunsAsync(
            [Description("How many newest runs to skip.")] int offset = 0,
            [Description("How many runs to return, 1 to 500.")] int limit = 50,
            CancellationToken cancellationToken = default
        )
        {
            return _list.HandleAsync(offset, limit, cancellationToken);
        }

        /// <summary>
        /// Gets one run by id.
        /// </summary>
        /// <param name="id">The run identifier.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The run.</returns>
        [McpServerTool(Name = "get_run")]
        [Description("Gets one run by its identifier.")]
        public async Task<RunResponse> GetRunAsync(
            [Description("The run identifier.")] string id,
            CancellationToken cancellationToken = default
        )
        {
            var runId = McpErrors.ParseIdOrThrow(id, "A run id is required.");
            return McpErrors.OrThrow(await _get.HandleAsync(runId, cancellationToken));
        }

        /// <summary>
        /// Lists the postings one run saw, most recently seen first.
        /// </summary>
        /// <param name="runId">The run identifier.</param>
        /// <param name="offset">How many postings to skip.</param>
        /// <param name="limit">How many postings to return; 1..500.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The postings and the total count.</returns>
        [McpServerTool(Name = "list_run_postings")]
        [Description("Lists the postings one run saw, most recently seen first.")]
        public async Task<RunPostingListResponse> ListRunPostingsAsync(
            [Description("The run identifier.")] string runId,
            [Description("How many postings to skip.")] int offset = 0,
            [Description("How many postings to return, 1 to 500.")] int limit = 200,
            CancellationToken cancellationToken = default
        )
        {
            var id = McpErrors.ParseIdOrThrow(runId, "A run id is required.");
            return McpErrors.OrThrow(
                await _postings.HandleAsync(id, offset, limit, cancellationToken)
            );
        }
    }
}
