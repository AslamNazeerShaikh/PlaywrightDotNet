using JobScrape.Domain.Sourcing;
using Xunit;

namespace JobScrape.Domain.Tests.Sourcing
{
    public sealed class RunLimitsTests
    {
        private static RunLimits Create(
            int maxPages = 3,
            int maxJobs = 60,
            int maxDetails = 60,
            double minDelaySeconds = 2,
            double maxDelaySeconds = 6
        )
        {
            return new RunLimits(
                maxPages,
                maxJobs,
                maxDetails,
                TimeSpan.FromSeconds(minDelaySeconds),
                TimeSpan.FromSeconds(maxDelaySeconds)
            );
        }

        [Fact]
        public void AcceptsTypicalLimits()
        {
            var limits = Create();

            Assert.Equal(3, limits.MaxPages);
            Assert.Equal(60, limits.MaxJobs);
            Assert.Equal(60, limits.MaxDetails);
            Assert.Equal(TimeSpan.FromSeconds(2), limits.MinDelay);
            Assert.Equal(TimeSpan.FromSeconds(6), limits.MaxDelay);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(600)]
        [InlineData(1000)]
        public void AcceptsPageBounds(int maxPages)
        {
            Assert.Equal(maxPages, Create(maxPages: maxPages).MaxPages);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1001)]
        [InlineData(-1)]
        public void RejectsPagesOutOfRange(int maxPages)
        {
            Assert.Throws<DomainException>(() => Create(maxPages: maxPages));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(20_000)]
        public void AcceptsJobBounds(int maxJobs)
        {
            Assert.Equal(maxJobs, Create(maxJobs: maxJobs).MaxJobs);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(20_001)]
        public void RejectsJobsOutOfRange(int maxJobs)
        {
            Assert.Throws<DomainException>(() => Create(maxJobs: maxJobs));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1000)]
        public void AcceptsDetailBoundsIncludingZero(int maxDetails)
        {
            Assert.Equal(maxDetails, Create(maxDetails: maxDetails).MaxDetails);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(1001)]
        public void RejectsDetailsOutOfRange(int maxDetails)
        {
            Assert.Throws<DomainException>(() => Create(maxDetails: maxDetails));
        }

        [Fact]
        public void AcceptsDelayBounds()
        {
            var shortest = Create(minDelaySeconds: 1, maxDelaySeconds: 1);
            var longest = Create(minDelaySeconds: 60, maxDelaySeconds: 120);

            Assert.Equal(TimeSpan.FromSeconds(1), shortest.MinDelay);
            Assert.Equal(TimeSpan.FromSeconds(120), longest.MaxDelay);
        }

        [Theory]
        [InlineData(0.999, 6)]
        [InlineData(0, 6)]
        [InlineData(60.001, 90)]
        public void RejectsMinDelayOutOfRange(double minDelaySeconds, double maxDelaySeconds)
        {
            Assert.Throws<DomainException>(() =>
                Create(minDelaySeconds: minDelaySeconds, maxDelaySeconds: maxDelaySeconds)
            );
        }

        [Fact]
        public void RejectsMinDelayGreaterThanMaxDelay()
        {
            Assert.Throws<DomainException>(() => Create(minDelaySeconds: 10, maxDelaySeconds: 5));
        }

        [Fact]
        public void RejectsMaxDelayOverTwoMinutes()
        {
            Assert.Throws<DomainException>(() =>
                Create(minDelaySeconds: 10, maxDelaySeconds: 120.001)
            );
        }

        [Fact]
        public void EqualityIsByValue()
        {
            Assert.Equal(Create(), Create());
            Assert.NotEqual(Create(), Create(maxPages: 4));
        }
    }
}
