using System.Collections.Concurrent;
using System.Globalization;
using JobScrape.Application.Abstractions;
using JobScrape.Application.Results;
using JobScrape.Domain.Access;
using Microsoft.Extensions.Caching.Memory;

namespace JobScrape.Api.Http
{
    /// <summary>
    /// Fixed-window per-user usage quotas for REST plus web MCP. Counts
    /// live in a process dictionary keyed by user plus scope, so a restart
    /// resets counters but never the persisted definitions. Anonymous,
    /// health, and auth paths are exempt; everything else under
    /// <c>/api/v1/*</c> counts as <c>Api</c> and <c>/mcp</c> as <c>Mcp</c>.
    /// Over-limit requests end here with a 429 <c>QUOTA_EXCEEDED</c>
    /// problem and never reach the handler.
    /// </summary>
    public sealed class UsageQuotaMiddleware
    {
        /// <summary>
        /// Monotonic window generation counter, used to tag counters so a
        /// 404 refund can never land in a window that reset since the
        /// request was counted.
        /// </summary>
        private static long _nextWindowGeneration;

        /// <summary>
        /// The definitions cache entry. Internal so the admin controller
        /// evicts it the moment an update lands; otherwise a lowered limit
        /// would wait out the one-minute lifetime before enforcing.
        /// </summary>
        internal const string DefinitionsCacheKey = "usage-quota-definitions";

        private static readonly TimeSpan DefinitionsCacheLifetime = TimeSpan.FromMinutes(1);

        /// <summary>
        /// One counter per authenticated actor and scope. Entries outlive a
        /// window and reset in place: removing them while a request still
        /// holds the reference would split one window across two counters.
        /// Growth is bounded by the number of distinct actors, which the
        /// identity store bounds.
        /// </summary>
        private readonly ConcurrentDictionary<string, Counter> _counters = new(
            StringComparer.Ordinal
        );

        private readonly RequestDelegate _next;
        private readonly IMemoryCache _cache;
        private readonly TimeProvider _clock;

        /// <summary>
        /// Initializes a new instance of the <see cref="UsageQuotaMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware. Must not be null.</param>
        /// <param name="cache">The counter and definition cache. Must not be null.</param>
        /// <param name="clock">The time source. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public UsageQuotaMiddleware(RequestDelegate next, IMemoryCache cache, TimeProvider clock)
        {
            ArgumentNullException.ThrowIfNull(next);
            ArgumentNullException.ThrowIfNull(cache);
            ArgumentNullException.ThrowIfNull(clock);
            _next = next;
            _cache = cache;
            _clock = clock;
        }

        /// <summary>
        /// Counts one request against the caller's scope quota, or rejects
        /// the request when the window is exhausted.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <returns>A task that completes when the request is counted or rejected.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            var scope = ScopeFor(
                context.Request.Method,
                context.Request.Path.Value ?? string.Empty
            );
            if (scope is null || context.User?.Identity?.IsAuthenticated != true)
            {
                await _next(context);
                return;
            }

            var now = _clock.GetUtcNow();
            var definitions = await DefinitionsAsync(context, now);
            var definition = definitions.FirstOrDefault(candidate => candidate.Scope == scope);
            if (definition is null)
            {
                await _next(context);
                return;
            }

            var actor = context.RequestServices.GetRequiredService<ICallerContext>().ActorId;
            var counter = CounterFor(actor, scope.Value, now);
            bool overLimit;
            TimeSpan retryAfter;
            long requestGeneration;
            lock (counter)
            {
                if (now - counter.Start >= definition.Window)
                {
                    counter.Start = now;
                    counter.Count = 0;
                    counter.Generation = Interlocked.Increment(ref _nextWindowGeneration);
                }

                counter.Count++;
                overLimit = counter.Count > definition.Limit;
                retryAfter = counter.Start + definition.Window - now;
                requestGeneration = counter.Generation;
            }

            if (overLimit)
            {
                context.Response.Headers.RetryAfter = Math.Max(
                        1,
                        (int)Math.Ceiling(retryAfter.TotalSeconds)
                    )
                    .ToString(CultureInfo.InvariantCulture);
                await Errors
                    .UsageQuotaExceeded(scope.Value, definition.Limit, definition.Window)
                    .ToProblem()
                    .ExecuteAsync(context);
                return;
            }

            await _next(context);

