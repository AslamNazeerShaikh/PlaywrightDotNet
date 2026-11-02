using JobScrape.Domain.Access;
using JobScrape.Domain.Common;
using JobScrape.Infrastructure.Browser;
using JobScrape.Infrastructure.Sources.LinkedIn;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static JobScrape.Infrastructure.Tests.Sources.LinkedIn.LinkedInTestPages;

namespace JobScrape.Infrastructure.Tests.Sources.LinkedIn
{
    public sealed class LinkedInChallengeClassifierTests
    {
        private static readonly Uri SearchUrl = new(
            "https://www.linkedin.com/jobs/search?keywords=dotnet&location=Pune&f_TPR=r604800"
        );

        private static ChallengeAssessment ClassifyFixture(string fixture, int? status = 200)
        {
            var html = Fixture(fixture);
            var (title, body) = Visible(html);
            var hasCards = LinkedInSearchParser.Parse(html, PageUrl, Now).Jobs.Count > 0;
            return LinkedInChallengeClassifier.Classify(SearchUrl, status, title, body, hasCards);
        }

        [Fact]
        public void AuthWallFixtureIsLoginRequiredWithZeroJobs()
        {
            var assessment = ClassifyFixture("search-authwall.html");

            Assert.Equal(ChallengeKind.LoginRequired, assessment.Kind);
            Assert.Empty(LinkedInSearchParser.Parse(Fixture("search-authwall.html"), PageUrl, Now).Jobs);
        }

        [Fact]
        public void RedirectToAuthWallIsLoginRequired()
        {
            var assessment = LinkedInChallengeClassifier.Classify(
                new Uri("https://www.linkedin.com/authwall?trk=jobs"),
                200,
                "LinkedIn",
                string.Empty,
                hasJobCards: false
            );

            Assert.Equal(ChallengeKind.LoginRequired, assessment.Kind);
        }

        [Theory]
        [InlineData(429)]
        [InlineData(999)]
        public void ThrottleStatusesAreRateLimited(int status)
        {
            var html = Fixture("search-429.html");
            var (title, body) = Visible(html);

            var assessment = LinkedInChallengeClassifier.Classify(
                SearchUrl,
                status,
                title,
                body,
                hasJobCards: false
            );

            Assert.Equal(ChallengeKind.RateLimited, assessment.Kind);
        }

        [Theory]
        [InlineData("search-guest.html")]
        [InlineData("search-empty.html")]
        public void UsableGuestPagesAreClear(string fixture)
        {
            Assert.Same(ChallengeAssessment.Clear, ClassifyFixture(fixture));
        }

        [Fact]
        public void RefusesWithoutExplicitConsent()
        {
            var blocked = CompliancePolicy.CanRun(
                SourceKey.LinkedIn,
                new ComplianceConsent(false, false, false)
            );

            Assert.False(blocked.IsAllowed);
            Assert.NotNull(blocked.Reason);
        }

        [Fact]
        public void AllowsWithFullConsent()
        {
            var allowed = CompliancePolicy.CanRun(
                SourceKey.LinkedIn,
                new ComplianceConsent(true, true, true)
            );

            Assert.True(allowed.IsAllowed);
        }

        [Fact]
        public async Task DetailsAreBlockedWithoutFetching()
        {
            await using var pool = new BrowserSessionPool(
                new BrowserSessionSettings("profiles", "artifacts", false, TimeSpan.FromSeconds(5))
            );
            var source = new LinkedInJobSource(
                pool,
                TimeProvider.System,
                NullLogger<LinkedInJobSource>.Instance
            );

            Assert.False(source.Capabilities.SupportsDetails);
            var result = await source.GetDetailAsync(
                new Application.Features.Scraping.SourceRunContext(
                    Domain.Common.ScrapeRunId.New(),
                    SourceKey.LinkedIn,
                    Domain.Common.BrowserProfileId.New(),
                    new Domain.Sourcing.SearchCriteria("dotnet", ["Pune"], null, 7, false)
                ),
                new Application.Features.Scraping.JobReference(
                    Domain.Sourcing.ExternalJobId.Create("4012345670"),
                    Domain.Sourcing.CanonicalUrl.Create(
                        "https://www.linkedin.com/jobs/view/4012345670",
                        SourceKey.LinkedIn
                    )
                ),
                TestContext.Current.CancellationToken
            );

            Assert.Null(result.Detail);
            Assert.Equal(ChallengeKind.LoginRequired, result.Challenge.Kind);
        }
    }
}
