using JobScrape.Infrastructure.Sources.Indeed;
using Xunit;
using static JobScrape.Infrastructure.Tests.Sources.Indeed.IndeedTestPages;

namespace JobScrape.Infrastructure.Tests.Sources.Indeed
{
    public sealed class IndeedSearchParserTests
    {
        [Fact]
        public void ParsesTwentyCardsFromNormalFixture()
        {
            var page = IndeedSearchParser.Parse(Fixture("search-normal.html"), PageUrl, Now);

            Assert.Equal(20, page.Jobs.Count);
            Assert.Equal(0, page.DroppedCards);
            Assert.True(page.HasNext);
            Assert.Equal("abc123def000XYZa", page.FirstJobKey);
            Assert.NotNull(page.ResultCount);
            foreach (var job in page.Jobs)
            {
                var posting = AssertAccepted(job);
                Assert.StartsWith("https://in.indeed.com/viewjob?jk=", job.Url);
                Assert.Equal(job.ExternalJobId, posting.ExternalJobId.Value);
            }
        }

        [Fact]
        public void FirstCardCarriesExpectedFields()
        {
            var page = IndeedSearchParser.Parse(Fixture("search-normal.html"), PageUrl, Now);

            var first = page.Jobs[0];
            Assert.Equal("Senior .NET Developer", first.Title);
            Assert.Equal("Contoso", first.Company);
            Assert.Equal("Pune, Maharashtra", first.Location?.Raw);
            Assert.Equal("₹8,00,000 - ₹12,00,000 a year", first.SalaryRaw);
            Assert.NotNull(first.Salary);
            Assert.Equal(new DateTimeOffset(2026, 10, 30, 0, 0, 0, TimeSpan.Zero), first.PostedAt);
            Assert.Equal(["C#", ".NET"], first.Skills);
        }

        [Fact]
        public void JustPostedMapsToStartOfToday()
        {
            var page = IndeedSearchParser.Parse(
                Page(Card(key: "keyjustposted1", posted: "PostedJust posted")),
                PageUrl,
                Now
            );

            Assert.Equal(
                new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero),
                page.Jobs[0].PostedAt
            );
        }

        [Fact]
        public void OpenEndedPostedStaysNull()
        {
            var page = IndeedSearchParser.Parse(
                Page(Card(key: "keyopenended1", posted: "Posted30+ days ago")),
                PageUrl,
                Now
            );

            Assert.Null(page.Jobs[0].PostedAt);
        }

        [Fact]
        public void EmptyResultsYieldNoJobsAndNoNext()
        {
            var page = IndeedSearchParser.Parse(Fixture("search-empty.html"), PageUrl, Now);

            Assert.Empty(page.Jobs);
            Assert.False(page.HasNext);
            Assert.Equal(0, page.DroppedCards);
        }

        [Fact]
        public void HiddenAndTrapCardsAreSkipped()
        {
            var page = IndeedSearchParser.Parse(
                Page(
                    Card(key: "visiblecard0001"),
                    Card(key: "hiddencard0002", wrapperAttributes: " style=\"display:none\""),
                    Card(
                        key: "offhostcard0003",
                        href: "https://evil.example/viewjob?jk=offhostcard0003"
                    ),
                    Card(key: "jslinkcard0004", href: "javascript:void(0)")
                ),
                PageUrl,
                Now
            );

            Assert.Single(page.Jobs);
            Assert.Equal("visiblecard0001", page.Jobs[0].ExternalJobId);
        }

        [Fact]
        public void DuplicateKeysAreEmittedOnce()
        {
            var page = IndeedSearchParser.Parse(
                Page(Card(key: "dupkey00000001"), Card(key: "dupkey00000001")),
                PageUrl,
                Now
            );

            Assert.Single(page.Jobs);
        }

        [Fact]
        public void CardsWithoutTitleAreDroppedAndCounted()
        {
            var page = IndeedSearchParser.Parse(
                Page(Card(key: "notitlecard001", title: null)),
                PageUrl,
                Now
            );

            Assert.Empty(page.Jobs);
            Assert.Equal(1, page.DroppedCards);
        }
    }
}
