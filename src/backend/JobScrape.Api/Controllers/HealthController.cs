using JobScrape.Api.Http;
using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.Health;
using JobScrape.Application.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobScrape.Api.Controllers
{
    /// <summary>
    /// Liveness probe for the cockpit and schedulers. Delegates to the
    /// <see cref="GetHealthHandler"/> use-case handler; this controller owns
    /// only HTTP mapping, never health logic. When <see cref="FeatureFlags.Health"/>
    /// is false the route answers the feature-disabled 404 problem; MCP
    /// <c>get_health</c> stays alive regardless by design.
    /// </summary>
    [ApiController]
    [Route("api/v1/health")]
    public sealed class HealthController : ControllerBase
    {
        // Constructor-injected handler and flags, never mutated: concurrent
        // requests share the controller type but no state flows through
        // these fields.
        private readonly GetHealthHandler _handler;
        private readonly FeatureFlags _flags;

        /// <summary>
        /// Initializes a new instance of the <see cref="HealthController"/> class.
        /// </summary>
        /// <param name="handler">The liveness use-case handler. Must not be null.</param>
        /// <param name="flags">The feature flags bound at host startup. Must not be null.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="handler"/> or <paramref name="flags"/> is null.
        /// </exception>
        public HealthController(GetHealthHandler handler, FeatureFlags flags)
        {
            ArgumentNullException.ThrowIfNull(handler);
            ArgumentNullException.ThrowIfNull(flags);
            _handler = handler;
            _flags = flags;
        }

        /// <summary>
        /// Returns the current liveness payload.
        /// </summary>
        /// <param name="cancellationToken">Token to observe for client disconnect.</param>
        /// <returns>
        /// 200 with the current <see cref="GetHealthResponse"/>; 404
        /// <c>FEATURE_DISABLED</c> when the Health flag is off.
        /// </returns>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IResult> GetAsync(CancellationToken cancellationToken)
        {
            // The disabled route answers the 404 problem with the stable
            // FEATURE_DISABLED code the UI routes on — not an absent route:
            // an absent route answers 401 to anonymous callers under the
            // auth fallback, while this route stays anonymous by design.
            // The MCP tool and the handler stay registered on purpose.
            if (!_flags.Health)
            {
                return Errors.FeatureDisabled().ToProblem();
            }

            // Liveness is infallible by contract, so there is no failure
            // branch to map: a thrown exception becomes a 500 via the global
            // exception handler, never a health payload.
            var response = await _handler.HandleAsync(cancellationToken);
            return Results.Ok(response);
        }
    }
}
