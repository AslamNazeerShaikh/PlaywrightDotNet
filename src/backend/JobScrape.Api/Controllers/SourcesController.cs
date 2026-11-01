using JobScrape.Application.Features.Sources;
using Microsoft.AspNetCore.Mvc;

namespace JobScrape.Api.Controllers
{
    /// <summary>
    /// Configured job sources with their policy, compliance gate, today's
    /// quota usage, and circuit state. Read-only.
    /// </summary>
    [ApiController]
    [Route("api/v1/sources")]
    public sealed class SourcesController : ControllerBase
    {
        // Constructor-injected handler, never mutated.
        private readonly ListSourcesHandler _list;

        /// <summary>
        /// Initializes a new instance of the <see cref="SourcesController"/> class.
        /// </summary>
        /// <param name="list">The list handler.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="list"/> is null.</exception>
        public SourcesController(ListSourcesHandler list)
        {
            ArgumentNullException.ThrowIfNull(list);
            _list = list;
        }

        /// <summary>
        /// Lists every configured source, ordered by key.
        /// </summary>
        /// <param name="cancellationToken">Token to observe for client disconnect.</param>
        /// <returns>HTTP 200 with the source snapshot.</returns>
        [HttpGet]
        public async Task<ActionResult<SourceListResponse>> ListAsync(
            CancellationToken cancellationToken
        )
        {
            return Ok(await _list.HandleAsync(cancellationToken));
        }
    }
}
