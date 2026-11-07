namespace JobScrape.Infrastructure.Persistence
{
    /// <summary>
    /// The stored idempotency row: one per (owner, endpoint, key). Plain
    /// persistence shape; the application layer owns the port contract.
    /// </summary>
    internal sealed class IdempotencyRecord
    {
        /// <summary>
        /// Gets or sets the authenticated user id, or empty when anonymous.
        /// </summary>
        public string OwnerKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the concrete request path.
        /// </summary>
        public string Endpoint { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the client-supplied idempotency key.
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the SHA256 hex of the request body the key was first
        /// used with. A reuse with a different hash is a 409, never a replay.
        /// </summary>
        public string RequestHash { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the stored HTTP status code.
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// Gets or sets the stored response content type.
        /// </summary>
        public string ContentType { get; set; } = "application/json";

        /// <summary>
        /// Gets or sets the stored response body bytes.
        /// </summary>
        public byte[] Body { get; set; } = [];

        /// <summary>
        /// Gets or sets when the response was stored.
        /// </summary>
        public DateTimeOffset CreatedAt { get; set; }
    }
}
