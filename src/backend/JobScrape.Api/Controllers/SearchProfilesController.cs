using JobScrape.Api.Http;
using JobScrape.Application.Features.SearchProfiles;
using Microsoft.AspNetCore.Mvc;

namespace JobScrape.Api.Controllers
{
    /// <summary>
    /// Saved search profiles. Delegates to the
    /// <see cref="GetSearchProfileHandler"/> use-case handler; this controller
    /// owns only HTTP mapping via <see cref="ErrorMapper"/>, never lookup logic.
    /// </summary>
    [ApiController]
    [Route("api/v1/search-profiles")]
    public sealed class SearchProfilesController : ControllerBase
    {
        // Constructor-injected handler, never mutated: concurrent requests
        // share the controller type but no state flows through this field.
        private readonly GetSearchProfileHandler _handler;

        /// <summary>
        /// Initializes a new instance of the <see cref="SearchProfilesController"/> class.
        /// </summary>
        /// <param name="handler">The profile-lookup use-case handler. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is null.</exception>
        public SearchProfilesController(GetSearchProfileHandler handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            _handler = handler;
        }

        /// <summary>
        /// Returns the profile summary for <paramref name="id"/>.
        /// </summary>
        /// <param name="id">The requested profile identifier.</param>
        /// <param name="cancellationToken">Token to observe for client disconnect.</param>
        /// <returns>
        /// HTTP 200 with the profile summary, or a problem response:
        /// 400 when <paramref name="id"/> is empty, 404 when absent.
        /// </returns>
        [HttpGet("{id:guid}")]
        public async Task<IResult> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            // The handler owns the failure semantics; the edge only maps the
            // typed error to its HTTP shape. Success stays a plain 200.
            var result = await _handler.HandleAsync(id, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
        }
    }
}
