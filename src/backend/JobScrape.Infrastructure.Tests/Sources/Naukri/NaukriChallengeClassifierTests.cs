using JobScrape.Domain.Access;
using JobScrape.Infrastructure.Sources.Naukri;
using Xunit;
using static JobScrape.Infrastructure.Tests.Sources.Naukri.NaukriTestPages;

namespace JobScrape.Infrastructure.Tests.Sources.Naukri
{
    public sealed class NaukriChallengeClassifierTests
    {
        private static readonly Uri SearchUrl = new(
            "https://www.naukri.com/dot-net-developer-jobs-in-pune?k=dot%20net%20developer&l=Pune"
        );

        private static ChallengeAssessment ClassifyFixture(string fixture, int? status = 200)
        {
            var html = Fixture(fixture);
            var (title, body) = Visible(html);
            var hasCards = NaukriSearchParser.Parse(html, Now).Jobs.Count > 0;
            return NaukriChallengeClassifier.Classify(SearchUrl, status, title, body, hasCards);
        }

        [Theory]
        [InlineData(200)]
        [InlineData(403)]
        [InlineData(null)]
        public void AkamaiBlockFixtureIsAccessDeniedWithZeroJobs(int? status)
        {
            var assessment = ClassifyFixture("search-challenge.html", status);

            Assert.Equal(ChallengeKind.AccessDenied, assessment.Kind);
            Assert.Equal(0.95, assessment.Confidence);
            Assert.Equal("title:'access denied'", assessment.Evidence);
            Assert.Empty(NaukriSearchParser.Parse(Fixture("search-challenge.html"), Now).Jobs);
        }

        [Theory]
        [InlineData("search-normal.html")]
        [InlineData("search-promoted.html")]
        [InlineData("search-last-page.html")]
        [InlineData("search-empty.html")]
        public void UsableResultPagesAreClear(string fixture)
        {
            // search-normal's footer mentions reCAPTCHA and "Access denied?",
            // search-empty's footer mentions reCAPTCHA, and every page has a
            // "Login" link: none of that is a wall.
            Assert.Same(ChallengeAssessment.Clear, ClassifyFixture(fixture));
        }

        [Fact]
        public void ResultsPageWhoseBodyMentionsWallPhrasesIsClear()
        {
            var assessment = NaukriChallengeClassifier.Classify(
                SearchUrl,
                200,
                "Dot Net Developer Jobs In Pune - Naukri.com",
                "20 jobs ... Trouble signing in? Complete the CAPTCHA. Please verify you are human. Access Denied errors? Pardon the interruption. Please login to continue.",
                hasJobCards: true
            );

            Assert.Same(ChallengeAssessment.Clear, assessment);
        }

        [Fact]
        public void EdgesuiteHostIsAccessDenied()
        {
            var assessment = NaukriChallengeClassifier.Classify(
                new Uri("https://errors.edgesuite.net/18.abc.1761955200.def"),
                200,
                "",
                "",
                hasJobCards: false
            );

            Assert.Equal(ChallengeKind.AccessDenied, assessment.Kind);
            Assert.Equal(0.99, assessment.Confidence);
            Assert.Equal("host=errors.edgesuite.net", assessment.Evidence);
        }

        [Fact]
        public void EdgesuiteReferenceInBodyIsAccessDenied()
        {
            var assessment = NaukriChallengeClassifier.Classify(
                SearchUrl,
                null,
                "",
                "Reference #18.1 https://errors.edgesuite.net/18.1",
                hasJobCards: false
            );

            Assert.Equal(ChallengeKind.AccessDenied, assessment.Kind);
            Assert.Equal("body:'errors.edgesuite.net'", assessment.Evidence);
        }

        [Fact]
        public void Status429IsRateLimitedWithoutRetryAfter()
        {
            var assessment = NaukriChallengeClassifier.Classify(SearchUrl, 429, "", "", false);

            Assert.Equal(ChallengeKind.RateLimited, assessment.Kind);
            Assert.Null(assessment.RetryAfter);
            Assert.Equal("status=429", assessment.Evidence);
        }

        [Theory]
        [InlineData(403, ChallengeKind.AccessDenied, "status=403")]
        [InlineData(401, ChallengeKind.LoginRequired, "status=401")]
        [InlineData(500, ChallengeKind.TransientError, "status=500")]
        [InlineData(503, ChallengeKind.TransientError, "status=503")]
        public void StatusCodesWithoutWallTextAreClassified(
            int status,
            ChallengeKind kind,
            string evidence
        )
        {
            var assessment = NaukriChallengeClassifier.Classify(
                SearchUrl,
                status,
                "Naukri.com",
                "Something went wrong",
                hasJobCards: false
            );

            Assert.Equal(kind, assessment.Kind);
            Assert.Equal(evidence, assessment.Evidence);
        }

