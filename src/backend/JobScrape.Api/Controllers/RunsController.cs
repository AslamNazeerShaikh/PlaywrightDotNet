using JobScrape.Api.Http;
using JobScrape.Api.Http.Auth;
using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.Runs;
using JobScrape.Application.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobScrape.Api.Controllers
{
    /// <summary>
    /// Scrape runs: start one, follow it, cancel it, and read what it found
    /// (docs/12-web-cockpit.md, runs endpoints).
    /// </summary>
    [ApiController]
    [Route("api/v1/runs")]
    public sealed class RunsController : ControllerBase
    {
        // Constructor-injected handlers and flags, never mutated.
        private readonly StartRunHandler _start;
        private readonly ListRunsHandler _list;
        private readonly GetRunHandler _get;
        private readonly CancelRunHandler _cancel;
        private readonly ListRunPostingsHandler _postings;
        private readonly ExportRunPostingsHandler _export;
        private readonly FeatureFlags _flags;

        /// <summary>
        /// Initializes a new instance of the <see cref="RunsController"/> class.
        /// </summary>
        /// <param name="start">Starts runs.</param>
        /// <param name="list">Lists runs.</param>
        /// <param name="get">Gets one run.</param>
        /// <param name="cancel">Cancels runs.</param>
        /// <param name="postings">Lists a run's postings.</param>
        /// <param name="export">Exports a run's postings.</param>
        /// <param name="flags">The feature flags bound at host startup.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public RunsController(
            StartRunHandler start,
            ListRunsHandler list,
            GetRunHandler get,
            CancelRunHandler cancel,
            ListRunPostingsHandler postings,
            ExportRunPostingsHandler export,
            FeatureFlags flags
        )
        {
            ArgumentNullException.ThrowIfNull(start);
            ArgumentNullException.ThrowIfNull(list);
            ArgumentNullException.ThrowIfNull(get);
            ArgumentNullException.ThrowIfNull(cancel);
            ArgumentNullException.ThrowIfNull(postings);
            ArgumentNullException.ThrowIfNull(export);
            ArgumentNullException.ThrowIfNull(flags);
            _start = start;
            _list = list;
            _get = get;
            _cancel = cancel;
            _postings = postings;
            _export = export;
            _flags = flags;
        }

        /// <summary>
        /// Lists the most recent runs, newest first.
        /// </summary>
        /// <param name="offset">How many newest runs to skip.</param>
        /// <param name="limit">How many runs to return.</param>
        /// <param name="cancellationToken">Aborted when the client disconnects.</param>
        /// <returns>200 with the runs.</returns>
        [HttpGet]
        [Authorize(Policy = AuthPolicies.RunsRead)]
        public async Task<ActionResult<RunListResponse>> ListAsync(
            [FromQuery] int offset = 0,
            [FromQuery] int limit = ListRunsHandler.Limit,
            CancellationToken cancellationToken = default
        )
        {
            return Ok(await _list.HandleAsync(offset, limit, cancellationToken));
        }

        /// <summary>
        /// Gets one run; poll it to follow progress.
        /// </summary>
        /// <param name="id">The run identifier.</param>
        /// <param name="cancellationToken">Aborted when the client disconnects.</param>
        /// <returns>200 with the run; 404 <c>RUN_NOT_FOUND</c>.</returns>
        [HttpGet("{id:guid}")]
        [Authorize(Policy = AuthPolicies.RunsRead)]
        public async Task<IResult> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            var result = await _get.HandleAsync(id, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
        }

        /// <summary>
        /// Starts a run in the background.
        /// </summary>
        /// <param name="request">The profile, session, and enrichment choice.</param>
        /// <param name="cancellationToken">Aborted when the client disconnects; the run itself continues.</param>
        /// <returns>202 with the recorded run and its <c>Location</c>; or the gate's problem.</returns>
        [HttpPost]
        [Authorize(Policy = AuthPolicies.RunsWrite)]
        public async Task<IResult> StartAsync(
            StartRunRequest request,
            CancellationToken cancellationToken
        )
        {
            var result = await _start.HandleAsync(request, cancellationToken);
            return result.IsSuccess
                ? Results.Accepted($"/api/v1/runs/{result.Value.Id}", result.Value)
                : result.Error.ToProblem();
        }

        /// <summary>
        /// Cancels a run. Idempotent: a finished run is returned unchanged.
        /// </summary>
        /// <param name="id">The run identifier.</param>
        /// <param name="cancellationToken">Aborted when the client disconnects.</param>
        /// <returns>200 with the run as it stands; 404 <c>RUN_NOT_FOUND</c>.</returns>
        [HttpPost("{id:guid}/cancel")]
        [Authorize(Policy = AuthPolicies.RunsWrite)]
        public async Task<IResult> CancelAsync(Guid id, CancellationToken cancellationToken)
        {
            var result = await _cancel.HandleAsync(id, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
        }

        /// <summary>
        /// Lists the postings a run saw.
        /// </summary>
        /// <param name="id">The run identifier.</param>
        /// <param name="offset">How many postings to skip.</param>
        /// <param name="limit">How many postings to return.</param>
        /// <param name="cancellationToken">Aborted when the client disconnects.</param>
        /// <returns>200 with the postings; 404 <c>RUN_NOT_FOUND</c>.</returns>
        [HttpGet("{id:guid}/postings")]
        [Authorize(Policy = AuthPolicies.RunsRead)]
        public async Task<IResult> PostingsAsync(
            Guid id,
            [FromQuery] int offset = 0,
            [FromQuery] int limit = ListRunPostingsHandler.Limit,
            CancellationToken cancellationToken = default
        )
        {
            var result = await _postings.HandleAsync(id, offset, limit, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
        }

        /// <summary>
        /// Exports the postings a run saw as a file.
        /// </summary>
        /// <param name="id">The run identifier.</param>
        /// <param name="format"><c>csv</c> or <c>xlsx</c>; defaults to <c>csv</c>, or <c>xlsx</c> when the CsvExport feature is off.</param>
        /// <param name="group"><c>all</c>, <c>only</c>, or <c>exclude</c>; defaults to <c>all</c>.</param>
        /// <param name="cancellationToken">Aborted when the client disconnects.</param>
        /// <returns>
        /// 200 with the file; 400 <c>EXPORT_FORMAT_UNSUPPORTED</c>,
        /// <c>EXPORT_GROUP_UNSUPPORTED</c>, or <c>EXPORT_COMPANIES_MISSING</c>;
        /// 404 <c>RUN_NOT_FOUND</c> or <c>FEATURE_DISABLED</c> when the chosen
        /// format is turned off.
        /// </returns>
        [HttpGet("{id:guid}/postings/export")]
        [Authorize(Policy = AuthPolicies.RunsRead)]
        public async Task<IResult> ExportAsync(
            Guid id,
            [FromQuery] string? format = null,
            [FromQuery] string group = "all",
            CancellationToken cancellationToken = default
        )
        {
            // No format resolves from the flags via the shared default.
            if (format is null)
            {
                format = ExportFormatSupport.Default(_flags);
                if (format is null)
                {
                    return Errors.FeatureDisabled().ToProblem();
                }
            }

            if (
                !Enum.TryParse<ExportFormat>(format, ignoreCase: true, out var parsed)
                || !Enum.IsDefined(parsed)
            )
            {
                return Errors
                    .ExportFormatUnsupported(format, ExportFormatSupport.Enabled(_flags))
                    .ToProblem();
            }

            var parsedGroup = (group ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "all" => CompanyExportGroup.All,
                "only" => CompanyExportGroup.Only,
                "exclude" => CompanyExportGroup.Exclude,
                _ => (CompanyExportGroup?)null,
            };
            if (parsedGroup is null)
            {
                return Errors.ExportGroupUnsupported(group ?? string.Empty).ToProblem();
            }

            var result = await _export.HandleAsync(
                id,
                parsed,
                parsedGroup.Value,
                cancellationToken
            );
            return result.IsSuccess
                ? Results.File(
                    result.Value.Content,
                    result.Value.ContentType,
                    result.Value.FileName
                )
                : result.Error.ToProblem();
        }
    }
}
