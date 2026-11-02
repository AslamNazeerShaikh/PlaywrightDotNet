using JobScrape.Domain.Common;

namespace JobScrape.Domain.Sourcing
{
    /// <summary>
    /// A tracking-free, absolute <c>https</c> job URL on the source's own
    /// host (docs/08-site-playbooks.md §2.1). Canonicalization keeps only the
    /// query parameters that identify the job on that source (Indeed
    /// <c>jk</c>) and drops everything else (<c>utm_*</c>, <c>refId</c>,
    /// <c>trackingId</c>, <c>from</c>, ...), the fragment, and a trailing slash.
    /// </summary>
    public sealed record CanonicalUrl
    {
        /// <summary>
        /// Maximum accepted URL length, before canonicalization.
        /// </summary>
        public const int MaxLength = 2048;

        /// <summary>
        /// Initializes a new instance with an already-canonical value.
        /// </summary>
        /// <param name="value">The canonical URL.</param>
        private CanonicalUrl(string value)
        {
            Value = value;
        }

        /// <summary>
        /// Gets the canonical URL string.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Validates and canonicalizes a job URL for a source.
        /// </summary>
        /// <param name="value">The raw absolute URL.</param>
        /// <param name="source">The source whose host the URL must be on. Must not be null.</param>
        /// <returns>The canonical URL.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
        /// <exception cref="DomainException">
        /// Thrown when the URL is blank, too long, relative, not <c>https</c>,
        /// carries credentials or a non-default port, or is on another host.
        /// </exception>
        public static CanonicalUrl Create(string? value, SourceKey source)
        {
            ArgumentNullException.ThrowIfNull(source);

            var trimmed = value?.Trim() ?? string.Empty;
            if (trimmed.Length is 0 or > MaxLength)
            {
                throw new DomainException($"Job URL must be 1..{MaxLength} characters.");
            }

            // https-only also rejects javascript:, data:, and file: URLs.
            if (
                !Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps
            )
            {
                throw new DomainException("Job URL must be an absolute https URL.");
            }

            if (uri.UserInfo.Length > 0 || !uri.IsDefaultPort)
            {
                throw new DomainException("Job URL cannot carry credentials or a custom port.");
            }

            var allowedHost = AllowedHost(source);
            if (!string.Equals(uri.IdnHost, allowedHost, StringComparison.OrdinalIgnoreCase))
            {
                throw new DomainException(
                    $"Job URL host must be '{allowedHost}' for source '{source}'."
                );
            }

            var path =
                uri.AbsolutePath.Length > 1 ? uri.AbsolutePath.TrimEnd('/') : uri.AbsolutePath;
            var query = string.Join(
                '&',
                uri.Query.TrimStart('?')
                    .Split('&', StringSplitOptions.RemoveEmptyEntries)
                    .Where(pair => IsSignificant(source, pair.Split('=', 2)[0]))
            );

            return new CanonicalUrl(
                query.Length == 0
                    ? $"https://{allowedHost}{path}"
                    : $"https://{allowedHost}{path}?{query}"
            );
        }

        /// <summary>
        /// Rehydrates a stored canonical URL. Each allowlisted host belongs to
        /// exactly one source, so the source is implied by the host and the
        /// full validation of <see cref="Create"/> still applies.
        /// </summary>
        /// <param name="value">A canonical URL on an allowlisted host.</param>
        /// <returns>The canonical URL.</returns>
        /// <exception cref="DomainException">Thrown when the URL is invalid or on no source's host.</exception>
        public static CanonicalUrl Parse(string? value)
        {
            var host = Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri)
                ? uri.IdnHost
                : null;
            var source = new[]
            {
                SourceKey.Naukri,
                SourceKey.LinkedIn,
                SourceKey.Indeed,
            }.FirstOrDefault(candidate =>
                string.Equals(host, AllowedHost(candidate), StringComparison.OrdinalIgnoreCase)
            );
            return source is null
                ? throw new DomainException("Stored job URL is not on any source's host.")
                : Create(value, source);
        }

        /// <summary>
        /// Returns the canonical URL string.
        /// </summary>
        /// <returns>The value of <see cref="Value"/>.</returns>
        public override string ToString()
        {
            return Value;
        }

        /// <summary>
        /// The only host a source's job URLs may use.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <returns>The lowercase host.</returns>
        private static string AllowedHost(SourceKey source)
        {
            // SourceKey has exactly three instances, so the discard arm is
            // Indeed; a fourth source must add its host here.
            return source.Value switch
            {
                "naukri" => "www.naukri.com",
                "linkedin" => "www.linkedin.com",
                _ => "in.indeed.com",
            };
        }

        /// <summary>
        /// Whether a query parameter identifies the job on the source and so
        /// survives canonicalization.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="name">The raw parameter name.</param>
        /// <returns>True for source-significant parameters.</returns>
        private static bool IsSignificant(SourceKey source, string name)
        {
            return source == SourceKey.Indeed && name == "jk";
        }
    }
}
