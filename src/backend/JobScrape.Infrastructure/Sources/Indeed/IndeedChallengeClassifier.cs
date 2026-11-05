using System.Globalization;
using System.Text.RegularExpressions;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;

namespace JobScrape.Infrastructure.Sources.Indeed
{
    /// <summary>
    /// Classifies a loaded Indeed page as clean or a wall
    /// (docs/08-site-playbooks.md §5.4). Rules run in this order; the first
    /// match wins:
    /// <list type="number">
    /// <item>HTTP 429 → RateLimited, 0.95 (no Retry-After is read here).</item>
    /// <item>A Cloudflare challenge title ("Just a moment", "Checking your
    /// browser") → AccessDenied, 0.9; the same phrases in the body count
    /// only when the page has <b>no</b> job cards. Cloudflare is an access
    /// control, not an interstitial the adapter waits out.</item>
    /// <item>Title names a CAPTCHA / human check → Captcha, 0.95.</item>
    /// <item>Only when the page has <b>no</b> job cards, body phrases:
    /// human-verification wording → Captcha, 0.85; "Access Denied" →
    /// AccessDenied, 0.85.</item>
    /// <item>HTTP 403 → AccessDenied, 0.9; HTTP 401 → LoginRequired, 0.8; HTTP 5xx → TransientError, 0.8.</item>
    /// <item>Otherwise <see cref="ChallengeAssessment.Clear"/>.</item>
    /// </list>
    /// Body text is ignored once job cards are present, so a usable results
    /// page whose footer mentions a check stays clean. Evidence names only
    /// the rule and the matched phrase, never page content.
    /// </summary>
    public static partial class IndeedChallengeClassifier
    {
        /// <summary>
        /// Classifies a page.
        /// </summary>
        /// <param name="finalUrl">The URL after redirects. Must not be null.</param>
        /// <param name="status">The main-document HTTP status, if known.</param>
        /// <param name="title">The document title. Must not be null.</param>
        /// <param name="bodyText">The visible body text. Must not be null.</param>
        /// <param name="hasJobCards">Whether the page shows at least one result card.</param>
        /// <returns>The assessment; <see cref="ChallengeAssessment.Clear"/> for a usable page.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a reference argument is null.</exception>
        public static ChallengeAssessment Classify(
            Uri finalUrl,
            int? status,
            string title,
            string bodyText,
            bool hasJobCards
        )
        {
            ArgumentNullException.ThrowIfNull(finalUrl);
            ArgumentNullException.ThrowIfNull(title);
            ArgumentNullException.ThrowIfNull(bodyText);

            if (status == 429)
            {
                return new ChallengeAssessment(ChallengeKind.RateLimited, 0.95, "status=429");
            }

            // The title is short and authoritative: a Cloudflare challenge
            // title is a wall even if card markup somehow lingers.
            if (Cloudflare().IsMatch(title))
            {
                return new ChallengeAssessment(
                    ChallengeKind.AccessDenied,
                    0.9,
                    "title:'cloudflare'"
                );
            }

            if (MatchPhrases(title, "title", 0.95, 0.95) is { } byTitle)
            {
                return byTitle;
            }

            if (!hasJobCards)
            {
                if (Cloudflare().IsMatch(bodyText))
                {
                    return new ChallengeAssessment(
                        ChallengeKind.AccessDenied,
                        0.9,
                        "body:'cloudflare'"
                    );
                }

                if (MatchPhrases(bodyText, "body", 0.85, 0.85) is { } byBody)
                {
                    return byBody;
                }
            }

            switch (status)
            {
                case 403:
                    return new ChallengeAssessment(ChallengeKind.AccessDenied, 0.9, "status=403");
                case 401:
                    return new ChallengeAssessment(ChallengeKind.LoginRequired, 0.8, "status=401");
                case >= 500 and <= 599:
                    return new ChallengeAssessment(
                        ChallengeKind.TransientError,
                        0.8,
                        string.Create(CultureInfo.InvariantCulture, $"status={status}")
                    );
            }

            return ChallengeAssessment.Clear;
        }

        /// <summary>
        /// Matches the wall phrases in one text, strongest kind first.
        /// </summary>
        /// <param name="text">The title or body text.</param>
        /// <param name="where">Where the text came from, for evidence.</param>
        /// <param name="captcha">Confidence for a CAPTCHA match.</param>
        /// <param name="denied">Confidence for an access-denied match.</param>
        /// <returns>The assessment, or null when nothing matched.</returns>
        private static ChallengeAssessment? MatchPhrases(
            string text,
            string where,
            double captcha,
            double denied
        )
        {
            // A title is short and authoritative, so a bare "captcha" counts
            // there; in body text only explicit verification wording does.
            var captchaMatch =
                where == "title" ? TitleCaptcha().Match(text) : BodyCaptcha().Match(text);
            if (captchaMatch.Success)
            {
                return new ChallengeAssessment(
                    ChallengeKind.Captcha,
                    captcha,
                    Evidence(where, captchaMatch)
                );
            }

            return AccessDenied().Match(text) is { Success: true } deniedMatch
                ? new ChallengeAssessment(
                    ChallengeKind.AccessDenied,
                    denied,
                    Evidence(where, deniedMatch)
                )
                : null;
        }

        /// <summary>
        /// Formats operator-safe evidence: the source and the matched phrase only.
        /// </summary>
        /// <param name="where">Where the phrase was found.</param>
        /// <param name="match">The match.</param>
        /// <returns>Evidence such as <c>title:'access denied'</c>.</returns>
        private static string Evidence(string where, Match match)
        {
            return $"{where}:'{Text.Collapse(match.Value).ToLowerInvariant()}'";
        }

        [GeneratedRegex(
            @"just\s+a\s+moment|checking\s+your\s+browser|additional\s+verification\s+required|verify\s+you\s+are\s+human|cloudflare\s+ray\s+id|attention\s+required\W*cloudflare|security\s+check\s+by\s+cloudflare",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        )]
        private static partial Regex Cloudflare();

        [GeneratedRegex(
            @"captcha|verify\s+(?:that\s+)?you\s+are\s+(?:a\s+)?human|are\s+you\s+a\s+robot",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        )]
        private static partial Regex TitleCaptcha();

        [GeneratedRegex(
            @"verify\s+(?:that\s+)?you\s+are\s+(?:a\s+)?human|i\W?m\s+not\s+a\s+robot|i\s+am\s+not\s+a\s+robot|are\s+you\s+a\s+robot|(?:complete|solve|enter)\s+the\s+captcha|captcha\s+(?:verification|challenge|required)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        )]
        private static partial Regex BodyCaptcha();

        [GeneratedRegex(
            @"access\s+denied",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        )]
        private static partial Regex AccessDenied();
    }
}
