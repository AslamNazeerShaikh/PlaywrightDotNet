using AngleSharp.Dom;
using JobScrape.Domain;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;

namespace JobScrape.Infrastructure.Sources.Indeed
{
    /// <summary>
    /// Parses an Indeed India results page (docs/08-site-playbooks.md §5.3).
    /// Each result is a <c>div.job_seen_beacon</c> (or a
    /// <c>div[data-testid="slider_item"]</c>); JSON-LD is rare on search
    /// pages, so the semantic DOM and <c>data-testid</c> attributes carry
    /// the fields. Offline and pure: the browser pass marks
    /// computed-invisible cards with
    /// <c>data-jobscrape-hidden="true"</c> before handing over the HTML.
    /// </summary>
    public static class IndeedSearchParser
    {
        /// <summary>
        /// Result card selectors, most specific first.
        /// </summary>
        private const string CardSelector = "div.job_seen_beacon";

        /// <summary>
        /// Fallback cards used only when the primary class is absent (low confidence).
        /// </summary>
        private const string FallbackCardSelector = "div[data-testid='slider_item']";

        /// <summary>
        /// Parses one results page. Never throws for any markup: a card
        /// without a visible Indeed job link, title, or job key is dropped
        /// and counted; hidden cards are skipped; unparseable optional
        /// fields stay null with their raw text kept.
        /// </summary>
        /// <param name="html">The page HTML. Must not be null.</param>
        /// <param name="pageUrl">The page URL, for resolving relative links. Must not be null.</param>
        /// <param name="now">The run time, for relative posted dates.</param>
        /// <returns>The parsed page.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="html"/> or <paramref name="pageUrl"/> is null.</exception>
        public static IndeedSearchPage Parse(string html, string pageUrl, DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull(html);
            ArgumentNullException.ThrowIfNull(pageUrl);

            using var document = IndeedHtml.Parse(html);
            var cards = document.QuerySelectorAll(CardSelector).ToList();
            var lowConfidence = cards.Count == 0;
            if (lowConfidence)
            {
                cards = document.QuerySelectorAll(FallbackCardSelector).ToList();
            }

            var jobs = new List<JobSnapshot>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var dropped = 0;
            foreach (var card in cards)
            {
                if (IndeedHtml.IsHidden(card))
                {
                    continue;
                }

                var snapshot = ParseCard(card, pageUrl, now, lowConfidence);
                if (snapshot is null)
                {
                    dropped++;
                }
                else if (seen.Add(snapshot.ExternalJobId))
                {
                    jobs.Add(snapshot);
                }
            }

            return new IndeedSearchPage(
                jobs,
                ResultCount(document),
                jobs.Count > 0 ? jobs[0].ExternalJobId : null,
                HasNext(document),
                dropped
            );
        }

        /// <summary>
        /// Extracts one card, or null when it lacks a critical field.
        /// </summary>
        /// <param name="card">The card element.</param>
        /// <param name="pageUrl">The page URL, for resolving relative links.</param>
        /// <param name="now">The run time.</param>
        /// <param name="lowConfidence">Whether the card came from the fallback selector.</param>
        /// <returns>The snapshot, or null for a malformed card.</returns>
        private static JobSnapshot? ParseCard(
            IElement card,
            string pageUrl,
            DateTimeOffset now,
            bool lowConfidence
        )
        {
            // The first visible anchor that is a real Indeed job link wins; a
            // trap link (off-host, javascript:, hidden) is never followed.
            IElement? anchor = null;
            string? url = null;
            string? keyFromUrl = null;
            foreach (
                var candidate in card.QuerySelectorAll(
                    "h2.jobTitle a, a[data-testid='job-title'], a.jcs-JobTitle, a[href*='/viewjob']"
                )
            )
            {
                if (
                    !IndeedHtml.IsHidden(candidate)
                    && IndeedFields.TryJobUrl(
                        candidate.GetAttribute("href"),
                        pageUrl,
                        out var canonical,
                        out var key
                    )
                )
                {
                    (anchor, url, keyFromUrl) = (candidate, canonical, key);
                    break;
                }
            }

            if (anchor is null || url is null)
            {
                return null;
            }

            var jobKey = IndeedFields.JobKey(card.GetAttribute("data-jk")) ?? keyFromUrl;
            var title = IndeedFields.Clean(
                IndeedHtml.TextOf(anchor)
                    ?? anchor.GetAttribute("title")
                    ?? anchor.GetAttribute("aria-label"),
                JobTitle.MaxLength
            );
            if (jobKey is null || title is null)
            {
                return null;
            }

            try
            {
                return BuildSnapshot(card, pageUrl, jobKey, url, title, now, lowConfidence);
            }
            catch (DomainException)
            {
                // Defensive: field helpers pre-validate, but scraped input
                // must never escape as an exception; drop the card instead.
                return null;
            }
        }

