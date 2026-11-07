using System.ComponentModel.DataAnnotations;
using JobScrape.Api.Http;
using JobScrape.Api.Http.Auth;
using JobScrape.Application.Features.Quotas;
using JobScrape.Application.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace JobScrape.Api.Controllers
{
    /// <summary>
    /// Usage quota administration: list and update the per-user request
    /// budgets for REST and web MCP. Admin role only. Data-annotation
    /// failures map to coded validation problems below because the
    /// automatic 400 is suppressed in Program.
    /// </summary>
    [ApiController]
    [Route("api/v1/quotas")]
    [Authorize(Policy = AuthPolicies.RequireAdmin)]
    public sealed class UsageQuotasController : ControllerBase
    {
        private readonly ListUsageQuotasHandler _list;
        private readonly UpdateUsageQuotaHandler _update;
        private readonly IMemoryCache _cache;

        /// <summary>
        /// Initializes a new instance of the <see cref="UsageQuotasController"/> class.
        /// </summary>
        /// <param name="list">Lists definitions. Must not be null.</param>
        /// <param name="update">Updates one definition. Must not be null.</param>
        /// <param name="cache">Evicted on a successful update. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public UsageQuotasController(
            ListUsageQuotasHandler list,
            UpdateUsageQuotaHandler update,
            IMemoryCache cache
        )
        {
            ArgumentNullException.ThrowIfNull(list);
            ArgumentNullException.ThrowIfNull(update);
            ArgumentNullException.ThrowIfNull(cache);
            _list = list;
            _update = update;
            _cache = cache;
        }

        /// <summary>
        /// Lists every usage quota definition, in scope order.
        /// </summary>
        /// <param name="cancellationToken">Aborted when the client disconnects.</param>
        /// <returns>200 with the definitions.</returns>
        [HttpGet]
        public async Task<ActionResult<QuotaListResponse>> ListAsync(
            CancellationToken cancellationToken
        )
        {
            return Ok(new QuotaListResponse((await _list.HandleAsync(cancellationToken)).Quotas));
        }

        /// <summary>
        /// Updates one scope's limit and window. A missing definition is
        /// created, so a dropped row is repaired without a migration.
        /// </summary>
        /// <param name="scope">The scope name: <c>Api</c> or <c>Mcp</c>.</param>
        /// <param name="request">The new limit and window.</param>
        /// <param name="cancellationToken">Aborted when the client disconnects.</param>
        /// <returns>
        /// 200 with the updated definition; 404 <c>UNKNOWN_QUOTA_SCOPE</c>;
        /// 400 with field errors for an out-of-range limit or window.
        /// </returns>
        [HttpPut("{scope}")]
        public async Task<IResult> UpdateAsync(
            string scope,
            [FromBody] UpdateQuotaRequest? request,
            CancellationToken cancellationToken
        )
        {
            if (request is null)
            {
                return Errors
                    .ValidationFailed(
                        "The request body is required.",
                        new Dictionary<string, string[]>
                        {
                            ["body"] = ["The request body is required."],
                        }
                    )
                    .ToProblem();
            }

            var invalid = ModelProblem();
            if (invalid is not null)
            {
                return invalid;
            }

            if (!TimeSpan.TryParse(request!.Window, out var window))
            {
                return Errors
                    .ValidationFailed(
                        $"Window '{request.Window}' is not a valid time span.",
                        new Dictionary<string, string[]>
                        {
                            ["window"] = [$"Window '{request.Window}' is not a valid time span."],
                        }
                    )
                    .ToProblem();
            }

            var result = await _update.HandleAsync(scope, request.Limit, window, cancellationToken);
            if (result.IsFailure)
            {
                return result.Error.ToProblem();
            }

            _cache.Remove(UsageQuotaMiddleware.DefinitionsCacheKey);
            return Results.Ok(result.Value);
        }

        /// <summary>
        /// Maps data-annotation failures to a coded validation problem.
        /// </summary>
        /// <returns>The problem, or null when the model is valid.</returns>
        private IResult? ModelProblem()
        {
            if (ModelState.IsValid)
            {
                return null;
            }

            var errors = ModelState
                .Where(pair => pair.Value?.Errors.Count > 0)
                .ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value!.Errors.Select(error => error.ErrorMessage).ToArray(),
                    StringComparer.Ordinal
                );
            return Errors
                .ValidationFailed("The request contains invalid fields.", errors)
                .ToProblem();
        }
    }

    /// <summary>
    /// The payload for updating one scope's quota.
    /// </summary>
    public sealed record UpdateQuotaRequest
    {
        /// <summary>Gets the requests one user may make per window. At least 1.</summary>
        [Range(1, int.MaxValue)]
        public int Limit { get; init; }

        /// <summary>Gets the fixed window, e.g. <c>00:01:00</c> or <c>1.00:00:00</c>.</summary>
        [Required]
        public string? Window { get; init; }
    }
}
