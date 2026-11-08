using System.Diagnostics;
using JobScrape.Application.Results;

namespace JobScrape.Api.Http
{
    /// <summary>
    /// The single owner of <see cref="ErrorInfo"/> to RFC 9457 Problem Details
    /// translation. Endpoints never hand-build problem objects; they call
    /// this mapper so status codes, type URIs, and extension members stay
    /// consistent. Follows docs/14-application-patterns.md §6.3; the type-URI
    /// host is <c>jobscrape.local</c> (the doc predates the JobLens to
    /// JobScrape rename and still shows the old host).
    /// </summary>
    public static class ErrorMapper
    {
        /// <summary>
        /// Translates a typed failure into an <see cref="IResult"/> problem
        /// response with content type <c>application/problem+json</c>.
        /// </summary>
        /// <param name="error">The typed failure. Must not be null.</param>
        /// <returns>An <see cref="IResult"/> rendering the problem.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="error"/> is null.</exception>
        public static IResult ToProblem(this ErrorInfo error)
        {
            ArgumentNullException.ThrowIfNull(error);

            var status = error.Kind switch
            {
                ErrorKind.Validation => StatusCodes.Status400BadRequest,
                ErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
                ErrorKind.NotFound => StatusCodes.Status404NotFound,
                ErrorKind.Conflict => StatusCodes.Status409Conflict,
                ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
                ErrorKind.RateLimited => StatusCodes.Status429TooManyRequests,
                ErrorKind.QuotaExceeded => StatusCodes.Status429TooManyRequests,
                ErrorKind.Challenge => StatusCodes.Status409Conflict,
                ErrorKind.CoolingDown => StatusCodes.Status423Locked,
                ErrorKind.SourceChanged => StatusCodes.Status502BadGateway,
                ErrorKind.Unavailable => StatusCodes.Status503ServiceUnavailable,
                ErrorKind.Storage => StatusCodes.Status507InsufficientStorage,
                _ => StatusCodes.Status500InternalServerError,
            };

            return Results.Problem(
                type: $"https://jobscrape.local/problems/{error.Code.ToLowerInvariant().Replace('_', '-')}",
                title: error.Title,
                detail: error.Detail,
                statusCode: status,
                extensions: Extensions(error)
            );
        }

        /// <summary>
        /// The JobScrape extension members: the stable <c>code</c>, the
        /// <c>traceId</c>, and for validation the field-level <c>errors</c>.
        /// Errors travel as an extension rather than through
        /// <c>Results.ValidationProblem</c>: MVC's problem writer rebuilds
        /// the body from the standard members plus extensions, so the
        /// validation-specific <c>Errors</c> property would be dropped on the
        /// wire.
        /// </summary>
        /// <param name="error">The typed failure.</param>
        /// <returns>The extension members.</returns>
        private static Dictionary<string, object?> Extensions(ErrorInfo error)
        {
            var extensions = new Dictionary<string, object?>
            {
                ["code"] = error.Code,
                ["traceId"] = Activity.Current?.Id ?? "unknown",
            };
            if (error.Kind == ErrorKind.Validation)
            {
                extensions["errors"] =
                    error.Metadata?.TryGetValue("errors", out var raw) == true
                    && raw is IReadOnlyDictionary<string, string[]> typed
                        ? typed
                        : new Dictionary<string, string[]>();
            }

            return extensions;
        }
    }
}
