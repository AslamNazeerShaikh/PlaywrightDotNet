using System.Globalization;
using JobScrape.Domain.Sourcing;

namespace JobScrape.Infrastructure.Sources.Indeed
{
    /// <summary>
    /// Builds Indeed India search URLs (docs/08-site-playbooks.md §5.2):
    /// <c>https://in.indeed.com/jobs?q=&amp;l=&amp;fromage=&amp;start=</c>.
    /// <c>start</c> is a zero-based result offset in steps of 10; later pages
    /// navigate directly with a larger offset. <c>fromage</c> is the
    /// posted-within-days filter.
    /// </summary>
    public static class IndeedUrlBuilder
    {
        /// <summary>
        /// The only Indeed host the adapter navigates to.
        /// </summary>
        public const string Host = "in.indeed.com";

        /// <summary>
        /// Results per search page; the <c>start</c> offset moves in steps of this size.
        /// </summary>
        public const int PageSize = 10;

        private const string Origin = "https://in.indeed.com/";

        /// <summary>
        /// Builds the search URL for one page. Query values are
        /// percent-encoded; use <see cref="Uri.AbsoluteUri"/> (not
        /// <see cref="Uri.ToString"/>, which unescapes) when navigating.
        /// </summary>
        /// <param name="criteria">The validated search criteria. Must not be null.</param>
        /// <param name="start">The zero-based result offset; 0 for the first page.</param>
        /// <returns>The absolute search URL.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="criteria"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="start"/> is negative.</exception>
        public static Uri SearchUrl(SearchCriteria criteria, int start = 0)
        {
            ArgumentNullException.ThrowIfNull(criteria);
            ArgumentOutOfRangeException.ThrowIfNegative(start);

            var locations =
                criteria.Locations.Count > 0 ? string.Join(", ", criteria.Locations) : string.Empty;
            var query = new List<string> { QueryString.Pair("q", criteria.Keywords) };
            if (locations.Length > 0)
            {
                query.Add(QueryString.Pair("l", locations));
            }

            if (criteria.PostedWithinDays is { } days)
            {
                query.Add(QueryString.Pair("fromage", days.ToString(CultureInfo.InvariantCulture)));
            }

            if (start > 0)
            {
                query.Add(QueryString.Pair("start", start.ToString(CultureInfo.InvariantCulture)));
            }

            return new Uri($"{Origin}jobs?{string.Join('&', query)}", UriKind.Absolute);
        }
    }
}
