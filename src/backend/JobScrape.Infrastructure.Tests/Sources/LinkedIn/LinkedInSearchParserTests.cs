using JobScrape.Infrastructure.Sources.LinkedIn;
using Xunit;
using static JobScrape.Infrastructure.Tests.Sources.LinkedIn.LinkedInTestPages;

namespace JobScrape.Infrastructure.Tests.Sources.LinkedIn
{
    public sealed class LinkedInSearchParserTests
    {
        [Fact]
        public void ParsesGuestCardsFromFixture()
        {
            var page = LinkedInSearchParser.Parse(Fixture("search-guest.html"), PageUrl, Now);

            Assert.Equal(6, page.Jobs.Count);
            Assert.Equal(0, page.DroppedCards);
            Assert.True(page.HasNext);
            Assert.Equal("4012345670", page.FirstJobId);
            Assert.NotNull(page.ResultCount);
            foreach (var job in page.Jobs)
            {
                var posting = AssertAccepted(job);
                Assert.Matches(@"^https://www\.linkedin\.com/jobs/view/\d+$", job.Url);
                Assert.Equal(job.ExternalJobId, posting.ExternalJobId.Value);
            }
        }

        [Fact]
        public void FirstCardCarriesExpectedFields()
        {
            var page = LinkedInSearchParser.Parse(Fixture("search-guest.html"), PageUrl, Now);

            var first = page.Jobs[0];
            Assert.Equal("Senior .NET Developer", first.Title);
            Assert.Equal("Contoso", first.Company);
            Assert.Equal("Pune, Maharashtra, India", first.Location?.Raw);
            Assert.Equal("Pune", first.Location?.City);
            Assert.Equal(new DateTimeOffset(2026, 10, 30, 0, 0, 0, TimeSpan.Zero), first.PostedAt);
        }

        [Fact]
        public void TrackingParamsAreStrippedFromCanonicalUrls()
        {
            var page = LinkedInSearchParser.Parse(Fixture("search-guest.html"), PageUrl, Now);

            Assert.DoesNotContain("refId", page.Jobs[0].Url);
            Assert.DoesNotContain("trackingId", page.Jobs[0].Url);
        }

        [Fact]
        public void EmptyResultsYieldNoJobsAndNoNext()
        {
            var page = LinkedInSearchParser.Parse(Fixture("search-empty.html"), PageUrl, Now);

            Assert.Empty(page.Jobs);
            Assert.False(page.HasNext);
        }

        [Fact]
        public void HiddenAndTrapCardsAreSkipped()
        {
            var page = LinkedInSearchParser.Parse(
                Page(
                    Card(id: "4012345670"),
                    Card(id: "4012345671", wrapperAttributes: " style=\"display:none\""),
                    Card(id: "4012345672", href: "https://evil.example/jobs/view/4012345672"),
                    Card(id: "4012345673", href: "javascript:void(0)")
                ),
                PageUrl,
                Now
            );

            Assert.Single(page.Jobs);
            Assert.Equal("4012345670", page.Jobs[0].ExternalJobId);
        }

        [Fact]
        public void CardsWithoutTitleAreDroppedAndCounted()
        {
            var page = LinkedInSearchParser.Parse(
                Page(Card(id: "4012345679", title: null)),
                PageUrl,
                Now
            );

            Assert.Empty(page.Jobs);
            Assert.Equal(1, page.DroppedCards);
        }
    }
}
