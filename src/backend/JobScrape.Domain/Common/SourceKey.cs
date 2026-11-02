using System.Diagnostics.CodeAnalysis;

namespace JobScrape.Domain.Common
{
    /// <summary>
    /// Stable lowercase key of a supported job board. Only the three known
    /// instances exist; parsing returns those singletons so reference and
    /// value equality agree. Each key carries its <see cref="ComplianceTier"/>
    /// so every consent gate reads the tier from one place.
    /// </summary>
    public sealed record SourceKey
    {
        /// <summary>
        /// Naukri. Standard tier: enabled by default.
        /// </summary>
        public static readonly SourceKey Naukri = new("naukri", ComplianceTier.Standard);

        /// <summary>
        /// Indeed India. Restricted tier: opt-in with risk acknowledgment.
        /// </summary>
        public static readonly SourceKey Indeed = new("indeed", ComplianceTier.Restricted);

        /// <summary>
        /// LinkedIn. High-risk tier: opt-in, guest-only, interactive consent.
        /// </summary>
        public static readonly SourceKey LinkedIn = new("linkedin", ComplianceTier.HighRisk);

        /// <summary>
        /// Initializes a new instance. Private so the known keys above are
        /// the only instances.
        /// </summary>
        /// <param name="value">The lowercase source key.</param>
        /// <param name="tier">The compliance tier of the source.</param>
        private SourceKey(string value, ComplianceTier tier)
        {
            Value = value;
            Tier = tier;
        }

        /// <summary>
        /// Gets the stable lowercase key, e.g. <c>naukri</c>.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Gets the compliance tier that gates runs against this source.
        /// </summary>
        public ComplianceTier Tier { get; }

        /// <summary>
        /// Parses a source key, ignoring case and surrounding whitespace.
        /// </summary>
        /// <param name="value">The raw key, e.g. <c>" Naukri "</c>.</param>
        /// <returns>The matching known source key.</returns>
        /// <exception cref="DomainException">Thrown when the key is missing or unknown.</exception>
        public static SourceKey Parse(string? value)
        {
            return TryParse(value, out var key)
                ? key
                : throw new DomainException($"Unknown source '{value}'.");
        }

        /// <summary>
        /// Attempts to parse a source key, ignoring case and surrounding
        /// whitespace. Use at request-validation boundaries where an unknown
        /// key is an expected input error rather than a broken invariant.
        /// </summary>
        /// <param name="value">The raw key.</param>
        /// <param name="key">The matching source key, or null when unknown.</param>
        /// <returns>True when <paramref name="value"/> names a known source.</returns>
        public static bool TryParse(string? value, [NotNullWhen(true)] out SourceKey? key)
        {
            // Upper-casing for comparison only; the canonical Value stays
            // lowercase on the singleton.
            key = value?.Trim().ToUpperInvariant() switch
            {
                "NAUKRI" => Naukri,
                "INDEED" => Indeed,
                "LINKEDIN" => LinkedIn,
                _ => null,
            };
            return key is not null;
        }

        /// <summary>
        /// Returns the lowercase key so the type formats as it is stored.
        /// </summary>
        /// <returns>The value of <see cref="Value"/>.</returns>
        public override string ToString()
        {
            return Value;
        }
    }
}
