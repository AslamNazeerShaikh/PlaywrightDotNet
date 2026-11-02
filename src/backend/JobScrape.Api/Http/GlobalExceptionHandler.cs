using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;

namespace JobScrape.Api.Http
{
    /// <summary>
    /// Last-resort handler for exceptions no endpoint mapped
    /// (docs/15-runtime-hardening.md §3.2). Logs once with full detail and
    /// returns a safe 500 Problem Details carrying only the stable code and
    /// the trace id; the exception message never reaches the client.
    /// Cancellation is not a failure and is left to abort the request.
    /// </summary>
    public sealed partial class GlobalExceptionHandler : IExceptionHandler
    {
        // Injected and never mutated.
        private readonly ILogger<GlobalExceptionHandler> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="GlobalExceptionHandler"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="logger"/> is null.</exception>
        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
        }

        /// <summary>
        /// Logs and answers an unhandled exception.
        /// </summary>
        /// <param name="httpContext">The failed request.</param>
        /// <param name="exception">The unhandled exception.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>False for cancellation (not handled); otherwise true after writing the 500.</returns>
        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken
        )
        {
            ArgumentNullException.ThrowIfNull(httpContext);
            ArgumentNullException.ThrowIfNull(exception);
            if (exception is OperationCanceledException)
            {
                return false;
            }

            var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
            LogUnhandled(
                _logger,
                exception,
                httpContext.Request.Method,
                httpContext.Request.Path,
                traceId
            );

            await Results
                .Problem(
                    type: "https://jobscrape.local/problems/internal-error",
                    title: "Internal server error",
                    detail: "An unexpected error occurred. Use the trace ID when reporting it.",
                    statusCode: StatusCodes.Status500InternalServerError,
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "INTERNAL_ERROR",
                        ["traceId"] = traceId,
                    }
                )
                .ExecuteAsync(httpContext);
            return true;
        }

        [LoggerMessage(
            Level = LogLevel.Error,
            Message = "Unhandled exception for {Method} {Path}. TraceId={TraceId}"
        )]
        private static partial void LogUnhandled(
            ILogger logger,
            Exception exception,
            string method,
            PathString path,
            string traceId
        );
    }
}
