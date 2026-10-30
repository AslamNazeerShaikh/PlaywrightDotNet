using JobScrape.Application.Features.Health;
using Microsoft.AspNetCore.Mvc;

namespace JobScrape.Api.Controllers
{
    /// <summary>
    /// Liveness probe for the cockpit and schedulers.
    /// </summary>
    [ApiController]
    [Route("api/v1/health")]
    public sealed class HealthController : ControllerBase
    {
        private readonly GetHealthHandler _handler;

        public HealthController(GetHealthHandler handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            _handler = handler;
        }

        [HttpGet]
        public async Task<ActionResult<GetHealthResponse>> GetAsync(CancellationToken cancellationToken)
        {
            var response = await _handler.HandleAsync(cancellationToken);
            return Ok(response);
        }
    }
}
