using System.ComponentModel;
using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.Health;
using ModelContextProtocol.Server;

namespace JobScrape.Mcp.Tools
{
    /// <summary>
    /// The liveness tool. Read-only and infallible by contract.
    /// </summary>
    [McpServerToolType]
    public sealed class HealthTools
    {
        private readonly GetHealthHandler _health;

        /// <summary>
        /// Initializes a new instance of the <see cref="HealthTools"/> class.
        /// </summary>
        /// <param name="health">The liveness handler. Must not be null.</param>
        /// <param name="caller">
        /// The calling actor. Read tools ignore it; the parameter exists so
        /// future authorization needs no signature change. Must not be null.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public HealthTools(GetHealthHandler health, ICallerContext caller)
        {
            ArgumentNullException.ThrowIfNull(health);
            ArgumentNullException.ThrowIfNull(caller);
            _health = health;
        }

        /// <summary>
        /// Returns the current liveness payload.
        /// </summary>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The health payload.</returns>
        [McpServerTool(Name = "get_health")]
        [Description("Returns the current service liveness payload.")]
        public Task<GetHealthResponse> GetHealthAsync(CancellationToken cancellationToken)
        {
            return _health.HandleAsync(cancellationToken);
        }
    }
}
