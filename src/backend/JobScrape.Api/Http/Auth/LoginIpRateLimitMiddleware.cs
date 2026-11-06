using System.Threading.RateLimiting;
using JobScrape.Application.Results;

namespace JobScrape.Api.Http.Auth
{
    /// <summary>
    /// Applies an IP-scoped fixed-window limiter to the login and
    /// setup-password endpoints so rotating account emails cannot bypass
    /// the auth ceiling. Runs alongside the per-account limiter registered
    /// as a policy. Rejection shape matches the centralized
    /// <see cref="AuthRegistration"/> limiter rejection.
    /// </summary>
    public sealed class LoginIpRateLimitMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly PartitionedRateLimiter<HttpContext> _limiter;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="next">The next delegate.</param>
        public LoginIpRateLimitMiddleware(RequestDelegate next)
        {
            _next = next;
            _limiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = AuthRegistration.LoginPermitLimit,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    }
                )
            );
        }

        /// <summary>
        /// Invokes the middleware.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <returns>A task.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            var path = (context.Request.Path.Value ?? string.Empty).TrimEnd('/');
            var isAuthSurface =
                HttpMethods.IsPost(context.Request.Method)
                && (
                    string.Equals(path, "/api/v1/auth/login", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(path, "/api/v1/auth/setup-password", StringComparison.OrdinalIgnoreCase)
                );

            if (isAuthSurface)
            {
                using var lease = _limiter.AttemptAcquire(context);
                if (!lease.IsAcquired)
                {
                    await AuthRegistration.WriteRateLimitedProblemAsync(context);
                    return;
                }
            }

            await _next(context);
        }
    }
}
