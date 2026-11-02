using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using Xunit;

namespace JobScrape.Domain.Tests.Sourcing
{
    public sealed class RehydrationTests
    {
        [Theory]
        [InlineData("https://www.naukri.com/job-listings-x-1", "naukri")]
        [InlineData("https://www.linkedin.com/jobs/view/4012345678", "linkedin")]
        [InlineData("https://in.indeed.com/viewjob?jk=abc", "indeed")]
        public void CanonicalUrlParseInfersSourceFromHost(string stored, string source)
        {
            var url = CanonicalUrl.Parse(stored);

            Assert.Equal(stored, url.Value);
            Assert.Equal(CanonicalUrl.Create(stored, SourceKey.Parse(source)), url);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("not a url")]
        [InlineData("https://evil.example/job-listings-x-1")]
        [InlineData("http://www.naukri.com/job-listings-x-1")]
        public void CanonicalUrlParseRejectsUnknownOrUnsafe(string? stored)
        {
            Assert.Throws<DomainException>(() => CanonicalUrl.Parse(stored));
        }

        [Fact]
        public void RunStatsRestoreRoundTripsCounts()
        {
            var stats = RunStats.Restore(3, 50, 40, 5, 10, 1, 2);

            Assert.Equal(3, stats.PagesFetched);
            Assert.Equal(50, stats.PostingsFound);
            Assert.Equal(40, stats.PostingsNew);
            Assert.Equal(5, stats.PostingsUpdated);
            Assert.Equal(10, stats.DetailsFetched);
            Assert.Equal(1, stats.Challenges);
            Assert.Equal(2, stats.Failures);
        }

        [Theory]
        [InlineData(-1, 0, 0, 0, 0, 0, 0)]
        [InlineData(0, 0, 0, 0, -1, 0, 0)]
        [InlineData(0, 0, 0, 0, 0, -1, 0)]
        [InlineData(0, 0, 0, 0, 0, 0, -1)]
        [InlineData(0, -1, 0, 0, 0, 0, 0)]
        [InlineData(0, 5, 4, 2, 0, 0, 0)]
        public void RunStatsRestoreRejectsInvalidCounts(
            int pages,
            int found,
            int added,
            int updated,
            int details,
            int challenges,
            int failures
        )
        {
            Assert.Throws<DomainException>(() =>
                RunStats.Restore(pages, found, added, updated, details, challenges, failures)
            );
        }
    }
}
