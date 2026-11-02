using JobScrape.Api.Http;
using JobScrape.Application.Features.SearchProfiles;
using Microsoft.AspNetCore.Mvc;

namespace JobScrape.Api.Controllers
{
    /// <summary>
    /// Saved search profiles. Delegates to explicit use-case handlers; this
    /// controller owns only HTTP mapping via <see cref="ErrorMapper"/>.
    /// </summary>
    [ApiController]
    [Route("api/v1/search-profiles")]
    public sealed class SearchProfilesController : ControllerBase
    {
        // Constructor-injected handlers, never mutated.
        private readonly GetSearchProfileHandler _get;
        private readonly ListSearchProfilesHandler _list;
        private readonly CreateSearchProfileHandler _create;

        /// <summary>
        /// Initializes a new instance of the <see cref="SearchProfilesController"/> class.
        /// </summary>
        /// <param name="get">The lookup handler.</param>
        /// <param name="list">The list handler.</param>
        /// <param name="create">The create handler.</param>
        /// <exception cref="ArgumentNullException">Thrown when any handler is null.</exception>
        public SearchProfilesController(
            GetSearchProfileHandler get,
            ListSearchProfilesHandler list,
            CreateSearchProfileHandler create
        )
        {
            ArgumentNullException.ThrowIfNull(get);
            ArgumentNullException.ThrowIfNull(list);
            ArgumentNullException.ThrowIfNull(create);
            _get = get;
            _list = list;
            _create = create;
        }

        /// <summary>
        /// Lists every profile ordered by name.
        /// </summary>
        /// <param name="cancellationToken">Token to observe for client disconnect.</param>
        /// <returns>HTTP 200 with the profile list.</returns>
        [HttpGet]
        public async Task<ActionResult<SearchProfileListResponse>> ListAsync(
            CancellationToken cancellationToken
        )
        {
            return Ok(await _list.HandleAsync(cancellationToken));
        }

        /// <summary>
        /// Returns the profile for <paramref name="id"/>.
        /// </summary>
        /// <param name="id">The requested profile identifier.</param>
        /// <param name="cancellationToken">Token to observe for client disconnect.</param>
        /// <returns>
        /// HTTP 200 with the profile, or a problem response: 400 when
        /// <paramref name="id"/> is empty, 404 when absent.
        /// </returns>
        [HttpGet("{id:guid}")]
        public async Task<IResult> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            var result = await _get.HandleAsync(id, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
        }

        /// <summary>
        /// Creates a profile.
        /// </summary>
        /// <param name="request">The profile to create.</param>
        /// <param name="cancellationToken">Token to observe for client disconnect.</param>
        /// <returns>
        /// HTTP 201 with the profile and its <c>Location</c>, or a problem
        /// response: 400 with field errors, 409 when the name is taken.
        /// </returns>
        [HttpPost]
        public async Task<IResult> CreateAsync(
            CreateSearchProfileRequest request,
            CancellationToken cancellationToken
        )
        {
            var result = await _create.HandleAsync(request, cancellationToken);
            return result.IsSuccess
                ? Results.Created($"/api/v1/search-profiles/{result.Value.Id}", result.Value)
                : result.Error.ToProblem();
        }
    }
}
