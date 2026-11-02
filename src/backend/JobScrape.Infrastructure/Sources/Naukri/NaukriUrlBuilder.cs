using System.Globalization;
using System.Text;
using JobScrape.Domain.Sourcing;

namespace JobScrape.Infrastructure.Sources.Naukri
{
    /// <summary>
    /// Builds Naukri page-1 search URLs (docs/08-site-playbooks.md §3.3).
    /// Later pages are reached by clicking the native <b>Next</b> control, so
    /// no builder emits a <c>-2</c> slug URL: the SPA loses its filters there.
    /// </summary>
    public static class NaukriUrlBuilder
    {
        /// <summary>
        /// The only Naukri host the adapter navigates to.
        /// </summary>
        private const string Origin = "https://www.naukri.com/";

        /// <summary>
        /// Turns free text into a Naukri path slug, following the verified
        /// rules of the old project: lower-case; <c>#</c> becomes
        /// <c>sharp</c>; <c>.</c> becomes <c>dot</c>; whitespace and hyphens
        /// become <c>-</c>; commas and every other non-alphanumeric character
        /// are removed; dash runs collapse and edge dashes are trimmed. Only
        /// ASCII letters and digits survive, so the slug is always URL-safe.
        /// </summary>
        /// <example>
        /// <c>"dot net developer"</c> → <c>dot-net-developer</c>;
        /// <c>"C#"</c> → <c>csharp</c>; <c>".NET Core"</c> → <c>dotnet-core</c>;
        /// <c>"asp.net core c#"</c> → <c>aspdotnet-core-csharp</c>;
        /// <c>"Delhi / NCR"</c> → <c>delhi-ncr</c>; <c>"C++"</c> → <c>c</c>.
        /// </example>
        /// <param name="text">The text to slug. Must not be null.</param>
        /// <returns>The slug; empty when no letter or digit survives.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        public static string Slug(string text)
        {
            ArgumentNullException.ThrowIfNull(text);

            var builder = new StringBuilder(text.Length * 2);
            foreach (var character in text)
            {
                var lower = char.ToLowerInvariant(character);
                if (char.IsAsciiLetterOrDigit(lower))
                {
                    builder.Append(lower);
                }
                else if (lower == '#')
                {
                    builder.Append("sharp");
                }
                else if (lower == '.')
                {
                    builder.Append("dot");
                }
                else if (
                    (char.IsWhiteSpace(lower) || lower == '-')
                    && builder.Length > 0
                    && builder[^1] != '-'
                )
                {
                    builder.Append('-');
                }
            }

            return builder.ToString().TrimEnd('-');
        }

        /// <summary>
        /// Builds the page-1 search URL in the slug form:
        /// <c>https://www.naukri.com/{slug(keywords)}-jobs-in-{slug(firstCity)}?k=…&amp;l=…&amp;experience=…&amp;jobAge=…&amp;sort=…</c>.
        /// The path carries the first city only; <c>l</c> carries every city,
        /// joined with <c>", "</c>. <c>experience</c> and <c>jobAge</c> are
        /// omitted when the criteria leave them unset. <c>sort=date</c> rides
        /// along with <c>jobAge</c> so the freshest postings come first;
        /// both are best-effort hints and the run handler re-checks every
        /// posted date. Without locations (a
        /// remote-only profile) the path is <c>{slug(keywords)}-jobs</c> and
        /// <c>l</c> is omitted: remote is <b>not</b> filtered through the URL,
        /// because no verified Naukri query parameter exists for it; remote
        /// postings are recognized from each card's location text instead.
        /// When the keywords slug to nothing (e.g. <c>"++"</c>) the path falls
        /// back to <c>jobs-in-{city}</c> or <c>jobs</c>; <c>k</c> still
        /// carries the keywords. Query values are percent-encoded; use
        /// <see cref="Uri.AbsoluteUri"/> (not <see cref="Uri.ToString"/>,
        /// which unescapes) when navigating.
        /// </summary>
        /// <param name="criteria">The validated search criteria. Must not be null.</param>
        /// <returns>The absolute page-1 URL.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="criteria"/> is null.</exception>
        public static Uri SearchUrl(SearchCriteria criteria)
        {
            ArgumentNullException.ThrowIfNull(criteria);

            var keywordSlug = Slug(criteria.Keywords);
            var citySlug =
                criteria.Locations.Count > 0 ? Slug(criteria.Locations[0]) : string.Empty;
            var path = (keywordSlug.Length > 0, citySlug.Length > 0) switch
            {
                (true, true) => $"{keywordSlug}-jobs-in-{citySlug}",
                (true, false) => $"{keywordSlug}-jobs",
                (false, true) => $"jobs-in-{citySlug}",
                _ => "jobs",
            };

            var query = new List<string> { Pair("k", criteria.Keywords) };
            if (criteria.Locations.Count > 0)
            {
                query.Add(Pair("l", string.Join(", ", criteria.Locations)));
            }

            if (criteria.ExperienceYears is { } years)
            {
                query.Add(Pair("experience", years.ToString(CultureInfo.InvariantCulture)));
            }

            if (criteria.PostedWithinDays is { } days)
            {
                query.Add(Pair("jobAge", days.ToString(CultureInfo.InvariantCulture)));
                query.Add(Pair("sort", "date"));
            }

            return new Uri($"{Origin}{path}?{string.Join('&', query)}", UriKind.Absolute);
        }

        /// <summary>
        /// Formats one percent-encoded query pair.
        /// </summary>
        /// <param name="name">The parameter name.</param>
        /// <param name="value">The raw value.</param>
        /// <returns>The encoded <c>name=value</c> pair.</returns>
        private static string Pair(string name, string value)
        {
            return $"{name}={Uri.EscapeDataString(value)}";
        }
    }
}
