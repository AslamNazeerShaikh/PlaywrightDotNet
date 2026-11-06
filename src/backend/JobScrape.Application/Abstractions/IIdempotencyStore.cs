namespace JobScrape.Application.Abstractions
{
    /// <summary>
    /// Replay store for the idempotency edge. One record per
    /// (owner, endpoint, key): a repeat within 24 hours replays the stored
    /// status and body instead of re-executing the operation.
    /// </summary>
    /// <param name="OwnerKey">The authenticated user id, or empty when anonymous.</param>
    /// <param name="Endpoint">The method, path, and query the key scopes to.</param>
    /// <param name="Key">The client-supplied idempotency key.</param>
    /// <param name="RequestHash">The SHA256 hex of the request body the key was first used with.</param>
    /// <param name="StatusCode">The stored HTTP status code.</param>
    /// <param name="ContentType">The stored response content type.</param>
    /// <param name="Body">The stored response body bytes.</param>
    /// <param name="CreatedAt">When the response was stored.</param>
    public sealed record IdempotencyEntry(
        string OwnerKey,
        string Endpoint,
        string Key,
        string RequestHash,
        int StatusCode,
        string ContentType,
        byte[] Body,
        DateTimeOffset CreatedAt
    );

    /// <summary>
    /// Persistence port for idempotency records.
    /// </summary>
    public interface IIdempotencyStore
    {
        /// <summary>
        /// Loads the record for the owner, endpoint, and key, or null.
        /// </summary>
        /// <param name="ownerKey">The authenticated user id, or empty.</param>
        /// <param name="endpoint">The concrete request path.</param>
        /// <param name="key">The client-supplied idempotency key.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The entry, or null when absent.</returns>
        Task<IdempotencyEntry?> GetAsync(
            string ownerKey,
            string endpoint,
            string key,
            CancellationToken cancellationToken
        );

        /// <summary>
        /// Stores the response, replacing any record for the same triple.
        /// Expired rows are pruned alongside the write.
        /// </summary>
        /// <param name="entry">The entry to store.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>A task that completes when the entry is stored.</returns>
        Task SaveAsync(IdempotencyEntry entry, CancellationToken cancellationToken);
    }
}
