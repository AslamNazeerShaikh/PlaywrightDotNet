using System.Globalization;
using System.Text.RegularExpressions;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;

namespace JobScrape.Infrastructure.Sources.LinkedIn
{
    /// <summary>
    /// Field normalization shared by the LinkedIn parsers
    /// (docs/08-site-playbooks.md §6). Every method is total: unknown or
    /// ambiguous input yields null, never an exception, and every value it
    /// returns already satisfies the domain invariants.
    /// </summary>
    internal static partial class LinkedInFields
    {
        /// <summary>
        /// The only hosts LinkedIn job URLs may use.
        /// </summary>
        private static readonly HashSet<string> Hosts = new(StringComparer.OrdinalIgnoreCase)
        {
            "www.linkedin.com",
            "linkedin.com",
        };

        /// <summary>
        /// Collapses whitespace, replaces control characters, and truncates.
        /// </summary>
        /// <param name="raw">The raw text.</param>
        /// <param name="maxLength">Maximum length of the result.</param>
        /// <returns>The cleaned text, or null when blank.</returns>
        public static string? Clean(string? raw, int maxLength = int.MaxValue)
        {
            if (raw is null)
            {
                return null;
            }

            var visible = string.Create(
                raw.Length,
                raw,
                static (span, source) =>
                {
                    for (var index = 0; index < source.Length; index++)
                    {
                        span[index] = char.IsControl(source[index]) ? ' ' : source[index];
                    }
                }
            );
            var collapsed = Text.Collapse(visible);
            if (collapsed.Length > maxLength)
            {
                collapsed = collapsed[..maxLength].TrimEnd();
            }

            return collapsed.Length == 0 ? null : collapsed;
        }

        /// <summary>
        /// Trims multi-line text and caps it to the domain's description limit.
        /// </summary>
        /// <param name="text">The text, possibly with line breaks.</param>
        /// <returns>The capped text, or null when blank.</returns>
        public static string? LongText(string? text)
        {
            var trimmed = text?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return null;
            }

            return trimmed.Length <= JobPosting.MaxDescriptionLength
                ? trimmed
                : trimmed[..JobPosting.MaxDescriptionLength].TrimEnd();
        }

        /// <summary>
        /// Cleans skill tags, keeps the first spelling of each
        /// case-insensitive name, and caps the list at the domain maximum.
        /// </summary>
        /// <param name="raw">The raw tags.</param>
        /// <returns>The distinct skills in first-seen order.</returns>
        public static IReadOnlyList<string> Skills(IEnumerable<string?> raw)
        {
            return raw.Select(skill => Clean(skill))
                .OfType<string>()
                .DistinctBy(skill => skill.ToUpperInvariant(), StringComparer.Ordinal)
                .Take(JobPosting.MaxSkills)
                .ToArray();
        }