        [Theory]
        [InlineData("https://www.naukri.com/nlogin/login?URL=https://www.naukri.com/x")]
        [InlineData("https://login.naukri.com/")]
        public void LoginUrlIsLoginRequired(string url)
        {
            var assessment = NaukriChallengeClassifier.Classify(
                new Uri(url),
                200,
                "Login - Naukri.com",
                "",
                hasJobCards: false
            );

            Assert.Equal(ChallengeKind.LoginRequired, assessment.Kind);
            Assert.True(assessment.RequiresManualIntervention);
        }

        [Fact]
        public void LoginWallWithoutCardsIsLoginRequired()
        {
            var assessment = NaukriChallengeClassifier.Classify(
                SearchUrl,
                200,
                "Naukri.com",
                "Please login to continue viewing jobs",
                hasJobCards: false
            );

            Assert.Equal(ChallengeKind.LoginRequired, assessment.Kind);
            Assert.Equal(0.7, assessment.Confidence);
            Assert.Equal("body:'please login'", assessment.Evidence);
        }

        [Theory]
        [InlineData("Captcha", "", ChallengeKind.Captcha, "title:'captcha'")]
        [InlineData(
            "Verify you are human",
            "",
            ChallengeKind.Captcha,
            "title:'verify you are human'"
        )]
        [InlineData(
            "Naukri.com",
            "Please verify you are   human to continue",
            ChallengeKind.Captcha,
            "body:'verify you are human'"
        )]
        [InlineData(
            "Naukri.com",
            "Check the box: I'm not a robot",
            ChallengeKind.Captcha,
            "body:'i'm not a robot'"
        )]
        [InlineData(
            "Pardon Our Interruption",
            "",
            ChallengeKind.SoftChallenge,
            "title:'pardon our interruption'"
        )]
        [InlineData(
            "Naukri.com",
            "Our systems have detected unusual traffic from your network",
            ChallengeKind.SoftChallenge,
            "body:'unusual traffic'"
        )]
        [InlineData(
            "Naukri.com",
            "Access Denied",
            ChallengeKind.AccessDenied,
            "body:'access denied'"
        )]
        public void WallPhrasesWithoutCardsAreClassified(
            string title,
            string body,
            ChallengeKind kind,
            string evidence
        )
        {
            var assessment = NaukriChallengeClassifier.Classify(SearchUrl, 200, title, body, false);

            Assert.Equal(kind, assessment.Kind);
            Assert.Equal(evidence, assessment.Evidence);
            Assert.InRange(assessment.Confidence, 0.8, 1.0);
        }

        [Fact]
        public void TitleWallWinsEvenWhenCardsArePresent()
        {
            var assessment = NaukriChallengeClassifier.Classify(
                SearchUrl,
                200,
                "Access Denied",
                "",
                hasJobCards: true
            );

            Assert.Equal(ChallengeKind.AccessDenied, assessment.Kind);
        }

        [Fact]
        public void BareCaptchaMentionInBodyIsNotAWall()
        {
            // A reCAPTCHA notice on a legitimate empty page must not pause a run.
            var assessment = NaukriChallengeClassifier.Classify(
                SearchUrl,
                200,
                "Naukri.com",
                "No results found. This site is protected by reCAPTCHA.",
                hasJobCards: false
            );

            Assert.Same(ChallengeAssessment.Clear, assessment);
        }

        [Fact]
        public void EvidenceNeverCarriesPageContent()
        {
            var assessment = NaukriChallengeClassifier.Classify(
                SearchUrl,
                200,
                "Naukri.com",
                "secret-session-token=abc123 Pardon the interruption while we check user someone",
                hasJobCards: false
            );

            Assert.Equal("body:'pardon the interruption'", assessment.Evidence);
            Assert.DoesNotContain("token", assessment.Evidence, StringComparison.Ordinal);
        }

        [Fact]
        public void NullArgumentsAreProgrammerErrors()
        {
            Assert.Throws<ArgumentNullException>(() =>
                NaukriChallengeClassifier.Classify(null!, 200, "", "", false)
            );
            Assert.Throws<ArgumentNullException>(() =>
                NaukriChallengeClassifier.Classify(SearchUrl, 200, null!, "", false)
            );
            Assert.Throws<ArgumentNullException>(() =>
                NaukriChallengeClassifier.Classify(SearchUrl, 200, "", null!, false)
            );
        }
    }
}
