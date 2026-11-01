using JobScrape.Api.Http;
using JobScrape.Application.Features.Sessions;
using Microsoft.AspNetCore.Mvc;

namespace JobScrape.Api.Controllers
{
    /// <summary>
    /// Browser profiles and their session state. Logging in and probing need
    /// the Playwright session manager and are not exposed yet.
    /// </summary>
    [ApiController]
    [Route("api/v1/sessions")]
    public sealed class SessionsController : ControllerBase
    {
        // Constructor-injected handlers, never mutated.
        private readonly ListSessionsHandler _list;
        private readonly GetSessionHandler _get;
        private readonly CreateSessionHandler _create;

        /// <summary>
        /// Initializes a new instance of the <see cref="SessionsController"/> class.
        /// </summary>
        /// <param name="list">The list handler.</param>
        /// <param name="get">The lookup handler.</param>
        /// <param name="create">The create handler.</param>
        /// <exception cref="ArgumentNullException">Thrown when any handler is null.</exception>
        public SessionsController(
            ListSessionsHandler list,
            GetSessionHandler get,
            CreateSessionHandler create
        )
        {
            ArgumentNullException.ThrowIfNull(list);
            ArgumentNullException.ThrowIfNull(get);
            ArgumentNullException.ThrowIfNull(create);
            _list = list;
            _get = get;
            _create = create;
        }

        /// <summary>
        /// Lists every browser profile ordered by name.
        /// </summary>
        /// <param name="cancellationToken">Token to observe for client disconnect.</param>
        /// <returns>HTTP 200 with the profile list.</returns>
        [HttpGet]
        public async Task<ActionResult<SessionListResponse>> ListAsync(
            CancellationToken cancellationToken
        )
        {
            return Ok(await _list.HandleAsync(cancellationToken));
        }

        /// <summary>
        /// Returns the browser profile for <paramref name="id"/>.
        /// </summary>
        /// <param name="id">The requested profile identifier.</param>
        /// <param name="cancellationToken">Token to observe for client disconnect.</param>
        /// <returns>HTTP 200, or a problem: 400 for an empty id, 404 when absent.</returns>
        [HttpGet("{id:guid}")]
        public async Task<IResult> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            var result = await _get.HandleAsync(id, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
        }

        /// <summary>
        /// Creates a browser profile in the <c>New</c> state.
        /// </summary>
        /// <param name="request">The profile to create.</param>
        /// <param name="cancellationToken">Token to observe for client disconnect.</param>
        /// <returns>
        /// HTTP 201 with the profile and its <c>Location</c>, or a problem:
        /// 400 with field errors, 409 when the name is taken.
        /// </returns>
        [HttpPost]
        public async Task<IResult> CreateAsync(
            CreateSessionRequest request,
            CancellationToken cancellationToken
        )
        {
            var result = await _create.HandleAsync(request, cancellationToken);
            return result.IsSuccess
                ? Results.Created($"/api/v1/sessions/{result.Value.Id}", result.Value)
                : result.Error.ToProblem();
        }
    }
}
