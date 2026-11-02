using AngleSharp.Dom;
using JobScrape.Domain;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;

namespace JobScrape.Infrastructure.Sources.Naukri
{
    /// <summary>
    /// Parses a Naukri results page (selector version 1, verified against
    /// live markup on 2026-11-01; docs/08-site-playbooks.md §3.4). Each
    /// organic result is a <c>div.srp-jobtuple-wrapper[data-job-id]</c>;
    /// promoted widgets (<c>div.srp-job-promotion</c>, the urgent-hiring
    /// tuple) repeat listed jobs and are never parsed. Offline and pure: the
    /// browser pass marks computed-invisible cards with
    /// <c>data-jobscrape-hidden="true"</c> before handing over the HTML.
    /// </summary>
    public static class NaukriSearchParser
    {
        /// <summary>
        /// Organic result cards (primary selector).
        /// </summary>
        private const string CardSelector = "div.srp-jobtuple-wrapper";

        /// <summary>
        /// Fallback cards: id-carrying elements inside the semantic results
        /// list, used only when the primary class is absent (low confidence).
        /// </summary>
        private const string FallbackCardSelector =
            "[aria-label='Job search results'] [data-job-id]";

        /// <summary>
        /// Containers of promoted/injected widgets, excluded from the fallback.
        /// </summary>
        private const string WidgetSelector =
            ".naukri-wdgt, .srp-job-promotion, #urgent-hiring-widget, [class*='job-promotion']";

        /// <summary>
        /// Parses one results page. Never throws for any markup: a card
        /// without a visible Naukri job link, title, or numeric id is dropped
        /// and counted; hidden and honeypot cards are skipped; unparseable
        /// optional fields stay null with their raw text kept.
        /// </summary>
        /// <param name="html">The page HTML. Must not be null.</param>
        /// <param name="now">The run time, for relative posted dates.</param>
        /// <returns>The parsed page.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="html"/> is null.</exception>
        public static NaukriSearchPage Parse(string html, DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull(html);

            using var document = NaukriHtml.Parse(html);
            var cards = document.QuerySelectorAll(CardSelector).ToList();
            var lowConfidence = cards.Count == 0;
            if (lowConfidence)
            {
                cards = document
                    .QuerySelectorAll(FallbackCardSelector)
                    .Where(card => card.Closest(WidgetSelector) is null)
                    .ToList();
            }

            var jobs = new List<JobSnapshot>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var dropped = 0;
            foreach (var card in cards)
            {
                if (NaukriHtml.IsHidden(card))
                {
                    continue;
                }

                var snapshot = ParseCard(card, now, lowConfidence);
                if (snapshot is null)
                {
                    dropped++;
                }
                else if (seen.Add(snapshot.ExternalJobId))
                {
                    jobs.Add(snapshot);
                }
            }

            return new NaukriSearchPage(
                jobs,
                ResultRange(document),
                jobs.Count > 0 ? jobs[0].ExternalJobId : null,
                HasNext(document),
                dropped
            );
        }

