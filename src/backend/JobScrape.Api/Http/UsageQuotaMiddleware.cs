using System.Globalization;
using JobScrape.Application.Abstractions;
using JobScrape.Application.Results;
using JobScrape.Domain.Access;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace JobScrape.Api.Http
{
    /// <summary>
    /// Fixed-window per-user usage quotas for REST plus web MCP. Counts
    /// live in memory, keyed by user plus scope plus window, so a restart
    /// resets counters but never the persisted definitions. Anonymous,
    /// health, and auth paths are exempt; everything else under
    /// <c>/api/v1/*</c> counts as <c>Api</c> and <c>/mcp</c> as <c>Mcp</c>.
    /// Over-limit requests end here with a 429 <c>QUOTA_EXCEEDED</c>
    /// problem and never reach the handler.
    /// </summary>
    public sealed class UsageQuotaMiddleware
    {
        /// <summary>
        /// The definitions cache entry. Internal so the admin controller
        /// evicts it the moment an update lands; otherwise a lowered limit
        /// would wait out the one-minute lifetime before enforcing.
        /// </summary>
        internal const string DefinitionsCacheKey = "usage-quota-definitions";

        private static readonly TimeSpan DefinitionsCacheLifetime = TimeSpan.FromMinutes(1);

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
            var scope = ScopeFor(context.Request.Path.Value ?? string.Empty);
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
            var counter = CounterFor(actor, scope.Value, definition, now);
            bool overLimit;
            TimeSpan retryAfter;
            lock (counter)
            {
                if (now - counter.Start >= definition.Window)
                {
                    counter.Start = now;
                    counter.Count = 0;
                    _cache.Set(CounterKey(actor, scope.Value), counter, now + definition.Window);
                }

                counter.Count++;
                overLimit = counter.Count > definition.Limit;
                retryAfter = counter.Start + definition.Window - now;
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
        }

        /// <summary>
        /// Maps a request path to its quota scope, or null when exempt.
        /// </summary>
        /// <param name="path">The request path.</param>
        /// <returns>The scope, or null for exempt and ungoverned paths.</returns>
        private static QuotaScope? ScopeFor(string path)
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
                stored = await SeedAsync(context);
            }

            var definitions = stored
                .Select(quota => new Definition(quota.Scope, quota.Limit, quota.Window))
                .ToList();
            _cache.Set(DefinitionsCacheKey, definitions, now + DefinitionsCacheLifetime);
            return definitions;
        }

        /// <summary>
        /// Inserts the starting budgets when the table is empty. A lost
        /// insert race re-reads instead of failing the request.
        /// </summary>
        /// <param name="context">The HTTP context carrying the scoped services.</param>
        /// <returns>The stored definitions.</returns>
        private static async Task<IReadOnlyList<UsageQuota>> SeedAsync(HttpContext context)
        {
            var quotas = context.RequestServices.GetRequiredService<IUsageQuotaRepository>();
            var unitOfWork = context.RequestServices.GetRequiredService<IUnitOfWork>();
            foreach (var quota in UsageQuotaDefaults.All())
            {
                await quotas.AddAsync(quota, context.RequestAborted);
            }

            try
            {
                await unitOfWork.SaveChangesAsync(context.RequestAborted);
            }
            catch (DbUpdateException)
            {
                context
                    .RequestServices.GetRequiredService<JobScrape.Infrastructure.Persistence.JobScrapeDbContext>()
                    .ChangeTracker.Clear();
            }

            return await quotas.ListAsync(context.RequestAborted);
        }

        /// <summary>
        /// Gets the caller's counter for the current window, creating it on
        /// first use with an expiry at the window end.
        /// </summary>
        /// <param name="actor">The caller identity.</param>
        /// <param name="scope">The governed surface.</param>
        /// <param name="definition">The live definition.</param>
        /// <param name="now">The current time.</param>
        /// <returns>The mutable counter.</returns>
        private Counter CounterFor(
            string actor,
            QuotaScope scope,
            Definition definition,
            DateTimeOffset now
        )
        {
            return _cache.GetOrCreate(
                CounterKey(actor, scope),
                entry =>
                {
                    entry.AbsoluteExpiration = now + definition.Window;
                    return new Counter { Start = now };
                }
            )!;
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
        /// The mutable fixed-window counter. Guarded by lock at each use;
        /// instances are never shared across keys.
        /// </summary>
        private sealed class Counter
        {
            /// <summary>
            /// Gets or sets when the current window started.
            /// </summary>
            public DateTimeOffset Start;

            /// <summary>
            /// Gets or sets the requests counted in the current window.
            /// </summary>
            public int Count;
        }
    }
}
