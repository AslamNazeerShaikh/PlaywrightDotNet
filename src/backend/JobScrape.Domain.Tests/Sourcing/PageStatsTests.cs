using JobScrape.Domain.Sourcing;
using Xunit;

namespace JobScrape.Domain.Tests.Sourcing
{
    public sealed class PageStatsTests
    {
        [Theory]
        [InlineData(0, 0, 0)]
        [InlineData(20, 15, 5)]
        [InlineData(20, 0, 0)]
        public void AcceptsConsistentCounts(int found, int added, int updated)
        {
            var page = new PageStats(found, added, updated);

            Assert.Equal(found, page.PostingsFound);
            Assert.Equal(added, page.PostingsNew);
            Assert.Equal(updated, page.PostingsUpdated);
        }

        [Theory]
        [InlineData(-1, 0, 0)]
        [InlineData(5, -1, 0)]
        [InlineData(5, 0, -1)]
        public void RejectsNegativeCounts(int found, int added, int updated)
        {
            Assert.Throws<DomainException>(() => new PageStats(found, added, updated));
        }

        [Fact]
        public void RejectsNewPlusUpdatedOverFound()
        {
            Assert.Throws<DomainException>(() => new PageStats(5, 3, 3));
        }
    }
}
