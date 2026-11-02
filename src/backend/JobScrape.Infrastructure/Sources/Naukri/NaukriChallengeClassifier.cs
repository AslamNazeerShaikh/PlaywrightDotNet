using System.Globalization;
using System.Text.RegularExpressions;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;

namespace JobScrape.Infrastructure.Sources.Naukri
{
    /// <summary>
    /// Classifies a loaded Naukri page as clean or a wall
    /// (docs/08-site-playbooks.md §3.7). Rules run in this order; the first
    /// match wins:
    /// <list type="number">
    /// <item>Akamai host (<c>errors.edgesuite.net</c>, <c>*.akamai*</c>) → AccessDenied, 0.99.</item>
    /// <item>HTTP 429 → RateLimited, 0.95 (no Retry-After is read here).</item>
    /// <item>URL under <c>/nlogin</c> or a <c>login.</c> host → LoginRequired, 0.95.</item>
    /// <item>Title names a CAPTCHA / human check → Captcha, 0.95; "Access Denied" → AccessDenied, 0.95;
    /// "Pardon the interruption" / "unusual traffic" → SoftChallenge, 0.9.</item>
    /// <item>Only when the page has <b>no</b> job cards, body phrases: human-verification
    /// wording → Captcha, 0.85; interruption/unusual-traffic wording → SoftChallenge, 0.8;
    /// "Access Denied" or an edgesuite reference → AccessDenied, 0.85.</item>
    /// <item>HTTP 403 → AccessDenied, 0.9; HTTP 401 → LoginRequired, 0.8; HTTP 5xx → TransientError, 0.8.</item>
    /// <item>No job cards and login-wall wording ("login to continue", …) → LoginRequired, 0.7.</item>
    /// <item>Otherwise <see cref="ChallengeAssessment.Clear"/>.</item>
    /// </list>
    /// Body text is ignored once job cards are present, so a usable results
    /// page whose footer mentions a CAPTCHA, "Access Denied", or "Login"
    /// stays clean; a bare "captcha" mention (e.g. a "protected by
    /// reCAPTCHA" notice) never matches on its own. "Pardon the interruption"
    /// and "unusual traffic" are SoftChallenge because they are interstitial
    /// bot checks a person can pass in the headed browser, not a hard block.
    /// Evidence names only the rule and the matched phrase, never page content.
    /// </summary>
    public static partial class NaukriChallengeClassifier
    {
        /// <summary>
        /// Classifies a page.
        /// </summary>
        /// <param name="finalUrl">The URL after redirects. Must not be null.</param>
        /// <param name="status">The main-document HTTP status, if known.</param>
        /// <param name="title">The document title. Must not be null.</param>
        /// <param name="bodyText">The visible body text. Must not be null.</param>
        /// <param name="hasJobCards">Whether the page shows at least one job card.</param>
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

            var host = finalUrl.IsAbsoluteUri ? finalUrl.IdnHost : string.Empty;
            var path = finalUrl.IsAbsoluteUri ? finalUrl.AbsolutePath : finalUrl.OriginalString;

            if (
                host.EndsWith("edgesuite.net", StringComparison.OrdinalIgnoreCase)
                || host.Contains("akamai", StringComparison.OrdinalIgnoreCase)
            )
            {
                return new ChallengeAssessment(ChallengeKind.AccessDenied, 0.99, $"host={host}");
            }

            if (status == 429)
            {
                return new ChallengeAssessment(ChallengeKind.RateLimited, 0.95, "status=429");
            }

            if (
                path.StartsWith("/nlogin", StringComparison.OrdinalIgnoreCase)
                || host.StartsWith("login.", StringComparison.OrdinalIgnoreCase)
            )
            {
                return new ChallengeAssessment(ChallengeKind.LoginRequired, 0.95, "url=login");
            }

            if (MatchPhrases(title, "title", 0.95, 0.95, 0.9) is { } byTitle)
            {
                return byTitle;
            }

            if (!hasJobCards && MatchPhrases(bodyText, "body", 0.85, 0.85, 0.8) is { } byBody)
            {
                return byBody;
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

            if (!hasJobCards && LoginWall().Match(bodyText) is { Success: true } login)
            {
                return new ChallengeAssessment(
                    ChallengeKind.LoginRequired,
                    0.7,
                    Evidence("body", login)
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
        /// <param name="soft">Confidence for a soft-challenge match.</param>
        /// <returns>The assessment, or null when nothing matched.</returns>
        private static ChallengeAssessment? MatchPhrases(
            string text,
            string where,
            double captcha,
            double denied,
            double soft
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

            if (SoftChallenge().Match(text) is { Success: true } softMatch)
            {
                return new ChallengeAssessment(
                    ChallengeKind.SoftChallenge,
                    soft,
                    Evidence(where, softMatch)
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
            @"pardon\s+(?:the|our)\s+interruption|unusual\s+traffic",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        )]
        private static partial Regex SoftChallenge();

        [GeneratedRegex(
            @"access\s+denied|errors\.edgesuite\.net",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        )]
        private static partial Regex AccessDenied();

        [GeneratedRegex(
            @"(?:log\s?in|sign\s?in)\s+to\s+(?:continue|view|proceed)|please\s+(?:log\s?in|sign\s?in)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        )]
        private static partial Regex LoginWall();
    }
}
