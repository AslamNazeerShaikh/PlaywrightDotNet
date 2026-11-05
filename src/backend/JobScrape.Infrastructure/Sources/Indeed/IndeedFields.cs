using System.Globalization;
using System.Text.RegularExpressions;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;

namespace JobScrape.Infrastructure.Sources.Indeed
{
    /// <summary>
    /// Field normalization shared by the Indeed parsers
    /// (docs/08-site-playbooks.md §6). Every method is total: unknown or
    /// ambiguous input yields null, never an exception, and every value it
    /// returns already satisfies the domain invariants.
    /// </summary>
    internal static partial class IndeedFields
    {
        /// <summary>
        /// One lakh rupees.
        /// </summary>
        private const decimal Lakh = 100_000m;

        /// <summary>
        /// One crore rupees.
        /// </summary>
        private const decimal Crore = 10_000_000m;

        /// <summary>
        /// The only hosts Indeed job URLs may use.
        /// </summary>
        private static readonly HashSet<string> Hosts = new(StringComparer.OrdinalIgnoreCase)
        {
            "in.indeed.com",
            "www.indeed.com",
            "indeed.com",
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
        /// Validates an Indeed job link and canonicalizes it to
        /// <c>https://in.indeed.com/viewjob?jk=&lt;id&gt;</c>: absolute
        /// <c>https</c> on an Indeed host, with a non-blank <c>jk</c> query
        /// value. Tracking parameters are dropped; <c>jk</c> is kept because
        /// it is the external id.
        /// </summary>
        /// <param name="href">The raw link.</param>
        /// <param name="baseUrl">The page URL, for resolving relative links.</param>
        /// <param name="canonical">The canonical URL.</param>
        /// <param name="jobKey">The <c>jk</c> value, if any.</param>
        /// <returns>True when the link is an Indeed job page.</returns>
        public static bool TryJobUrl(
            string? href,
            string? baseUrl,
            out string canonical,
            out string? jobKey
        )
        {
            canonical = string.Empty;
            jobKey = null;
            if (!TryResolve(href, baseUrl, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            {
                return false;
            }

            if (
                !Hosts.Contains(uri.IdnHost)
                || uri.UserInfo.Length > 0
                || !uri.IsDefaultPort
                || !uri.AbsolutePath.StartsWith("/viewjob", StringComparison.OrdinalIgnoreCase)
            )
            {
                return false;
            }

            jobKey = QueryValue(uri.Query, "jk");
            if (string.IsNullOrEmpty(jobKey) || jobKey.Length > CanonicalUrl.MaxLength)
            {
                jobKey = null;
                return false;
            }

            canonical =
                $"https://{IndeedUrlBuilder.Host}/viewjob?jk={Uri.EscapeDataString(jobKey)}";
            return canonical.Length <= CanonicalUrl.MaxLength;
        }

        /// <summary>
        /// An Indeed job key: the <c>jk</c> query value, alphanumeric.
        /// </summary>
        /// <param name="value">The candidate key.</param>
        /// <returns>The trimmed key, or null when it is not alphanumeric.</returns>
        public static string? JobKey(string? value)
        {
            var trimmed = value?.Trim();
            return
                trimmed is not null && trimmed.Length > 0 && trimmed.All(char.IsAsciiLetterOrDigit)
                ? trimmed
                : null;
        }

        /// <summary>
        /// A company page link with query and fragment stripped; any
        /// <c>http(s)</c> host, relative links resolved against Indeed.
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
        /// An external apply link: absolute <c>https</c>, off Indeed's
        /// domains, no credentials. Kept whole because ATS links can need
        /// their query.
        /// </summary>
        /// <param name="href">The raw link.</param>
        /// <param name="baseUrl">The page URL, for resolving relative links.</param>
        /// <returns>The link, or null when it is internal or unsafe.</returns>
        public static string? ExternalApplyUrl(string? href, string? baseUrl)
        {
            if (
                string.IsNullOrWhiteSpace(href)
                || !TryResolve(href, baseUrl, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps
                || uri.UserInfo.Length > 0
                || Hosts.Contains(uri.IdnHost)
            )
            {
                return null;
            }

            return uri.AbsoluteUri.Length <= JobPosting.MaxExternalUrlLength
                ? uri.AbsoluteUri
                : null;
        }

        /// <summary>
        /// Parses Indian salary text: <c>₹8,00,000 - ₹12,00,000 a year</c>,
        /// <c>₹15,000 a month</c>, <c>15-27.5 Lacs PA</c>, or
        /// <c>₹ 8 LPA</c>. Amounts convert to rupees (1 lakh = 100,000;
        /// 1 crore = 10,000,000). Anything without an explicit period
        /// yields null; min &gt; max is rejected.
        /// </summary>
        /// <param name="raw">The salary text.</param>
        /// <returns>The salary, or null when not unambiguous.</returns>
        public static SalaryRange? Salary(string? raw)
        {
            var text = Clean(raw);
            if (text is null)
            {
                return null;
            }

            var match = SalaryPattern()
                .Match(text.Replace(",", string.Empty, StringComparison.Ordinal));
            if (!match.Success)
            {
                return null;
            }

            var unit = match.Groups["unit"].Value.ToUpperInvariant();
            var periodText = match.Groups["period"].Value;
            SalaryPeriod? period =
                YearPeriod().IsMatch(periodText) ? SalaryPeriod.Year
                : MonthPeriod().IsMatch(periodText) ? SalaryPeriod.Month
                : null;
            if (unit == "LPA")
            {
                // "LPA" already says per annum; a contradicting suffix is ambiguous.
                period =
                    periodText.Length == 0 || period == SalaryPeriod.Year
                        ? SalaryPeriod.Year
                        : null;
            }

            if (period is null)
            {
                return null;
            }

            var multiplier = unit switch
            {
                "" => 1m,
                "CR" or "CRORE" or "CRORES" => Crore,
                _ => Lakh,
            };
            var min = Amount(match.Groups["min"].Value, multiplier);
            var max = match.Groups["max"].Success
                ? Amount(match.Groups["max"].Value, multiplier)
                : min;

            // A bare number without lakh/crore is only credible as rupees.
            if (min is null || max is null || min > max || (unit.Length == 0 && min < 1000))
            {
                return null;
            }

            return new SalaryRange(min, max, "INR", period.Value);
        }

        /// <summary>
        /// Resolves relative posted text against the run time
        /// (playbook §6.5): <c>Posted 3 days ago</c> and Indeed's
        /// <c>EmployerActive 2 days ago</c> → start of the UTC day n days
        /// back; <c>Just posted</c>, <c>Today</c>, <c>Active today</c>,
        /// hours/minutes → start of <paramref name="now"/>'s UTC day;
        /// <c>30+ days ago</c> and anything unrecognized yield null.
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
                : match.Groups["yesterday"].Success ? 1
                : 0;
            return new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero).AddDays(-daysBack);
        }

        /// <summary>
        /// Parses an ISO 8601 date or date-time (JSON-LD <c>datePosted</c>)
        /// as UTC; a bare date means midnight UTC.
        /// </summary>
        /// <param name="raw">The date text.</param>
        /// <returns>The UTC instant, or null when unparseable.</returns>
        public static DateTimeOffset? IsoDate(string? raw)
        {
            return DateTimeOffset.TryParse(
                raw?.Trim(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var value
            )
                ? value.ToUniversalTime()
                : null;
        }

        /// <summary>
        /// Builds a location from Indeed's location text (playbook §6.3).
        /// Work mode: <c>Hybrid</c> → Hybrid; <c>Remote</c> → Remote; an
        /// explicit on-site marker → Onsite; a plain city list → Unknown,
        /// because Indeed does not state it and the mode is never guessed.
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
        /// Resolves a link against Indeed's origin.
        /// </summary>
        /// <param name="href">The raw link.</param>
        /// <param name="baseUrl">The page URL, for resolving relative links.</param>
        /// <param name="uri">The absolute URI.</param>
        /// <returns>True when the link is a usable URI.</returns>
        private static bool TryResolve(string? href, string? baseUrl, out Uri uri)
        {
            uri = new Uri($"https://{IndeedUrlBuilder.Host}/");
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

        /// <summary>
        /// Reads one query value from a <c>?a=b&amp;c=d</c> string.
        /// </summary>
        /// <param name="query">The query including the leading <c>?</c>.</param>
        /// <param name="name">The parameter name.</param>
        /// <returns>The decoded value, or null.</returns>
        private static string? QueryValue(string query, string name)
        {
            foreach (
                var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            )
            {
                var equals = pair.IndexOf('=', StringComparison.Ordinal);
                if (equals <= 0)
                {
                    continue;
                }

                if (
                    string.Equals(
                        Uri.UnescapeDataString(pair[..equals]),
                        name,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    return Uri.UnescapeDataString(pair[(equals + 1)..]);
                }
            }

            return null;
        }

        /// <summary>
        /// Scales a decimal amount, rounding to whole rupees.
        /// </summary>
        /// <param name="digits">The amount text.</param>
        /// <param name="multiplier">Rupees per unit.</param>
        /// <returns>The amount in rupees, or null when unparseable.</returns>
        private static decimal? Amount(string digits, decimal multiplier)
        {
            return decimal.TryParse(
                digits,
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var value
            )
                ? decimal.Round(value * multiplier, 0)
                : null;
        }

        [GeneratedRegex(
            @"^(?:₹|inr|rs\.?)?\s*(?<min>\d{1,9}(?:\.\d{1,4})?)\s*(?:(?:-|–|to)\s*(?:₹|inr|rs\.?)?\s*(?<max>\d{1,9}(?:\.\d{1,4})?))?\s*(?<unit>lpa|lacs?|lakhs?|crores?|cr)?\s*(?<period>a\s+year|a\s+month|per\s+year|per\s+month|yearly|monthly|annually|p\.?\s?a\.?|p\.?\s?m\.?|per\s+annum)?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        )]
        private static partial Regex SalaryPattern();

        [GeneratedRegex(
            @"^(?:a\s+year|per\s+year|per\s+annum|yearly|annually|p\.?\s?a\.?)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        )]
        private static partial Regex YearPeriod();

        [GeneratedRegex(
            @"^(?:a\s+month|per\s+month|monthly|p\.?\s?m\.?)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        )]
        private static partial Regex MonthPeriod();

        [GeneratedRegex(
            @"^(?:(?:posted|employer\s*active|active)\s*)?(?:(?<days>\d{1,4})\s+days?\s+ago|(?<yesterday>yesterday|a\s+day\s+ago)|just\s+posted|today|active\s+today|(?:few|an?|\d{1,2})\s+(?:hours?|hrs?|minutes?|mins?|seconds?|secs?)\s+ago)$",
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
