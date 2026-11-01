using System.Net;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using JobScrape.Domain;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;

namespace JobScrape.Infrastructure.Sources.Naukri
{
    /// <summary>
    /// Parses a Naukri job detail page (selector version 1, verified against
    /// live markup on 2026-11-01). The schema.org <c>JobPosting</c> JSON-LD
    /// block is read first; the visible header, description, and key-skill
    /// DOM fill whatever it lacks. Partial success is normal: missing fields
    /// stay null and are never faked.
    /// </summary>
    public static partial class NaukriDetailParser
    {
        /// <summary>
        /// Parses one detail page.
        /// </summary>
        /// <param name="html">The page HTML. Must not be null.</param>
        /// <param name="url">
        /// The page URL (final URL after redirects). Must be a Naukri
        /// <c>/job-listings-</c> URL; otherwise the page's
        /// <c>link[rel=canonical]</c> is used. Tracking query parameters are dropped.
        /// </param>
        /// <param name="now">The run time, for a relative posted date when JSON-LD has none.</param>
        /// <returns>
        /// The snapshot, or null when the page yields no usable identity
        /// (Naukri job URL and numeric id) or title. The id comes from JSON-LD
        /// <c>identifier.value</c> first, so a redirect to another job
        /// produces that job's id and the handler rejects the mismatch.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="html"/> or <paramref name="url"/> is null.</exception>
        public static JobSnapshot? Parse(string html, string url, DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull(html);
            ArgumentNullException.ThrowIfNull(url);

            using var document = NaukriHtml.Parse(html);
            if (
                !NaukriFields.TryJobUrl(url, out var canonical, out var idFromUrl)
                && !NaukriFields.TryJobUrl(
                    document.QuerySelector("link[rel='canonical']")?.GetAttribute("href"),
                    out canonical,
                    out idFromUrl
                )
            )
            {
                return null;
            }

            var jsonLd = NaukriJsonLd.Find(document);
            var id = NaukriFields.NumericId(jsonLd?.Identifier) ?? idFromUrl;
            var title = NaukriFields.Clean(
                jsonLd?.Title
                    ?? NaukriHtml.TextOf(
                        // Never a bare h1: a block page's "Access Denied"
                        // heading must not become a job title.
                        NaukriHtml.First(
                            document,
                            "h1[class*='jd-header-title']",
                            "[class*='jd-header-title']"
                        )
                    ),
                JobTitle.MaxLength
            );
            if (id is null || title is null)
            {
                return null;
            }

            try
            {
                return Build(document, jsonLd, id, canonical, title, now);
            }
            catch (DomainException)
            {
                // Defensive: field helpers pre-validate, but scraped input
                // must never escape as an exception.
                return null;
            }
        }

        /// <summary>
        /// Reads the optional detail fields around a validated identity.
        /// </summary>
        /// <param name="document">The page.</param>
        /// <param name="jsonLd">The JSON-LD posting, if any.</param>
        /// <param name="id">The external id.</param>
        /// <param name="url">The canonical job URL.</param>
        /// <param name="title">The cleaned title.</param>
        /// <param name="now">The run time.</param>
        /// <returns>The snapshot.</returns>
        private static JobSnapshot Build(
            IDocument document,
            NaukriJsonLd? jsonLd,
            string id,
            string url,
            string title,
            DateTimeOffset now
        )
        {
            var companyLink = document.QuerySelector("[class*='jd-header-comp-name'] a");
            var experienceRaw = NaukriHtml.TextOf(
                NaukriHtml.First(document, "[class*='jhc__exp__'] span", "[class*='jhc__exp'] span")
            );
            var (minYears, maxYears) = NaukriFields.Experience(experienceRaw);
            if (minYears is null && maxYears is null)
            {
                minYears = NaukriFields.ExperienceFromMonths(jsonLd?.MonthsOfExperience);
            }

            var salaryRaw =
                NaukriHtml.TextOf(document.QuerySelector("[class*='jhc__salary'] span"))
                ?? NaukriFields.Clean(jsonLd?.SalaryText);
            var postedRaw = PostedText(document);

            return new JobSnapshot
            {
                Source = SourceKey.Naukri,
                ExternalJobId = id,
                Url = url,
                Title = title,
                Company = NaukriFields.Clean(
                    jsonLd?.Company ?? NaukriHtml.TextOf(companyLink),
                    CompanyName.MaxLength
                ),
                CompanyUrl = NaukriFields.CompanyPageUrl(companyLink?.GetAttribute("href")),
                Location = Location(document, jsonLd),
                ExperienceRaw = experienceRaw,
                ExperienceMinYears = minYears,
                ExperienceMaxYears = maxYears,
                SalaryRaw = salaryRaw,
                Salary = NaukriFields.Salary(salaryRaw) ?? JsonLdSalary(jsonLd),
                PostedRaw = postedRaw,
                PostedAt =
                    NaukriFields.IsoDate(jsonLd?.DatePosted) ?? NaukriFields.Posted(postedRaw, now),
                Description = Description(document, jsonLd),
                Skills = NaukriFields.Skills(
                    jsonLd?.Skills is { Count: > 0 } skills
                        ? skills
                        : document
                            .QuerySelectorAll("[class*='key-skill'] a")
                            .Select(chip => chip.TextContent)
                ),
                ApplyUrl = ApplyUrl(document),
                LowConfidence = jsonLd is null,
            };
        }

