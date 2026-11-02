using System.Globalization;
using System.Text.RegularExpressions;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;

namespace JobScrape.Infrastructure.Sources.Naukri
{
    /// <summary>
    /// Field normalization shared by the Naukri parsers
    /// (docs/08-site-playbooks.md §6). Every method is total: unknown or
    /// ambiguous input yields null, never an exception, and every value it
    /// returns already satisfies the domain invariants.
    /// </summary>
    internal static partial class NaukriFields
    {
        /// <summary>
        /// The only host Naukri job URLs may use.
        /// </summary>
        public const string Host = "www.naukri.com";

        /// <summary>
        /// Path prefix of a Naukri job page.
        /// </summary>
        private const string JobPathPrefix = "/job-listings-";

        /// <summary>
        /// One lakh rupees.
        /// </summary>
        private const decimal Lakh = 100_000m;

        /// <summary>
        /// One crore rupees.
        /// </summary>
        private const decimal Crore = 10_000_000m;

        /// <summary>
        /// Base for resolving root-relative and protocol-relative links.
        /// </summary>
        private static readonly Uri Origin = new("https://" + Host + "/");

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
        /// Validates a Naukri job link and canonicalizes it: absolute (or
        /// root-relative) <c>https://www.naukri.com/job-listings-…</c>, no
        /// credentials or custom port; query and fragment dropped.
        /// </summary>
        /// <param name="href">The raw link.</param>
        /// <param name="canonical">The canonical URL.</param>
        /// <param name="idFromUrl">The numeric suffix of the path, if any.</param>
        /// <returns>True when the link is a Naukri job page.</returns>
        public static bool TryJobUrl(string? href, out string canonical, out string? idFromUrl)
        {
            canonical = string.Empty;
            idFromUrl = null;
            if (!TryResolve(href, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            {
                return false;
            }

            var path = uri.AbsolutePath.TrimEnd('/');
            if (
                !string.Equals(uri.IdnHost, Host, StringComparison.OrdinalIgnoreCase)
                || uri.UserInfo.Length > 0
                || !uri.IsDefaultPort
                || !path.StartsWith(JobPathPrefix, StringComparison.OrdinalIgnoreCase)
            )
            {
                return false;
            }

            canonical = $"https://{Host}{path}";
            if (canonical.Length > CanonicalUrl.MaxLength)
            {
                canonical = string.Empty;
                return false;
            }
            var suffix = IdSuffix().Match(path);
            idFromUrl = suffix.Success ? suffix.Groups["id"].Value : null;
            return true;
        }

        /// <summary>
        /// A Naukri id: digits only.
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
        /// http(s) host, relative links resolved against Naukri.
        /// </summary>
        /// <param name="href">The raw link.</param>
        /// <returns>The link, or null when unusable.</returns>
        public static string? CompanyPageUrl(string? href)
        {
            if (
                !TryResolve(href, out var uri)
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
        /// An external apply link: absolute <c>https</c>, off Naukri's
        /// domains, no credentials. Kept whole because ATS links can need
        /// their query.
        /// </summary>
        /// <param name="href">The raw link.</param>
        /// <returns>The link, or null when it is internal or unsafe.</returns>
        public static string? ExternalApplyUrl(string? href)
        {
            if (
                string.IsNullOrWhiteSpace(href)
                || !Uri.TryCreate(href.Trim(), UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps
                || uri.UserInfo.Length > 0
                || IsNaukriDomain(uri.IdnHost)
            )
            {
                return null;
            }

            return uri.AbsoluteUri.Length <= JobPosting.MaxExternalUrlLength
                ? uri.AbsoluteUri
                : null;
        }

        /// <summary>
        /// Parses experience text such as <c>5-8 Yrs</c>, <c>4 - 9 years</c>,
        /// <c>10+ Yrs</c> (min only), or <c>3 Yrs</c> (min = max). Values
        /// outside 0–50 or with min &gt; max are rejected.
        /// </summary>
        /// <param name="raw">The experience text.</param>
        /// <returns>The parsed bounds; both null when not unambiguous.</returns>
        public static (int? Min, int? Max) Experience(string? raw)
        {
            var text = Clean(raw);
            if (text is null)
            {
                return (null, null);
            }

            var match = ExperiencePattern().Match(text);
            if (!match.Success)
            {
                return (null, null);
            }

            var min = int.Parse(match.Groups["min"].Value, CultureInfo.InvariantCulture);
            int? max =
                match.Groups["max"].Success
                    ? int.Parse(match.Groups["max"].Value, CultureInfo.InvariantCulture)
                : match.Groups["plus"].Success ? null
                : min;
            return min > 50 || max > 50 || min > max ? (null, null) : (min, max);
        }

        /// <summary>
        /// Builds the experience bounds from JSON-LD
        /// <c>monthsOfExperience</c>, which schema.org defines as a minimum.
        /// </summary>
        /// <param name="months">The month count text.</param>
        /// <returns>The minimum whole years, or null when unusable.</returns>
        public static int? ExperienceFromMonths(string? months)
        {
            return
                int.TryParse(
                    months?.Trim(),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var value
                )
                && value / 12 <= 50
                ? value / 12
                : null;
        }

        /// <summary>
        /// Parses Indian salary text: <c>15-27.5 Lacs PA</c>,
        /// <c>12-18 Lacs P.A.</c>, <c>25 Lacs PA</c> (min = max),
        /// <c>1-1.5 Cr PA</c>, <c>₹ 8 LPA</c>, or plain rupee amounts with a
        /// period such as <c>5,00,000 - 8,00,000 P.A.</c>. The amount is
        /// converted to rupees (1 lakh = 100,000; 1 crore = 10,000,000).
        /// Anything without an explicit period (PA/PM/LPA), such as
        /// <c>Not disclosed</c>, yields null.
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
        /// (playbook §6.5): <c>n days ago</c> → start of the UTC day n days
        /// before <paramref name="now"/>; <c>Just now</c>, <c>Today</c>,
        /// <c>Few hours ago</c>, <c>n hours/minutes ago</c> → start of
        /// <paramref name="now"/>'s UTC day; <c>Yesterday</c> → one day
        /// back. Open-ended text (<c>30+ days ago</c>) and anything
        /// unrecognized yield null: a date is never guessed.
        /// </summary>
        /// <param name="raw">The posted text, optionally prefixed with <c>Posted:</c>.</param>
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
        /// Builds a location from Naukri's location text (playbook §6.3).
        /// Work mode: a <c>Hybrid</c> prefix → Hybrid; <c>Remote</c>,
        /// <c>Work from home</c>, or <c>WFH</c> → Remote; an explicit
        /// <c>Work from office</c>/<c>On-site</c> → Onsite; a plain city
        /// list → Unknown, because Naukri does not state it and the mode is
        /// never guessed. City is the first listed city with any work-mode
        /// prefix and parenthetical (<c>(All Areas)</c>) removed. Region and
        /// country stay null: Naukri lists overseas jobs too and states no
        /// country on cards, so <c>India</c> would be invented.
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
            var places = text;
            var dash = text.IndexOf(" - ", StringComparison.Ordinal);
            if (dash > 0 && WorkModeOf(text[..dash]) != WorkMode.Unknown)
            {
                places = text[(dash + 3)..];
            }

            var city = places
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(place => Clean(Parenthetical().Replace(place, " ")))
                .FirstOrDefault(place =>
                    place is not null && WorkModeOf(place) == WorkMode.Unknown
                );
            return new JobLocation(text, city, null, null, mode);
        }

        /// <summary>
        /// Whether a host belongs to Naukri (the site or a subdomain).
        /// </summary>
        /// <param name="host">The host.</param>
        /// <returns>True for <c>naukri.com</c> and its subdomains.</returns>
        public static bool IsNaukriDomain(string host)
        {
            return host.Equals("naukri.com", StringComparison.OrdinalIgnoreCase)
                || host.EndsWith(".naukri.com", StringComparison.OrdinalIgnoreCase);
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
        /// Resolves a link against the Naukri origin.
        /// </summary>
        /// <param name="href">The raw link.</param>
        /// <param name="uri">The absolute URI.</param>
        /// <returns>True when the link is a usable URI.</returns>
        private static bool TryResolve(string? href, out Uri uri)
        {
            uri = Origin;
            var trimmed = href?.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.Length > CanonicalUrl.MaxLength)
            {
                return false;
            }

            if (Uri.TryCreate(Origin, trimmed, out var resolved))
            {
                uri = resolved;
                return true;
            }

            return false;
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

        [GeneratedRegex(@"-(?<id>\d{1,30})$", RegexOptions.CultureInvariant)]
        private static partial Regex IdSuffix();

        [GeneratedRegex(@"^\d{1,30}$", RegexOptions.CultureInvariant)]
        private static partial Regex NumericIdPattern();

        [GeneratedRegex(
            @"^(?<min>\d{1,3})\s*(?:(?:-|–|to)\s*(?<max>\d{1,3})|(?<plus>\+))?\s*(?:yrs?|years?)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        )]
        private static partial Regex ExperiencePattern();

        [GeneratedRegex(
            @"^(?:₹|inr|rs\.?)?\s*(?<min>\d{1,9}(?:\.\d{1,4})?)\s*(?:(?:-|–|to)\s*(?:₹|inr|rs\.?)?\s*(?<max>\d{1,9}(?:\.\d{1,4})?))?\s*(?<unit>lpa|lacs?|lakhs?|crores?|cr)?\s*(?<period>p\.?\s?a\.?|per\s+annum|per\s+year|yearly|annually|p\.?\s?m\.?|per\s+month|monthly)?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        )]
        private static partial Regex SalaryPattern();

        [GeneratedRegex(
            @"^(?:p\.?\s?a\.?|per\s+annum|per\s+year|yearly|annually)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        )]
        private static partial Regex YearPeriod();

        [GeneratedRegex(
            @"^(?:p\.?\s?m\.?|per\s+month|monthly)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        )]
        private static partial Regex MonthPeriod();

        [GeneratedRegex(
            @"^(?:posted\s*:?\s*)?(?:(?<days>\d{1,4})\s+days?\s+ago|(?<yesterday>yesterday|a\s+day\s+ago)|just\s+now|today|(?:few|an?|\d{1,2})\s+(?:hours?|hrs?|minutes?|mins?|seconds?|secs?)\s+ago)$",
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
            @"\b(?:work\s+from\s+office|wfo|on-?site|in-?office)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        )]
        private static partial Regex OnsitePattern();

        [GeneratedRegex(@"\([^)]*\)", RegexOptions.CultureInvariant)]
        private static partial Regex Parenthetical();
    }
}
