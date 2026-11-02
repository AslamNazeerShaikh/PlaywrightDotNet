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
            Assert.Empty(IndeedSearchParser.Parse(Fixture("search-cloudflare.html"), PageUrl, Now).Jobs);
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