        /// <summary>
        /// Validates a LinkedIn job link and canonicalizes it to
        /// <c>https://www.linkedin.com/jobs/view/&lt;id&gt;</c>: absolute
        /// <c>https</c> on a LinkedIn host, with a numeric
        /// <c>/jobs/view/&lt;id&gt;</c> path. Tracking parameters and
        /// fragments are dropped; the numeric id is kept because it is the
        /// external id.
        /// </summary>
        /// <param name="href">The raw link.</param>
        /// <param name="baseUrl">The page URL, for resolving relative links.</param>
        /// <param name="canonical">The canonical URL.</param>
        /// <param name="idFromUrl">The numeric path id, if any.</param>
        /// <returns>True when the link is a LinkedIn job page.</returns>
        public static bool TryJobUrl(
            string? href,
            string? baseUrl,
            out string canonical,
            out string? idFromUrl
        )
        {
            canonical = string.Empty;
            idFromUrl = null;
            if (!TryResolve(href, baseUrl, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            {
                return false;
            }

            if (!Hosts.Contains(uri.IdnHost) || uri.UserInfo.Length > 0 || !uri.IsDefaultPort)
            {
                return false;
            }

            var match = ViewPath().Match(uri.AbsolutePath.TrimEnd('/'));
            if (!match.Success)
            {
                return false;
            }

            canonical = $"https://{LinkedInUrlBuilder.Host}/jobs/view/{match.Groups["id"].Value}";
            if (canonical.Length > CanonicalUrl.MaxLength)
            {
                canonical = string.Empty;
                return false;
            }

            idFromUrl = match.Groups["id"].Value;
            return true;
        }

        /// <summary>
        /// A LinkedIn job id: digits only.
        /// </summary>
        /// <param name="value">The candidate id.</param>
        /// <returns>The trimmed id, or null when it is not all digits.</returns>
        public static string? NumericId(string? value)
        {
            var trimmed = value?.Trim();
            return trimmed is not null && NumericIdPattern().IsMatch(trimmed) ? trimmed : null;
        }

        /// <summary>
        /// A company page link with query and fragment stripped; any
        /// <c>http(s)</c> host, relative links resolved against LinkedIn.
        /// </summary>
        /// <param name="href">The raw link.</param>
        /// <param name="baseUrl">The page URL, for resolving relative links.</param>
        /// <returns>The link, or null when unusable.</returns>
        public static string? CompanyPageUrl(string? href, string? baseUrl)
        {
            if (
                !TryResolve(href, baseUrl, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
                || uri.UserInfo.Length > 0
            )
            {
                return null;
            }

            var url = uri.GetLeftPart(UriPartial.Path);
            return url.Length <= JobPosting.MaxExternalUrlLength ? url : null;
        }

        /// <summary>
        /// Resolves relative posted text against the run time
        /// (playbook §6.5): <c>n days/weeks ago</c> → start of the UTC day
        /// n (or 7n) days before <paramref name="now"/>; <c>Just now</c>,
        /// hours, and minutes → start of <paramref name="now"/>'s UTC day.
        /// Months and anything unrecognized yield null: a date is never guessed.
        /// </summary>
        /// <param name="raw">The posted text.</param>
        /// <param name="now">The run time.</param>
        /// <returns>The UTC posted date, or null.</returns>
        public static DateTimeOffset? Posted(string? raw, DateTimeOffset now)
        {
            var text = Clean(raw);
            if (text is null)
            {
                return null;
            }

            var match = PostedPattern().Match(text);
            if (!match.Success)
            {
                return null;
            }

            var daysBack =
                match.Groups["days"].Success
                    ? int.Parse(match.Groups["days"].Value, CultureInfo.InvariantCulture)
                : match.Groups["weeks"].Success
                    ? 7 * int.Parse(match.Groups["weeks"].Value, CultureInfo.InvariantCulture)
                : 0;
            return new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero).AddDays(-daysBack);
        }

        /// <summary>
        /// Builds a location from LinkedIn's location text (playbook §6.3).
        /// Work mode: <c>Hybrid</c> → Hybrid; <c>Remote</c> → Remote; an
        /// explicit on-site marker → Onsite; a plain city list → Unknown,
        /// because LinkedIn does not state it and the mode is never guessed.
        /// City is the first listed place with any work-mode word removed.
        /// Region and country stay null.
        /// </summary>
        /// <param name="raw">The location text as shown.</param>
        /// <returns>The location, or null when the text is blank.</returns>
        public static JobLocation? Location(string? raw)
        {
            var text = Clean(raw);
            if (text is null)
            {
                return null;
            }

            var mode = WorkModeOf(text);
            var city = text.Split(
                    ',',
                    StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
                )
                .Select(place => Clean(ModeWords().Replace(place, " ")))
                .FirstOrDefault(place =>
                    place is not null && WorkModeOf(place) == WorkMode.Unknown
                );
            return new JobLocation(text, city, null, null, mode);
        }

        /// <summary>
        /// Detects the work mode named in location text.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns>The mode, or Unknown.</returns>
        private static WorkMode WorkModeOf(string text)
        {
            if (HybridPattern().IsMatch(text))
            {
                return WorkMode.Hybrid;
            }

            if (RemotePattern().IsMatch(text))
            {
                return WorkMode.Remote;
            }

            return OnsitePattern().IsMatch(text) ? WorkMode.Onsite : WorkMode.Unknown;
        }

        /// <summary>
        /// Resolves a link against LinkedIn's origin.
        /// </summary>
        /// <param name="href">The raw link.</param>
        /// <param name="baseUrl">The page URL, for resolving relative links.</param>
        /// <param name="uri">The absolute URI.</param>
        /// <returns>True when the link is a usable URI.</returns>
        private static bool TryResolve(string? href, string? baseUrl, out Uri uri)
        {
            uri = new Uri($"https://{LinkedInUrlBuilder.Host}/");
            var trimmed = href?.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.Length > CanonicalUrl.MaxLength)
            {
                return false;
            }

            var origin = Uri.TryCreate(baseUrl, UriKind.Absolute, out var page) ? page : uri;
            if (Uri.TryCreate(origin, trimmed, out var resolved))
            {
                uri = resolved;
                return true;
            }

            return false;
        }

        [GeneratedRegex(@"/jobs/view/(?<id>\d{1,30})$", RegexOptions.CultureInvariant)]
        private static partial Regex ViewPath();

        [GeneratedRegex(@"^\d{1,30}$", RegexOptions.CultureInvariant)]
        private static partial Regex NumericIdPattern();

        [GeneratedRegex(
            @"^(?:(?<days>\d{1,4})\s+days?\s+ago|(?<weeks>\d{1,3})\s+weeks?\s+ago|just\s+now|today|(?:few|an?|\d{1,2})\s+(?:hours?|hrs?|minutes?|mins?|seconds?|secs?)\s+ago)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        )]
        private static partial Regex PostedPattern();

        [GeneratedRegex(@"\bhybrid\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex HybridPattern();

        [GeneratedRegex(
            @"\b(?:remote|work\s+from\s+home|wfh)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        )]
        private static partial Regex RemotePattern();

        [GeneratedRegex(
            @"\b(?:work\s+from\s+office|wfo|on-?site|in-?office|in\s+person)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        )]
        private static partial Regex OnsitePattern();

        [GeneratedRegex(
            @"\b(?:hybrid|remote|work\s+from\s+home|wfh|work\s+from\s+office|wfo|on-?site|in-?office|in\s+person)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        )]
        private static partial Regex ModeWords();
    }
}