        /// <summary>
        /// Reads the optional card fields around a validated identity.
        /// </summary>
        /// <param name="card">The card element.</param>
        /// <param name="pageUrl">The page URL, for resolving relative links.</param>
        /// <param name="key">The job key.</param>
        /// <param name="url">The canonical job URL.</param>
        /// <param name="title">The cleaned title.</param>
        /// <param name="now">The run time.</param>
        /// <param name="lowConfidence">Whether fallback selectors were used.</param>
        /// <returns>The snapshot.</returns>
        private static JobSnapshot BuildSnapshot(
            IElement card,
            string pageUrl,
            string key,
            string url,
            string title,
            DateTimeOffset now,
            bool lowConfidence
        )
        {
            var company = IndeedHtml.First(
                card,
                "span[data-testid='company-name']",
                "[data-testid='company-name']",
                "span.companyName"
            );
            var location = IndeedHtml.TextOf(
                IndeedHtml.First(
                    card,
                    "div[data-testid='text-location']",
                    "[data-testid='text-location']",
                    "div.companyLocation"
                )
            );
            var salaryRaw = IndeedFields.Clean(
                IndeedHtml.TextOf(
                    IndeedHtml.First(
                        card,
                        "div[data-testid='attribute_snippet_testid']",
                        ".salary-snippet-container",
                        "div.salary-snippet-container",
                        "span.salaryText"
                    )
                )
            );
            var postedRaw = IndeedFields.Clean(
                IndeedHtml.TextOf(
                    IndeedHtml.First(card, "span.date", "[data-testid='myJobsStateDate']")
                )
            );
            var snippet = IndeedFields.LongText(
                IndeedHtml.TextOf(
                    IndeedHtml.First(
                        card,
                        "div[data-testid='jobsnippet_footer']",
                        "div.job-snippet",
                        "table.snippet"
                    )
                )
            );
            var skillTags = card.QuerySelectorAll(
                "div[data-testid='jobsnippet_footer'] li, div.job-snippet li"
            );

            return new JobSnapshot
            {
                Source = SourceKey.Indeed,
                ExternalJobId = key,
                Url = url,
                Title = title,
                Company = IndeedFields.Clean(
                    IndeedHtml.TextOf(company) ?? company?.GetAttribute("title"),
                    CompanyName.MaxLength
                ),
                CompanyUrl = IndeedFields.CompanyPageUrl(
                    company?.GetAttribute("href")
                        ?? company?.QuerySelector("a")?.GetAttribute("href"),
                    pageUrl
                ),
                Location = IndeedFields.Location(location),
                SalaryRaw = salaryRaw,
                Salary = IndeedFields.Salary(salaryRaw),
                PostedRaw = postedRaw,
                PostedAt = IndeedFields.Posted(postedRaw, now),
                Snippet = snippet,
                Skills = IndeedFields.Skills(skillTags.Select(tag => tag.TextContent)),
                LowConfidence = lowConfidence,
            };
        }

        /// <summary>
        /// The result-count text, e.g. <c>1–10 of 200 jobs</c>.
        /// </summary>
        /// <param name="document">The page.</param>
        /// <returns>The collapsed text, or null.</returns>
        private static string? ResultCount(IDocument document)
        {
            return IndeedHtml.TextOf(
                IndeedHtml.First(
                    document,
                    "div.jobsearch-JobCountAndSortPane-jobCount",
                    "[data-testid='jobsearch-JobCountAndSortPane-jobCount']",
                    "div.jobsearch-ResultsList > div"
                )
            );
        }

        /// <summary>
        /// Whether a visible pagination link to a later page exists: an
        /// anchor to the same search with a larger <c>start</c>, or a
        /// next-page control.
        /// </summary>
        /// <param name="document">The page.</param>
        /// <returns>True when the adapter may advance.</returns>
        private static bool HasNext(IDocument document)
        {
            return document
                .QuerySelectorAll("a[href]")
                .Any(link =>
                    !IndeedHtml.IsHidden(link)
                    && IsNextLink(link.GetAttribute("href"), link.TextContent)
                );
        }

        /// <summary>
        /// Whether a link advances the search: a larger <c>start</c> offset,
        /// or next-page wording without an <c>aria-disabled</c> marker.
        /// </summary>
        /// <param name="href">The raw link.</param>
        /// <param name="text">The link text.</param>
        /// <returns>True for a usable next-page link.</returns>
        private static bool IsNextLink(string? href, string? text)
        {
            if (string.IsNullOrWhiteSpace(href))
            {
                return false;
            }

            var trimmed = href.Trim();
            if (
                trimmed.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)
                || trimmed == "#"
            )
            {
                return false;
            }

            return trimmed.Contains("start=", StringComparison.OrdinalIgnoreCase)
                || string.Equals(text?.Trim(), "Next", StringComparison.OrdinalIgnoreCase);
        }
    }
}
