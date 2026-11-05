using System.Globalization;
using JobScrape.Domain.Sourcing;

namespace JobScrape.Infrastructure.Sources.LinkedIn
{
    /// <summary>
    /// Builds LinkedIn guest search URLs (docs/08-site-playbooks.md §4.3):
    /// <c>https://www.linkedin.com/jobs/search?keywords=&amp;location=&amp;f_TPR=r604800&amp;start=</c>.
    /// <c>start</c> is a zero-based result offset in steps of 25;
    /// <c>f_TPR</c> carries the posted-within-days window in seconds.
    /// Guest-only: no authenticated or <c>jobs-guest</c> API endpoint is
    /// ever built here.
    /// </summary>
    public static class LinkedInUrlBuilder
    {
        /// <summary>
        /// The only LinkedIn host the adapter navigates to.
        /// </summary>
        public const string Host = "www.linkedin.com";

        /// <summary>
        /// Results per guest search page; the <c>start</c> offset moves in steps of this size.
        /// </summary>
        public const int PageSize = 25;

        private const string Origin = "https://www.linkedin.com/";

        /// <summary>
        /// Builds the guest search URL for one page. Query values are
        /// percent-encoded; use <see cref="Uri.AbsoluteUri"/> (not
        /// <see cref="Uri.ToString"/>, which unescapes) when navigating.
        /// </summary>
        /// <param name="criteria">The validated search criteria. Must not be null.</param>
        /// <param name="start">The zero-based result offset; 0 for the first page.</param>
        /// <returns>The absolute guest search URL.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="criteria"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="start"/> is negative.</exception>
        public static Uri SearchUrl(SearchCriteria criteria, int start = 0)
        {
            ArgumentNullException.ThrowIfNull(criteria);
            ArgumentOutOfRangeException.ThrowIfNegative(start);

            var locations = criteria.Locations.Count > 0
                ? string.Join(", ", criteria.Locations)
                : string.Empty;
            var query = new List<string> { QueryString.Pair("keywords", criteria.Keywords) };
            if (locations.Length > 0)
            {
                query.Add(QueryString.Pair("location", locations));
            }

            if (criteria.PostedWithinDays is { } days)
            {
                query.Add(QueryString.Pair("f_TPR", $"r{days * 24 * 3600}"));
            }

            if (start > 0)
            {
                query.Add(QueryString.Pair("start", start.ToString(CultureInfo.InvariantCulture)));
            }

            return new Uri($"{Origin}jobs/search?{string.Join('&', query)}", UriKind.Absolute);
        }
    }
}
