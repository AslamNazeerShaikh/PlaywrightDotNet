using System.Globalization;
using System.Text.RegularExpressions;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;

namespace JobScrape.Infrastructure.Sources.LinkedIn
{
    /// <summary>
    /// Classifies a loaded LinkedIn page as clean or a wall
    /// (docs/08-site-playbooks.md §4.5). Rules run in this order; the first
    /// match wins:
    /// <list type="number">
    /// <item>HTTP 429 or 999 → RateLimited, 0.95: fails closed, no retry is
    /// ever attempted (no Retry-After is read here).</item>
    /// <item>URL under <c>/authwall</c>, <c>/checkpoint</c>, or
    /// <c>/login</c> → LoginRequired, 0.95.</item>
    /// <item>Title names a sign-in wall → LoginRequired, 0.95.</item>
    /// <item>Only when the page has <b>no</b> job cards, body phrases: sign-in
    /// or join-to-view wording → LoginRequired, 0.85.</item>
    /// <item>HTTP 403 → AccessDenied, 0.9; HTTP 401 → LoginRequired, 0.8; HTTP 5xx → TransientError, 0.8.</item>
    /// <item>Otherwise <see cref="ChallengeAssessment.Clear"/>.</item>
    /// </list>
    /// Body text is ignored once job cards are present, so a usable guest
    /// page whose footer mentions signing in stays clean. Evidence names
    /// only the rule and the matched phrase, never page content.
    /// </summary>
    public static partial class LinkedInChallengeClassifier
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

            if (status is 429 or 999)
            {
                return new ChallengeAssessment(
                    ChallengeKind.RateLimited,
                    0.95,
                    string.Create(CultureInfo.InvariantCulture, $"status={status}")
                );
            }

            var path = finalUrl.IsAbsoluteUri ? finalUrl.AbsolutePath : finalUrl.OriginalString;
            if (WallPath().IsMatch(path))
            {
                return new ChallengeAssessment(ChallengeKind.LoginRequired, 0.95, "url=authwall");
            }

            if (TitleWall().IsMatch(title))
            {
                return new ChallengeAssessment(ChallengeKind.LoginRequired, 0.95, "title:'sign in'");
            }

            if (!hasJobCards && BodyWall().IsMatch(bodyText))
            {
                return new ChallengeAssessment(ChallengeKind.LoginRequired, 0.85, "body:'sign in'");
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

        [GeneratedRegex(
            @"^/(authwall|checkpoint|login)(/|$)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        )]
        private static partial Regex WallPath();

        [GeneratedRegex(
            @"sign\s?in\s+to\s+view|join\s+now\s+to\s+see|you\W?ve\s+been\s+signed\s+out|please\s+sign\s+in",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        )]
        private static partial Regex TitleWall();

        [GeneratedRegex(
            @"sign\s+in\s+to\s+view\s+more|sign\s+in\s+to\s+see|join\s+(?:now\s+)?to\s+(?:view|see|apply)|sign\s+in\s+to\s+apply|create\s+(?:a|an|your)\s+(?:free\s+)?account\s+to\s+(?:view|see|apply)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        )]
        private static partial Regex BodyWall();
    }
}
