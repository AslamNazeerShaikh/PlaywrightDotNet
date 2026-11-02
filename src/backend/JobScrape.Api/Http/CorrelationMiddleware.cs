using Serilog.Context;

namespace JobScrape.Api.Http
{
    /// <summary>
    /// Gives every request one correlation id, echoed in the
    /// <c>X-Correlation-Id</c> response header and pushed into the log
    /// context with the request id (docs/15-runtime-hardening.md §2.5). A
    /// client-supplied id is reused only when it is short and made of safe
    /// characters, so a header can never inject text into the logs.
    /// </summary>
    public sealed class CorrelationMiddleware
    {
        /// <summary>
        /// The request and response header.
        /// </summary>
        public const string HeaderName = "X-Correlation-Id";

        /// <summary>
        /// Longest client-supplied id that is reused.
        /// </summary>
        public const int MaxLength = 64;

        // Injected and never mutated.
        private readonly RequestDelegate _next;

        /// <summary>
        /// Initializes a new instance of the <see cref="CorrelationMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="next"/> is null.</exception>
        public CorrelationMiddleware(RequestDelegate next)
        {
            ArgumentNullException.ThrowIfNull(next);
            _next = next;
        }

        /// <summary>
        /// Assigns the id, then runs the rest of the pipeline inside its log scope.
        /// </summary>
        /// <param name="context">The request context.</param>
        /// <returns>A task that completes with the pipeline.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            var supplied = context.Request.Headers[HeaderName].FirstOrDefault();
            var correlationId = IsSafe(supplied)
                ? supplied!
                : System.Diagnostics.Activity.Current?.TraceId.ToString()
                    ?? Guid.NewGuid().ToString("N");

            context.Response.Headers[HeaderName] = correlationId;
            using (LogContext.PushProperty("CorrelationId", correlationId))
            using (LogContext.PushProperty("RequestId", context.TraceIdentifier))
            {
                await _next(context);
            }
        }

        /// <summary>
        /// Whether a supplied id is short and only letters, digits, <c>-</c>, or <c>_</c>.
        /// </summary>
        /// <param name="value">The supplied id.</param>
        /// <returns>True when safe to reuse.</returns>
        private static bool IsSafe(string? value)
        {
            return value is { Length: > 0 and <= MaxLength }
                && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
        }
    }
}
