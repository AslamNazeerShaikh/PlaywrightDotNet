using JobScrape.Domain.Access;
using JobScrape.Infrastructure.Sources.Indeed;
using Xunit;
using static JobScrape.Infrastructure.Tests.Sources.Indeed.IndeedTestPages;

namespace JobScrape.Infrastructure.Tests.Sources.Indeed
{
    public sealed class IndeedChallengeClassifierTests
    {
        private static readonly Uri SearchUrl = new(
            "https://in.indeed.com/jobs?q=dotnet&l=Pune&fromage=7"
        );

        private static ChallengeAssessment ClassifyFixture(string fixture, int? status = 200)
        {
            var html = Fixture(fixture);
            var (title, body) = Visible(html);
            var hasCards = IndeedSearchParser.Parse(html, PageUrl, Now).Jobs.Count > 0;
            return IndeedChallengeClassifier.Classify(SearchUrl, status, title, body, hasCards);
        }

        [Theory]
        [InlineData(200)]
        [InlineData(403)]
        [InlineData(null)]
        public void CloudflareFixtureIsAccessDeniedWithZeroJobs(int? status)
        {
            var assessment = ClassifyFixture("search-cloudflare.html", status);

            Assert.Equal(ChallengeKind.AccessDenied, assessment.Kind);
            Assert.Empty(
                IndeedSearchParser.Parse(Fixture("search-cloudflare.html"), PageUrl, Now).Jobs
            );
        }

        [Theory]
        [InlineData("search-normal.html")]
        [InlineData("search-empty.html")]
        public void UsableResultPagesAreClear(string fixture)
        {
            // search-empty's footer mentions reCAPTCHA and a "Login" link:
            // neither is a wall.
            Assert.Same(ChallengeAssessment.Clear, ClassifyFixture(fixture));
        }

        [Fact]
        public void TooManyRequestsAreRateLimited()
        {
            var assessment = IndeedChallengeClassifier.Classify(
                SearchUrl,
                429,
                "Dot Net Developer Jobs - Indeed",
                "No results",
                hasJobCards: false
            );

            Assert.Equal(ChallengeKind.RateLimited, assessment.Kind);
        }

        [Fact]
        public void TitleCaptchaIsCaptchaWithTitleEvidence()
        {
            var assessment = IndeedChallengeClassifier.Classify(
                SearchUrl,
                200,
                "Please complete the CAPTCHA",
                "No results",
                hasJobCards: false
            );

            Assert.Equal(ChallengeKind.Captcha, assessment.Kind);
            Assert.Equal(0.95, assessment.Confidence);
            Assert.Equal("title:'captcha'", assessment.Evidence);
        }

        [Fact]
        public void TitleAccessDeniedIsAccessDeniedWithTitleEvidence()
        {
            var assessment = IndeedChallengeClassifier.Classify(
                SearchUrl,
                200,
                "Access Denied",
                "No results",
                hasJobCards: false
            );

            Assert.Equal(ChallengeKind.AccessDenied, assessment.Kind);
            Assert.Equal("title:'access denied'", assessment.Evidence);
        }

        [Fact]
        public void BodyCloudflareWithoutCardsIsAccessDenied()
        {
            var assessment = IndeedChallengeClassifier.Classify(
                SearchUrl,
                200,
                "Dot Net Developer Jobs - Indeed",
                "Checking your browser before accessing the site.",
                hasJobCards: false
            );

            Assert.Equal(ChallengeKind.AccessDenied, assessment.Kind);
            Assert.Equal("body:'cloudflare'", assessment.Evidence);
        }

        [Fact]
        public void BodyCaptchaWithoutCardsIsCaptcha()
        {
            var assessment = IndeedChallengeClassifier.Classify(
                SearchUrl,
                200,
                "Dot Net Developer Jobs - Indeed",
                "Please confirm I'm not a robot to continue.",
                hasJobCards: false
            );

            Assert.Equal(ChallengeKind.Captcha, assessment.Kind);
            Assert.Equal(0.85, assessment.Confidence);
            Assert.Equal("body:'i'm not a robot'", assessment.Evidence);
        }

        [Fact]
        public void BodyAccessDeniedWithoutCardsIsAccessDenied()
        {
            var assessment = IndeedChallengeClassifier.Classify(
                SearchUrl,
                200,
                "Dot Net Developer Jobs - Indeed",
                "Access denied by the security policy.",
                hasJobCards: false
            );

            Assert.Equal(ChallengeKind.AccessDenied, assessment.Kind);
            Assert.Equal(0.85, assessment.Confidence);
            Assert.Equal("body:'access denied'", assessment.Evidence);
        }

        [Theory]
        [InlineData(403, ChallengeKind.AccessDenied, "status=403")]
        [InlineData(401, ChallengeKind.LoginRequired, "status=401")]
        [InlineData(503, ChallengeKind.TransientError, "status=503")]
        public void HttpStatusesMapToWallKinds(
            int status,
            ChallengeKind expectedKind,
            string expectedEvidence
        )
        {
            var assessment = IndeedChallengeClassifier.Classify(
                SearchUrl,
                status,
                "Dot Net Developer Jobs - Indeed",
                "No results",
                hasJobCards: false
            );

            Assert.Equal(expectedKind, assessment.Kind);
            Assert.Equal(expectedEvidence, assessment.Evidence);
        }

        [Fact]
        public void ResultsPageWhoseBodyMentionsWallPhrasesIsClear()
        {
            var assessment = IndeedChallengeClassifier.Classify(
                SearchUrl,
                200,
                "Dot Net Developer Jobs - Indeed",
                "20 jobs ... Checking your browser status. Trouble signing in?",
                hasJobCards: true
            );

            Assert.Same(ChallengeAssessment.Clear, assessment);
        }
    }
}