            if (context.Response.StatusCode == StatusCodes.Status404NotFound)
            {
                lock (counter)
                {
                    if (counter.Generation == requestGeneration && counter.Count > 0)
                    {
                        counter.Count--;
                    }
                }
            }
        }

        /// <summary>
        /// Maps a request path to its quota scope, or null when exempt.
        /// </summary>
        /// <param name="method">The request method.</param>
        /// <param name="path">The request path.</param>
        /// <returns>The scope, or null for exempt and ungoverned paths.</returns>
        private static QuotaScope? ScopeFor(string method, string path)
        {
            if (
                path.Equals("/mcp", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/mcp/", StringComparison.OrdinalIgnoreCase)
            )
            {
                return QuotaScope.Mcp;
            }

            const string prefix = "/api/v1/";
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var rest = path[prefix.Length..];
            if (
                rest.Equals("auth", StringComparison.OrdinalIgnoreCase)
                || rest.StartsWith("auth/", StringComparison.OrdinalIgnoreCase)
                || rest.Equals("health", StringComparison.OrdinalIgnoreCase)
            )
            {
                return null;
            }

            // Quota-definition reads are exempt from consumption so admin
            // reads never self-429. Writes still count.
            if (
                (
                    method.Equals("GET", StringComparison.OrdinalIgnoreCase)
                    || method.Equals("HEAD", StringComparison.OrdinalIgnoreCase)
                )
                && (
                    rest.Equals("quotas", StringComparison.OrdinalIgnoreCase)
                    || rest.StartsWith("quotas/", StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                return null;
            }

            return QuotaScope.Api;
        }

        /// <summary>
        /// Loads the definitions, cached for at most one minute, seeding
        /// the starting budgets when the table is empty.
        /// </summary>
        /// <param name="context">The HTTP context carrying the scoped services.</param>
        /// <param name="now">The current time.</param>
        /// <returns>The live definitions.</returns>
        private async Task<IReadOnlyList<Definition>> DefinitionsAsync(
            HttpContext context,
            DateTimeOffset now
        )
        {
            if (
                _cache.TryGetValue(DefinitionsCacheKey, out IReadOnlyList<Definition>? cached)
                && cached is not null
            )
            {
                return cached;
            }

            var quotas = context.RequestServices.GetRequiredService<IUsageQuotaRepository>();
            var stored = await quotas.ListAsync(context.RequestAborted);
            if (stored.Count == 0)
            {
                stored = await quotas.EnsureSeededAsync(context.RequestAborted);
            }

            var definitions = stored
                .Select(quota => new Definition(quota.Scope, quota.Limit, quota.Window))
                .ToList();
            _cache.Set(DefinitionsCacheKey, definitions, now + DefinitionsCacheLifetime);
            return definitions;
        }

        /// <summary>
        /// Gets the caller's counter, creating it on first use. GetOrAdd is
        /// atomic: every concurrent caller receives the same instance even
        /// when the factory runs more than once, so no increment is orphaned.
        /// </summary>
        /// <param name="actor">The caller identity.</param>
        /// <param name="scope">The governed surface.</param>
        /// <param name="now">The current time.</param>
        /// <returns>The mutable counter.</returns>
        private Counter CounterFor(string actor, QuotaScope scope, DateTimeOffset now)
        {
            return _counters.GetOrAdd(
                CounterKey(actor, scope),
                _ => new Counter
                {
                    Start = now,
                    Generation = Interlocked.Increment(ref _nextWindowGeneration),
                }
            );
        }

        /// <summary>
        /// Builds the counter cache key for one user plus scope.
        /// </summary>
        /// <param name="actor">The caller identity.</param>
        /// <param name="scope">The governed surface.</param>
        /// <returns>The key.</returns>
        private static string CounterKey(string actor, QuotaScope scope)
        {
            return $"usage-quota:{scope}:{actor}";
        }

        /// <summary>
        /// One live definition snapshot. A record, not the tracked
        /// aggregate, so the one-minute cache never holds EF Core state.
        /// </summary>
        /// <param name="Scope">The governed surface.</param>
        /// <param name="Limit">Requests one user may make per window.</param>
        /// <param name="Window">The fixed window.</param>
        private sealed record Definition(QuotaScope Scope, int Limit, TimeSpan Window);

        /// <summary>
        /// The mutable fixed-window counter. Guarded by the instance lock at
        /// each use; instances are never shared across keys.
        /// </summary>
        private sealed class Counter
        {
            /// <summary>
            /// When the current window started.
            /// </summary>
            public DateTimeOffset Start;

            /// <summary>
            /// Requests counted in the current window.
            /// </summary>
            public int Count;

            /// <summary>
            /// The window generation, incremented on every reset, so refunds
            /// can be scoped to the window that counted the request.
            /// </summary>
            public long Generation;
        }
    }
}