        /// <summary>
        /// Extracts one card, or null when it lacks a critical field.
        /// </summary>
        /// <param name="card">The card element.</param>
        /// <param name="now">The run time.</param>
        /// <param name="lowConfidence">Whether the card came from the fallback selector.</param>
        /// <returns>The snapshot, or null for a malformed card.</returns>
        private static JobSnapshot? ParseCard(IElement card, DateTimeOffset now, bool lowConfidence)
        {
            // The first visible anchor that is a real Naukri job link wins; a
            // trap link (off-host, javascript:, hidden) is never followed.
            IElement? anchor = null;
            string? url = null;
            string? idFromUrl = null;
            foreach (
                var candidate in card.QuerySelectorAll(
                    "a.title, h2 a, a[class*='title'], a[href*='/job-listings-']"
                )
            )
            {
                if (
                    !NaukriHtml.IsHidden(candidate)
                    && NaukriFields.TryJobUrl(
                        candidate.GetAttribute("href"),
                        out var canonical,
                        out var suffix
                    )
                )
                {
                    (anchor, url, idFromUrl) = (candidate, canonical, suffix);
                    break;
                }
            }

            if (anchor is null || url is null)
            {
                return null;
            }

            var id = NaukriFields.NumericId(card.GetAttribute("data-job-id")) ?? idFromUrl;
            var title = NaukriFields.Clean(
                NaukriHtml.TextOf(anchor) ?? anchor.GetAttribute("title"),
                JobTitle.MaxLength
            );
            if (id is null || title is null)
            {
                return null;
            }

            try
            {
                return BuildSnapshot(card, anchor, id, url, title, now, lowConfidence);
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
        /// <param name="anchor">The card's job link.</param>
        /// <param name="id">The external id.</param>
        /// <param name="url">The canonical job URL.</param>
        /// <param name="title">The cleaned title.</param>
        /// <param name="now">The run time.</param>
        /// <param name="lowConfidence">Whether fallback selectors were used.</param>
        /// <returns>The snapshot.</returns>
        private static JobSnapshot BuildSnapshot(
            IElement card,
            IElement anchor,
            string id,
            string url,
            string title,
            DateTimeOffset now,
            bool lowConfidence
        )
        {
            var company = NaukriHtml.First(card, "a.comp-name", ".comp-name", "a[class*='comp']");
            var experienceRaw = NaukriHtml.TextOf(
                NaukriHtml.First(card, "span.expwdth", ".exp-wrap")
            );
            var (minYears, maxYears) = NaukriFields.Experience(experienceRaw);
            var salaryElement = NaukriHtml.First(card, ".sal-wrap span[title]", ".sal-wrap");
            var salaryRaw = NaukriFields.Clean(
                NaukriHtml.TextOf(salaryElement) ?? salaryElement?.GetAttribute("title")
            );
            var postedRaw = NaukriHtml.TextOf(
                NaukriHtml.First(
                    card,
                    "span.job-post-day",
                    "[class*='job-post']",
                    "[class*='created-date']"
                )
            );
            var skillTags = card.QuerySelectorAll("ul.tags-gt li.tag-li");
            if (skillTags.Length == 0)
            {
                skillTags = card.QuerySelectorAll("li.tag-li, ul.tags-gt li");
            }

            return new JobSnapshot
            {
                Source = SourceKey.Naukri,
                ExternalJobId = id,
                Url = url,
                Title = title,
                Company = NaukriFields.Clean(
                    NaukriHtml.TextOf(company) ?? company?.GetAttribute("title"),
                    CompanyName.MaxLength
                ),
                CompanyUrl = NaukriFields.CompanyPageUrl(company?.GetAttribute("href")),
                Location = NaukriFields.Location(
                    NaukriHtml.TextOf(
                        NaukriHtml.First(card, "span.locWdth", ".loc-wrap", "span[class*='loc']")
                    )
                ),
                ExperienceRaw = experienceRaw,
                ExperienceMinYears = minYears,
                ExperienceMaxYears = maxYears,
                SalaryRaw = salaryRaw,
                Salary = NaukriFields.Salary(salaryRaw),
                PostedRaw = postedRaw,
                PostedAt = NaukriFields.Posted(postedRaw, now),
                Snippet = NaukriFields.LongText(
                    NaukriHtml.TextOf(
                        NaukriHtml.First(card, "span.job-desc", "[class*='job-desc']")
                    )
                ),
                Skills = NaukriFields.Skills(skillTags.Select(tag => tag.TextContent)),
                LowConfidence = lowConfidence || !anchor.ClassList.Contains("title"),
            };
        }

        /// <summary>
        /// The result-range text, e.g. <c>1 - 20 of 131</c>.
        /// </summary>
        /// <param name="document">The page.</param>
        /// <returns>The collapsed text, or null.</returns>
        private static string? ResultRange(IDocument document)
        {
            var element = document.QuerySelector("span[class*='count-string']");
            return NaukriHtml.TextOf(element) ?? NaukriFields.Clean(element?.GetAttribute("title"));
        }

        /// <summary>
        /// Whether a visible, enabled native Next link exists.
        /// </summary>
        /// <param name="document">The page.</param>
        /// <returns>True when the adapter may click Next.</returns>
        private static bool HasNext(IDocument document)
        {
            return document
                .QuerySelectorAll("a")
                .Any(link =>
                    string.Equals(
                        NaukriHtml.TextOf(link),
                        "Next",
                        StringComparison.OrdinalIgnoreCase
                    )
                    && !string.Equals(
                        link.GetAttribute("aria-disabled")?.Trim(),
                        "true",
                        StringComparison.OrdinalIgnoreCase
                    )
                    && !link.HasAttribute("disabled")
                    && !NaukriHtml.IsHidden(link)
                );
        }
    }
}
