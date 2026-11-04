using System.Net;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using JobScrape.Domain;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using JobScrape.Infrastructure.Sources;

namespace JobScrape.Infrastructure.Sources.Indeed
{
    /// <summary>
    /// Parses an Indeed job detail page (docs/08-site-playbooks.md §5.3).
    /// The schema.org <c>JobPosting</c> JSON-LD block is read first; the
    /// <c>div#jobDescriptionText</c> description and header DOM fill
    /// whatever it lacks. Partial success is normal: missing fields stay
    /// null and are never faked.
    /// </summary>
    public static partial class IndeedDetailParser
    {
        /// <summary>
        /// Parses one detail page.
        /// </summary>
        /// <param name="html">The page HTML. Must not be null.</param>
        /// <param name="url">
        /// The page URL (final URL after redirects). Must be an Indeed
        /// <c>/viewjob?jk=</c> URL; otherwise the page's
        /// <c>link[rel=canonical]</c> is used. Tracking query parameters are dropped.
        /// </param>
        /// <param name="now">The run time, for a relative posted date when JSON-LD has none.</param>
        /// <returns>
        /// The snapshot, or null when the page yields no usable identity
        /// (Indeed job URL and key) or title.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="html"/> or <paramref name="url"/> is null.</exception>
        public static JobSnapshot? Parse(string html, string url, DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull(html);
            ArgumentNullException.ThrowIfNull(url);

            using var document = IndeedHtml.Parse(html);
            if (
                !IndeedFields.TryJobUrl(url, null, out var canonical, out var keyFromUrl)
                && !IndeedFields.TryJobUrl(
                    document.QuerySelector("link[rel='canonical']")?.GetAttribute("href"),
                    url,
                    out canonical,
                    out keyFromUrl
                )
            )
            {
                return null;
            }

            var jsonLd = IndeedJsonLd.Find(document);
            var key = keyFromUrl;
            var title = IndeedFields.Clean(
                jsonLd?.Title
                    ?? IndeedHtml.TextOf(
                        // Never a bare h1: a block page's "Access Denied"
                        // heading must not become a job title.
                        IndeedHtml.First(
                            document,
                            "h1.jobsearch-JobInfoHeader-title",
                            "[data-testid='jobsearch-JobInfoHeader-title']",
                            "h2.jobsearch-JobInfoHeader-title"
                        )
                    ),
                JobTitle.MaxLength
            );
            if (key is null || title is null)
            {
                return null;
            }

            try
            {
                return Build(document, url, jsonLd, key, canonical, title, now);
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
        /// <param name="pageUrl">The page URL, for resolving relative links.</param>
        /// <param name="jsonLd">The JSON-LD posting, if any.</param>
        /// <param name="key">The job key.</param>
        /// <param name="url">The canonical job URL.</param>
        /// <param name="title">The cleaned title.</param>
        /// <param name="now">The run time.</param>
        /// <returns>The snapshot.</returns>
        private static JobSnapshot Build(
            IDocument document,
            string pageUrl,
            IndeedJsonLd? jsonLd,
            string key,
            string url,
            string title,
            DateTimeOffset now
        )
        {
            var company = IndeedHtml.First(
                document,
                "[data-testid='inlineHeader-companyName']",
                "div[data-company-name]",
                ".jobsearch-CompanyInfoContainer a"
            );
            var location = IndeedHtml.TextOf(
                IndeedHtml.First(
                    document,
                    "[data-testid='job-location']",
                    "[data-testid='inlineHeader-companyLocation']",
                    "div.jobsearch-JobInfoHeader-subtitle div:last-child"
                )
            );
            var salaryRaw = IndeedFields.Clean(
                IndeedHtml.TextOf(
                    IndeedHtml.First(
                        document,
                        "[data-testid='jobsearch-JobMetadataHeader-salary']",
                        "#salaryInfoAndJobType"
                    )
                )
            );
            var postedRaw = IndeedFields.Clean(
                IndeedHtml.TextOf(
                    IndeedHtml.First(document, "span.jobsearch-HiringInsights-entry--text")
                )
            );

            return new JobSnapshot
            {
                Source = SourceKey.Indeed,
                ExternalJobId = key,
                Url = url,
                Title = title,
                Company = IndeedFields.Clean(
                    jsonLd?.Company ?? IndeedHtml.TextOf(company),
                    CompanyName.MaxLength
                ),
                CompanyUrl = IndeedFields.CompanyPageUrl(
                    company?.GetAttribute("href")
                        ?? company?.QuerySelector("a")?.GetAttribute("href"),
                    pageUrl
                ),
                Location = Location(document, jsonLd, location),
                SalaryRaw = string.IsNullOrWhiteSpace(salaryRaw) ? null : salaryRaw,
                Salary = IndeedFields.Salary(salaryRaw),
                PostedRaw = postedRaw,
                PostedAt =
                    IndeedFields.IsoDate(jsonLd?.DatePosted) ?? IndeedFields.Posted(postedRaw, now),
                Description = Description(document, jsonLd),
                Skills = IndeedFields.Skills(
                    jsonLd?.Skills is { Count: > 0 } skills
                        ? skills
                        : document
                            .QuerySelectorAll(
                                "[data-testid='jobsearch-JobMetadataHeader-item'] span"
                            )
                            .Select(chip => chip.TextContent)
                ),
                ApplyUrl = ApplyUrl(document, pageUrl),
                LowConfidence = jsonLd is null,
            };
        }

        /// <summary>
        /// The location as the header shows it, else the JSON-LD localities.
        /// </summary>
        /// <param name="document">The page.</param>
        /// <param name="jsonLd">The JSON-LD posting, if any.</param>
        /// <param name="shown">The header location text, if any.</param>
        /// <returns>The location, or null.</returns>
        private static JobLocation? Location(
            IDocument document,
            IndeedJsonLd? jsonLd,
            string? shown
        )
        {
            var text =
                shown
                ?? string.Join(
                    ", ",
                    (jsonLd?.Localities ?? [])
                        .Select(locality => IndeedFields.Clean(locality))
                        .Where(locality => locality is not null)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                );
            var location = IndeedFields.Location(string.IsNullOrWhiteSpace(text) ? null : text);
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
        /// The readable description: JSON-LD first, then the
        /// <c>div#jobDescriptionText</c> container. Capped to the domain limit.
        /// </summary>
        /// <param name="document">The page.</param>
        /// <param name="jsonLd">The JSON-LD posting, if any.</param>
        /// <returns>The description, or null.</returns>
        private static string? Description(IDocument document, IndeedJsonLd? jsonLd)
        {
            if (!string.IsNullOrWhiteSpace(jsonLd?.DescriptionHtml))
            {
                var html = jsonLd.DescriptionHtml;

                // Some generators entity-encode the markup itself.
                if (!html.Contains('<', StringComparison.Ordinal) && EncodedTag().IsMatch(html))
                {
                    html = WebUtility.HtmlDecode(html);
                }

                using var fragment = IndeedHtml.Parse(html);
                if (
                    fragment.Body is { } body
                    && IndeedFields.LongText(
                        HtmlPlainText.ToPlainText(body, static line => IndeedFields.Clean(line))
                    )
                        is { } text
                )
                {
                    return text;
                }
            }

            var container = IndeedHtml.First(
                document,
                "#jobDescriptionText",
                ".jobsearch-jobDescriptionText",
                "[data-testid='jobsearch-JobDescription']"
            );
            return container is null
                ? null
                : IndeedFields.LongText(
                    HtmlPlainText.ToPlainText(container, static line => IndeedFields.Clean(line))
                );
        }

        /// <summary>
        /// The first visible apply control pointing at an Indeed apply URL
        /// (the <c>applyButtonLinkContainer</c>); external ATS links are read
        /// from its href when off-host. Indeed's modal apply button has no
        /// href and never matches.
        /// </summary>
        /// <param name="document">The page.</param>
        /// <param name="pageUrl">The page URL, for resolving relative links.</param>
        /// <returns>The link, or null.</returns>
        private static string? ApplyUrl(IDocument document, string pageUrl)
        {
            foreach (var link in document.QuerySelectorAll("#applyButtonLinkContainer a[href]"))
            {
                if (
                    !IndeedHtml.IsHidden(link)
                    && IndeedFields.ExternalApplyUrl(link.GetAttribute("href"), pageUrl)
                        is { } applyUrl
                )
                {
                    return applyUrl;
                }
            }

            return null;
        }

        [GeneratedRegex(@"&lt;/?[a-z]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex EncodedTag();
    }
}
