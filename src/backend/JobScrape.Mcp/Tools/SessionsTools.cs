using System.ComponentModel;
using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.Sessions;
using ModelContextProtocol.Server;

namespace JobScrape.Mcp.Tools
{
    /// <summary>
    /// The browser profile tools. Read-only; single-profile reads report
    /// <c>VALIDATION_FAILED</c> and <c>SESSION_NOT_FOUND</c> as tool errors.
    /// </summary>
    [McpServerToolType]
    public sealed class SessionsTools
    {
        private readonly ListSessionsHandler _list;
        private readonly GetSessionHandler _get;
        private readonly ICallerContext _caller;

        /// <summary>
        /// Initializes a new instance of the <see cref="SessionsTools"/> class.
        /// </summary>
        /// <param name="list">The session list handler. Must not be null.</param>
        /// <param name="get">The single-session handler. Must not be null.</param>
        /// <param name="caller">
        /// The calling actor. Must hold <c>sessions.read</c>. Must not be null.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public SessionsTools(ListSessionsHandler list, GetSessionHandler get, ICallerContext caller)
        {
            ArgumentNullException.ThrowIfNull(list);
            ArgumentNullException.ThrowIfNull(get);
            ArgumentNullException.ThrowIfNull(caller);
            _list = list;
            _get = get;
            _caller = caller;
        }

        /// <summary>
        /// Returns every browser profile ordered by name.
        /// </summary>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The profile list.</returns>
        [McpServerTool(Name = "list_sessions")]
        [Description("Returns every browser profile ordered by name.")]
        public Task<SessionListResponse> ListSessionsAsync(
            CancellationToken cancellationToken = default
        )
        {
            McpAuth.RequirePermission(_caller, "sessions.read");
            return _list.HandleAsync(cancellationToken);
        }

        /// <summary>
        /// Returns the browser profile for one id.
        /// </summary>
        /// <param name="id">The requested profile identifier.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The browser profile.</returns>
        [McpServerTool(Name = "get_session")]
        [Description("Returns one browser profile by its identifier.")]
        public async Task<SessionResponse> GetSessionAsync(
            [Description("The profile identifier.")] string id,
            CancellationToken cancellationToken = default
        )
        {
            McpAuth.RequirePermission(_caller, "sessions.read");
            var sessionId = McpErrors.ParseIdOrThrow(id, "A session id is required.");
            return McpErrors.OrThrow(await _get.HandleAsync(sessionId, cancellationToken));
        }
    }
}
