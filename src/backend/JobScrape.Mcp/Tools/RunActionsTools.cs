using System.ComponentModel;
using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.Runs;
using ModelContextProtocol.Server;

namespace JobScrape.Mcp.Tools
{
    /// <summary>
    /// The run action tools. Starting a run returns the recorded run at
    /// once; the agent polls <c>get_run</c> for progress. Expected handler
    /// failures surface as tool errors carrying the failure code.
    /// </summary>
    [McpServerToolType]
    public sealed class RunActionsTools
    {
        private readonly StartRunHandler _start;
        private readonly CancelRunHandler _cancel;
        private readonly ICallerContext _caller;

        /// <summary>
        /// Initializes a new instance of the <see cref="RunActionsTools"/> class.
        /// </summary>
        /// <param name="start">The run starter. Must not be null.</param>
        /// <param name="cancel">The run canceller. Must not be null.</param>
        /// <param name="caller">
        /// The calling actor. Must hold <c>runs.write</c>. Must not be null.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public RunActionsTools(
            StartRunHandler start,
            CancelRunHandler cancel,
            ICallerContext caller
        )
        {
            ArgumentNullException.ThrowIfNull(start);
            ArgumentNullException.ThrowIfNull(cancel);
            ArgumentNullException.ThrowIfNull(caller);
            _start = start;
            _cancel = cancel;
            _caller = caller;
        }

        /// <summary>
        /// Starts a run in the background and returns the recorded run.
        /// </summary>
        /// <param name="profileId">The search profile identifier.</param>
        /// <param name="browserProfileId">The browser profile identifier bound to the profile source.</param>
        /// <param name="enrich">Whether to visit detail pages after searching.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The started run as recorded; poll <c>get_run</c> for progress.</returns>
        [McpServerTool(Name = "start_run")]
        [Description(
            "Starts a run in the background and returns the recorded run. Poll get_run for progress."
        )]
        public async Task<RunResponse> StartRunAsync(
            [Description("The search profile identifier.")] string profileId,
            [Description("The browser profile identifier bound to the profile source.")]
                string browserProfileId,
            [Description("Whether to visit detail pages after searching.")] bool enrich = true,
            CancellationToken cancellationToken = default
        )
        {
            McpAuth.RequirePermission(_caller, "runs.write");
            var profile = McpErrors.ParseIdOrThrow(profileId, "A profile id is required.");
            var browser = McpErrors.ParseIdOrThrow(
                browserProfileId,
                "A browser profile id is required."
            );
            return McpErrors.OrThrow(
                await _start.HandleAsync(
                    new StartRunRequest(profile, browser, enrich),
                    cancellationToken
                )
            );
        }

        /// <summary>
        /// Cancels a run, idempotently; a terminal run is returned as it is.
        /// </summary>
        /// <param name="id">The run identifier.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The run as it stands.</returns>
        [McpServerTool(Name = "cancel_run")]
        [Description("Cancels a run, idempotently; a terminal run is returned as it is.")]
        public async Task<RunResponse> CancelRunAsync(
            [Description("The run identifier.")] string id,
            CancellationToken cancellationToken = default
        )
        {
            McpAuth.RequirePermission(_caller, "runs.write");
            var runId = McpErrors.ParseIdOrThrow(id, "A run id is required.");
            return McpErrors.OrThrow(await _cancel.HandleAsync(runId, cancellationToken));
        }
    }
}
