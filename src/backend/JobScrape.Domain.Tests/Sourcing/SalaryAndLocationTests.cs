using JobScrape.Domain.Sourcing;
using Xunit;

namespace JobScrape.Domain.Tests.Sourcing
{
    public sealed class SalaryAndLocationTests
    {
        [Fact]
        public void SalaryRangeAcceptsBoundsAndUppercasesCurrency()
        {
            var salary = new SalaryRange(1_200_000m, 1_800_000m, " inr ", SalaryPeriod.Year);

            Assert.Equal(1_200_000m, salary.Min);
            Assert.Equal(1_800_000m, salary.Max);
            Assert.Equal("INR", salary.Currency);
            Assert.Equal(SalaryPeriod.Year, salary.Period);
        }

        [Theory]
        [InlineData(null, 50_000)]
        [InlineData(50_000, null)]
        [InlineData(0, 0)]
        [InlineData(10, 10)]
        public void SalaryRangeAcceptsOneSidedAndEqualBounds(int? min, int? max)
        {
            var salary = new SalaryRange(min, max, "INR", SalaryPeriod.Month);

            Assert.Equal(min, (int?)salary.Min);
            Assert.Equal(max, (int?)salary.Max);
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData(-1, 10)]
        [InlineData(10, -1)]
        [InlineData(20, 10)]
        public void SalaryRangeRejectsInvalidBounds(int? min, int? max)
        {
            Assert.Throws<DomainException>(() =>
                new SalaryRange(min, max, "INR", SalaryPeriod.Year)
            );
        }

        [Theory]
        [InlineData(null)]
        [InlineData("IN")]
        [InlineData("INRR")]
        [InlineData("I1R")]
        [InlineData("₹")]
        public void SalaryRangeRejectsNonIsoCurrency(string? currency)
        {
            Assert.Throws<DomainException>(() =>
                new SalaryRange(1, 2, currency, SalaryPeriod.Year)
            );
        }

        [Fact]
        public void SalaryRangeRejectsUndefinedPeriod()
        {
            Assert.Throws<DomainException>(() => new SalaryRange(1, 2, "INR", (SalaryPeriod)99));
        }

        [Fact]
        public void JobLocationNormalizesPartsAndBlankMeansUnknown()
        {
            var location = new JobLocation(
                "  Pune,  Hyderabad ",
                " Pune ",
                "  ",
                null,
                WorkMode.Hybrid
            );

            Assert.Equal("Pune, Hyderabad", location.Raw);
            Assert.Equal("Pune", location.City);
            Assert.Null(location.Region);
            Assert.Null(location.Country);
            Assert.Equal(WorkMode.Hybrid, location.WorkMode);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("   ")]
        public void JobLocationRequiresRawText(string? raw)
        {
            Assert.Throws<DomainException>(() =>
                new JobLocation(raw, "Pune", null, null, WorkMode.Onsite)
            );
        }

        [Fact]
        public void JobLocationRejectsUndefinedWorkMode()
        {
            Assert.Throws<DomainException>(() =>
                new JobLocation("Pune", null, null, null, (WorkMode)42)
            );
        }
    }
}
