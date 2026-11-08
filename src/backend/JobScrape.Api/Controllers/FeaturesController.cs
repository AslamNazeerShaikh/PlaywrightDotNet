using JobScrape.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobScrape.Api.Controllers
{
    /// <summary>
    /// Anonymous capability report for the cockpit. This controller owns only
    /// HTTP mapping; the values come straight from the <see cref="FeatureFlags"/>
    /// singleton bound at host startup.
    /// </summary>
    [ApiController]
    [Route("api/v1/features")]
    public sealed class FeaturesController : ControllerBase
    {
        // Constructor-injected flags, never mutated: concurrent requests
        // share the controller type but no state flows through this field.
        private readonly FeatureFlags _flags;

        /// <summary>
        /// Initializes a new instance of the <see cref="FeaturesController"/> class.
        /// </summary>
        /// <param name="flags">The feature flags bound at host startup. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="flags"/> is null.</exception>
        public FeaturesController(FeatureFlags flags)
        {
            ArgumentNullException.ThrowIfNull(flags);
            _flags = flags;
        }

        /// <summary>
        /// Returns the five shipped feature switches as camelCase JSON.
        /// </summary>
        /// <returns>HTTP 200 with the current <see cref="FeatureFlags"/>.</returns>
        [HttpGet]
        [AllowAnonymous]
        public ActionResult<FeatureFlags> Get()
        {
            // Infallible by contract: the flags are bound and validated at
            // startup, so there is no failure branch to map.
            return Ok(_flags);
        }
    }
}
