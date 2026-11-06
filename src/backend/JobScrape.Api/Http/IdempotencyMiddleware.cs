using System.Collections.Concurrent;
using System.Security.Cryptography;
using JobScrape.Application.Abstractions;
using JobScrape.Application.Results;

namespace JobScrape.Api.Http
{
    /// <summary>
    /// Replays mutating requests carrying an <c>Idempotency-Key</c> header.
    /// The first execution stores the status code, content type, and body
    /// for 24 hours; a repeat with the same authenticated user, scoped key
    /// (method, path, and query), and identical request body replays the
    /// stored response instead of re-executing. A repeat with a different
    /// body is a 409 <c>IDEMPOTENCY_KEY_REUSED</c>. Anonymous requests and
    /// anything under <c>/api/v1/auth</c> never engage: login and setup
    /// tokens must never replay. Only POST, PUT, PATCH, and DELETE engage.
    /// Error responses at or above 500 and auth or throttle outcomes are
    /// never stored.
    /// </summary>
    public sealed class IdempotencyMiddleware
    {
        /// <summary>
        /// The request header carrying the client-supplied key.
        /// </summary>
        public const string HeaderName = "Idempotency-Key";

        /// <summary>
        /// Longest stored response body, in bytes.
        /// </summary>
        internal const int MaxBodyBytes = 1_048_576;

        /// <summary>
        /// Longest honored key.
        /// </summary>
        internal const int MaxKeyLength = 128;

        /// <summary>
        /// How long a loser waits for the winner's row to complete.
        /// </summary>
        internal static readonly TimeSpan CompletionWaitTimeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Per-key locks so parallel duplicate requests serialize and only
        /// the first executes.
        /// </summary>
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(
            StringComparer.Ordinal
        );

        // Injected and never mutated.
        private readonly RequestDelegate _next;

        /// <summary>
        /// Initializes a new instance of the <see cref="IdempotencyMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="next"/> is null.</exception>
        public IdempotencyMiddleware(RequestDelegate next)
        {
            ArgumentNullException.ThrowIfNull(next);
            _next = next;
        }

        /// <summary>
        /// Replays a stored response or executes and stores the result.
        /// </summary>
        /// <param name="context">The request context.</param>
        /// <param name="store">The idempotency port.</param>
        /// <param name="clock">The time source.</param>
        /// <returns>A task that completes with the pipeline.</returns>
        public async Task InvokeAsync(
            HttpContext context,
            IIdempotencyStore store,
            TimeProvider clock
        )
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(store);
            ArgumentNullException.ThrowIfNull(clock);

            var key = context.Request.Headers[HeaderName].FirstOrDefault()?.Trim();
            var isAuthenticated = context.User.Identity?.IsAuthenticated == true;
            if (
                !isAuthenticated
                || IsAuthPath(context.Request.Path)
                || !IsReplayable(context.Request.Method)
            )
            {
                await _next(context);
                return;
            }

            if (string.IsNullOrEmpty(key))
            {
                await _next(context);
                return;
            }

            if (key.Length > MaxKeyLength)
            {
                await Errors
                    .ValidationFailed(
                        "Idempotency key exceeds the 128 character limit.",
                        new Dictionary<string, string[]>
                        {
                            [HeaderName] = ["Idempotency key must be 128 characters or fewer."],
                        }
                    )
                    .ToProblem()
                    .ExecuteAsync(context);
                return;
            }

