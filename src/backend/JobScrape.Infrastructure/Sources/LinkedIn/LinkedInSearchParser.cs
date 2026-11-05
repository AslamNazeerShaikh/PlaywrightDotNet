using AngleSharp.Dom;
using JobScrape.Domain;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;

namespace JobScrape.Infrastructure.Sources.LinkedIn
{
    /// <summary>
    /// Parses a LinkedIn guest results page (docs/08-site-playbooks.md §4.4).
    /// Each result is an <c>li</c> carrying a <c>/jobs/view/&lt;id&gt;</c>
    /// link; <c>data-testid</c> and semantic selectors are preferred because
    /// guest markup changes frequently. Offline and pure: the browser pass
    /// marks computed-invisible cards with
    /// <c>data-jobscrape-hidden="true"</c> before handing over the HTML.
    /// </summary>
    public static class LinkedInSearchParser
    {
        /// <summary>
        /// Result card selectors, most specific first.
        /// </summary>
        private const string CardSelector =
            "li.jobs-search-results__list-item, div.job-card-container";

        /// <summary>
        /// Fallback cards used only when the primary classes are absent (low confidence).
        /// </summary>
        private const string FallbackCardSelector = "li:has(a[href*='/jobs/view/'])";

        /// <summary>
        /// Parses one results page. Never throws for any markup: a card
        /// without a visible LinkedIn job link, title, or numeric id is
        /// dropped and counted; hidden and login-wall cards are skipped;
        /// unparseable optional fields stay null with their raw text kept.
        /// </summary>
        /// <param name="html">The page HTML. Must not be null.</param>
        /// <param name="pageUrl">The page URL, for resolving relative links. Must not be null.</param>
        /// <param name="now">The run time, for relative posted dates.</param>
        /// <returns>The parsed page.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="html"/> or <paramref name="pageUrl"/> is null.</exception>
        public static LinkedInSearchPage Parse(string html, string pageUrl, DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull(html);
            ArgumentNullException.ThrowIfNull(pageUrl);

            using var document = LinkedInHtml.Parse(html);
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
                if (LinkedInHtml.IsHidden(card))
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

            return new LinkedInSearchPage(
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
            // The first visible anchor that is a real LinkedIn job link wins;
            // a trap link (off-host, javascript:, hidden) is never followed.
            IElement? anchor = null;
            string? url = null;
            string? idFromUrl = null;
            foreach (
                var candidate in card.QuerySelectorAll(
                    "a.job-card-container__link, a[href*='/jobs/view/']"
                )
            )
            {
                if (
                    !LinkedInHtml.IsHidden(candidate)
                    && LinkedInFields.TryJobUrl(
                        candidate.GetAttribute("href"),
                        pageUrl,
                        out var canonical,
                        out var id
                    )
                )
                {
                    (anchor, url, idFromUrl) = (candidate, canonical, id);
                    break;
                }
            }

            if (anchor is null || url is null)
            {
                return null;
            }

            var jobId = LinkedInFields.NumericId(card.GetAttribute("data-job-id")) ?? idFromUrl;
            var title = LinkedInFields.Clean(
                LinkedInHtml.TextOf(anchor)
                    ?? anchor.GetAttribute("title")
                    ?? anchor.GetAttribute("aria-label"),
                JobTitle.MaxLength
            );
            if (jobId is null || title is null)
            {
                return null;
            }

            try
            {
                return BuildSnapshot(card, pageUrl, jobId, url, title, now, lowConfidence);
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
        /// <param name="id">The external id.</param>
        /// <param name="url">The canonical job URL.</param>
        /// <param name="title">The cleaned title.</param>
        /// <param name="now">The run time.</param>
        /// <param name="lowConfidence">Whether fallback selectors were used.</param>
        /// <returns>The snapshot.</returns>
        private static JobSnapshot BuildSnapshot(
            IElement card,
            string pageUrl,
            string id,
            string url,
            string title,
            DateTimeOffset now,
            bool lowConfidence
        )
        {
            var company = LinkedInHtml.First(
                card,
                "span.job-card-container__company-name",
                "[class*='artdeco-entity-lockup__subtitle']",
                "a[class*='company']"
            );
            var location = LinkedInHtml.TextOf(
                LinkedInHtml.First(
                    card,
                    "span.job-card-container__metadata-item",
                    "li.job-card-container__metadata-item",
                    "[class*='job-card-container__metadata']"
                )
            );
            var postedRaw = LinkedInFields.Clean(
                LinkedInHtml.TextOf(
                    LinkedInHtml.First(card, "time", "[class*='job-card-list__posted-date']")
                ) ?? LinkedInHtml.First(card, "time")?.GetAttribute("datetime")
            );

            return new JobSnapshot
            {
                Source = SourceKey.LinkedIn,
                ExternalJobId = id,
                Url = url,
                Title = title,
                Company = LinkedInFields.Clean(
                    LinkedInHtml.TextOf(company) ?? company?.GetAttribute("title"),
                    CompanyName.MaxLength
                ),
                CompanyUrl = LinkedInFields.CompanyPageUrl(
                    company?.GetAttribute("href")
                        ?? company?.QuerySelector("a")?.GetAttribute("href"),
                    pageUrl
                ),
                Location = LinkedInFields.Location(location),
                PostedRaw = postedRaw,
                PostedAt = LinkedInFields.Posted(postedRaw, now),
                LowConfidence = lowConfidence,
            };
        }

        /// <summary>
        /// The result-count text shown above the guest list.
        /// </summary>
        /// <param name="document">The page.</param>
        /// <returns>The collapsed text, or null.</returns>
        private static string? ResultCount(IDocument document)
        {
            return LinkedInHtml.TextOf(
                LinkedInHtml.First(
                    document,
                    ".jobs-search-results-list__subtitle",
                    "[data-testid='jobs-search-results-count']",
                    ".jobs-search-results-list__title"
                )
            );
        }

        /// <summary>
        /// Whether a visible pagination link to a later <c>start</c> offset
        /// exists.
        /// </summary>
        /// <param name="document">The page.</param>
        /// <returns>True when the adapter may advance.</returns>
        private static bool HasNext(IDocument document)
        {
            return document
                .QuerySelectorAll("a[href]")
                .Any(link => !LinkedInHtml.IsHidden(link) && IsNextLink(link.GetAttribute("href")));
        }

        /// <summary>
        /// Whether a link advances the guest search: the same search path
        /// with a <c>start</c> offset.
        /// </summary>
        /// <param name="href">The raw link.</param>
        /// <returns>True for a usable next-page link.</returns>
        private static bool IsNextLink(string? href)
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

            return trimmed.Contains("/jobs/search", StringComparison.OrdinalIgnoreCase)
                && trimmed.Contains("start=", StringComparison.OrdinalIgnoreCase);
        }
    }
}
