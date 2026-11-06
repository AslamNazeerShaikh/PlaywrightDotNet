using JobScrape.Application.Features.Health;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobScrape.Api.Controllers
{
    /// <summary>
    /// Liveness probe for the cockpit and schedulers. Delegates to the
    /// <see cref="GetHealthHandler"/> use-case handler; this controller owns
    /// only HTTP mapping, never health logic.
    /// </summary>
    [ApiController]
    [Route("api/v1/health")]
    public sealed class HealthController : ControllerBase
    {
        // Constructor-injected handler, never mutated: concurrent requests
        // share the controller type but no state flows through this field.
        private readonly GetHealthHandler _handler;

        /// <summary>
        /// Initializes a new instance of the <see cref="HealthController"/> class.
        /// </summary>
        /// <param name="handler">The liveness use-case handler. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is null.</exception>
        public HealthController(GetHealthHandler handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            _handler = handler;
        }

        /// <summary>
        /// Returns the current liveness payload.
        /// </summary>
        /// <param name="cancellationToken">Token to observe for client disconnect.</param>
        /// <returns>HTTP 200 with the current <see cref="GetHealthResponse"/>.</returns>
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<GetHealthResponse>> GetAsync(
            CancellationToken cancellationToken
        )
        {
            // Liveness is infallible by contract, so there is no failure
            // branch to map: a thrown exception becomes a 500 via the global
            // exception handler, never a health payload.
            var response = await _handler.HandleAsync(cancellationToken);
            return Ok(response);
        }
    }
}