            var owner = context.User.FindFirst(IdentityClaimTypes.Subject)?.Value ?? string.Empty;
            var endpoint = Scope(context.Request);
            var lockKey = string.Concat(owner, "\u001f", endpoint, "\u001f", key);
            var semaphore = _locks.GetOrAdd(lockKey, static _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync(context.RequestAborted);
            try
            {
                context.Request.EnableBuffering();
                var requestHash = await HashBodyAsync(context.Request);
                var pendingEntry = new IdempotencyEntry(
                    owner,
                    endpoint,
                    key,
                    requestHash,
                    0,
                    "application/json",
                    [],
                    clock.GetUtcNow()
                );
                var won = await store.TryReserveAsync(pendingEntry, context.RequestAborted);
                if (!won)
                {
                    var stored = await WaitForCompletionAsync(
                        store,
                        owner,
                        endpoint,
                        key,
                        clock,
                        context.RequestAborted
                    );
                    if (stored is null)
                    {
                        await Errors.RunNotStarted().ToProblem().ExecuteAsync(context);
                        return;
                    }

                    if (!stored.RequestHash.Equals(requestHash, StringComparison.Ordinal))
                    {
                        await Errors.IdempotencyKeyReused().ToProblem().ExecuteAsync(context);
                        return;
                    }

                    context.Response.StatusCode = stored.StatusCode;
                    context.Response.ContentType = stored.ContentType;
                    await context.Response.Body.WriteAsync(stored.Body, context.RequestAborted);
                    return;
                }

                var original = context.Response.Body;
                await using var buffer = new MemoryStream();
                context.Response.Body = buffer;
                try
                {
                    await _next(context);
                }
                catch
                {
                    context.Response.Body = original;
                    await store.RemoveAsync(owner, endpoint, key, context.RequestAborted);
                    throw;
                }

                context.Response.Body = original;

                buffer.Seek(0, SeekOrigin.Begin);
                if (buffer.Length <= MaxBodyBytes && IsStorable(context.Response.StatusCode))
                {
                    try
                    {
                        await store.SaveAsync(
                            new IdempotencyEntry(
                                owner,
                                endpoint,
                                key,
                                requestHash,
                                context.Response.StatusCode,
                                context.Response.ContentType ?? "application/json",
                                buffer.ToArray(),
                                clock.GetUtcNow()
                            ),
                            context.RequestAborted
                        );
                    }
                    catch
                    {
                        await store.RemoveAsync(owner, endpoint, key, context.RequestAborted);
                        throw;
                    }
                }
                else
                {
                    await store.RemoveAsync(owner, endpoint, key, context.RequestAborted);
                }

                buffer.Seek(0, SeekOrigin.Begin);
                await buffer.CopyToAsync(original, context.RequestAborted);
            }
            finally
            {
                semaphore.Release();
                if (semaphore.CurrentCount == 1)
                {
                    _locks.TryRemove(new KeyValuePair<string, SemaphoreSlim>(lockKey, semaphore));
                }
            }
        }

        /// <summary>
        /// Waits for the winner's row to leave the pending state, bounded by
        /// <see cref="CompletionWaitTimeout"/>.
        /// </summary>
        private static async Task<IdempotencyEntry?> WaitForCompletionAsync(
            IIdempotencyStore store,
            string owner,
            string endpoint,
            string key,
            TimeProvider clock,
            CancellationToken cancellationToken
        )
        {
            var deadline = clock.GetUtcNow() + CompletionWaitTimeout;
            while (clock.GetUtcNow() < deadline)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return null;
                }

                var entry = await store.GetAsync(owner, endpoint, key, cancellationToken);
                if (entry is not null && entry.StatusCode != 0)
                {
                    return entry;
                }

                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
            }

            return null;
        }

        /// <summary>
        /// Whether the request targets the auth surface, which never engages
        /// idempotency.
        /// </summary>
        /// <param name="path">The request path.</param>
        /// <returns>True for paths under <c>/api/v1/auth</c>.</returns>
        private static bool IsAuthPath(PathString path)
        {
            return path.Value?.StartsWith("/api/v1/auth", StringComparison.OrdinalIgnoreCase)
                == true;
        }

        /// <summary>
        /// Scopes the key to the method, path, and query so the same key on
        /// a different endpoint never replays another response.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The scope string.</returns>
        private static string Scope(HttpRequest request)
        {
            return string.Concat(
                request.Method,
                " ",
                request.Path.Value ?? string.Empty,
                request.QueryString.Value ?? string.Empty
            );
        }

        /// <summary>
        /// Hashes the request body with SHA256, leaving the body readable
        /// for model binding.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The lowercase hex hash.</returns>
        private static async Task<string> HashBodyAsync(HttpRequest request)
        {
            request.Body.Position = 0;
            await using var copy = new MemoryStream();
            await request.Body.CopyToAsync(copy, request.HttpContext.RequestAborted);
            request.Body.Position = 0;
            return Convert.ToHexStringLower(SHA256.HashData(copy.ToArray()));
        }

        /// <summary>
        /// Whether the method engages idempotency.
        /// </summary>
        /// <param name="method">The request method.</param>
        /// <returns>True for POST, PUT, PATCH, or DELETE.</returns>
        private static bool IsReplayable(string method)
        {
            return HttpMethods.IsPost(method)
                || HttpMethods.IsPut(method)
                || HttpMethods.IsPatch(method)
                || HttpMethods.IsDelete(method);
        }

        /// <summary>
        /// Whether the status is safe to replay: success or a deterministic
        /// client outcome, never a server, auth, or throttle failure.
        /// </summary>
        /// <param name="statusCode">The response status code.</param>
        /// <returns>True when the response may be stored.</returns>
        private static bool IsStorable(int statusCode)
        {
            return (
                    statusCode >= StatusCodes.Status200OK
                    && statusCode < StatusCodes.Status300MultipleChoices
                )
                || statusCode
                    is StatusCodes.Status400BadRequest
                        or StatusCodes.Status404NotFound
                        or StatusCodes.Status409Conflict
                        or StatusCodes.Status422UnprocessableEntity
                        or StatusCodes.Status423Locked;
        }
    }
}