        /// <summary>
        /// The location as the header shows it (it carries the work-mode
        /// prefix the search card showed), else the JSON-LD localities.
        /// </summary>
        /// <param name="document">The page.</param>
        /// <param name="jsonLd">The JSON-LD posting, if any.</param>
        /// <returns>The location, or null.</returns>
        private static JobLocation? Location(IDocument document, NaukriJsonLd? jsonLd)
        {
            var shown =
                NaukriHtml.TextOf(document.QuerySelector("[class*='jhc__loc']"))
                ?? string.Join(
                    ", ",
                    (jsonLd?.Localities ?? [])
                        .Select(locality => NaukriFields.Clean(locality))
                        .Where(locality => locality is not null and not "-")
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                );
            var location = NaukriFields.Location(shown);
            var telecommute = string.Equals(
                jsonLd?.LocationType,
                "TELECOMMUTE",
                StringComparison.OrdinalIgnoreCase
            );
            return location is { WorkMode: WorkMode.Unknown } && telecommute
                ? new JobLocation(location.Raw, location.City, null, null, WorkMode.Remote)
                : location;
        }

        /// <summary>
        /// The header's <c>Posted:</c> stat text, e.g. <c>6 days ago</c>.
        /// </summary>
        /// <param name="document">The page.</param>
        /// <returns>The text, or null.</returns>
        private static string? PostedText(IDocument document)
        {
            foreach (var stat in document.QuerySelectorAll("[class*='jhc__stat']"))
            {
                var label = NaukriHtml.TextOf(stat.QuerySelector("label"));
                if (label?.StartsWith("Posted", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return NaukriHtml.TextOf(stat.QuerySelector("span"));
                }
            }

            return null;
        }

        /// <summary>
        /// A salary stated as numbers in JSON-LD, when complete and consistent.
        /// </summary>
        /// <param name="jsonLd">The JSON-LD posting, if any.</param>
        /// <returns>The salary, or null.</returns>
        private static SalaryRange? JsonLdSalary(NaukriJsonLd? jsonLd)
        {
            if (jsonLd?.SalaryMin is not { } min || jsonLd.SalaryMax is not { } max)
            {
                return null;
            }

            var period = jsonLd.SalaryUnit?.Trim().ToUpperInvariant() switch
            {
                "YEAR" or "P.A." or "PA" or "PER ANNUM" or "YEARLY" => SalaryPeriod.Year,
                "MONTH" or "P.M." or "PM" or "MONTHLY" => SalaryPeriod.Month,
                _ => (SalaryPeriod?)null,
            };
            var currency = jsonLd.SalaryCurrency?.Trim().ToUpperInvariant();
            return
                period is null
                || currency is not { Length: 3 }
                || !currency.All(char.IsAsciiLetterUpper)
                || min < 0
                || min > max
                || min == 0 && max == 0
                ? null
                : new SalaryRange(min, max, currency, period.Value);
        }

        /// <summary>
        /// The readable description: JSON-LD first, then the description
        /// container. Capped to the domain limit.
        /// </summary>
        /// <param name="document">The page.</param>
        /// <param name="jsonLd">The JSON-LD posting, if any.</param>
        /// <returns>The description, or null.</returns>
        private static string? Description(IDocument document, NaukriJsonLd? jsonLd)
        {
            if (!string.IsNullOrWhiteSpace(jsonLd?.DescriptionHtml))
            {
                var html = jsonLd.DescriptionHtml;

                // Some generators entity-encode the markup itself.
                if (!html.Contains('<', StringComparison.Ordinal) && EncodedTag().IsMatch(html))
                {
                    html = WebUtility.HtmlDecode(html);
                }

                using var fragment = NaukriHtml.Parse(html);
                if (
                    fragment.Body is { } body
                    && NaukriFields.LongText(NaukriHtml.ToPlainText(body)) is { } text
                )
                {
                    return text;
                }
            }

            var container = NaukriHtml.First(
                document,
                "[class*='JDC__dang-inner-html']",
                "section[class*='job-desc']",
                "div[class*='job-desc']",
                ".dtl-section"
            );
            return container is null
                ? null
                : NaukriFields.LongText(NaukriHtml.ToPlainText(container));
        }

        /// <summary>
        /// The first visible external apply link: an anchor whose text,
        /// <c>aria-label</c>, or <c>title</c> mentions applying or the
        /// company/career site, pointing to an <c>https</c> host outside
        /// Naukri. Naukri's own apply control is a button, so it never matches.
        /// </summary>
        /// <param name="document">The page.</param>
        /// <returns>The link, or null.</returns>
        private static string? ApplyUrl(IDocument document)
        {
            foreach (var link in document.QuerySelectorAll("a[href]"))
            {
                var label = string.Join(
                    ' ',
                    link.TextContent,
                    link.GetAttribute("aria-label"),
                    link.GetAttribute("title")
                );
                if (
                    ApplyLabel().IsMatch(label)
                    && !NaukriHtml.IsHidden(link)
                    && NaukriFields.ExternalApplyUrl(link.GetAttribute("href")) is { } applyUrl
                )
                {
                    return applyUrl;
                }
            }

            return null;
        }

        [GeneratedRegex(
            @"\bapply\b|\b(?:company|career|careers)\s+(?:site|website|page)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        )]
        private static partial Regex ApplyLabel();

        [GeneratedRegex(@"&lt;/?[a-z]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex EncodedTag();
    }
}
