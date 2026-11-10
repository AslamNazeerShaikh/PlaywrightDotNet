using JobScrape.Infrastructure.Sources.Naukri;
using Xunit;

namespace JobScrape.Infrastructure.Tests.Sources.Naukri
{
    public sealed class NaukriResultRangeTests
    {
        [Fact]
        public void ParsesValidRange()
        {
            var range = NaukriResultRange.Parse("1 - 20 of 131");

            Assert.Equal(new NaukriResultRange(1, 20, 131), range);
            Assert.Equal(20, range!.PageSize);
            Assert.Equal(7, range.MaxPage);
        }

        [Fact]
        public void ParsesLastPageRange()
        {
            var range = NaukriResultRange.Parse("121 - 131 of 131");

            Assert.Equal(new NaukriResultRange(121, 131, 131), range);
            Assert.Equal(11, range!.PageSize);
            Assert.Equal(12, range.MaxPage);
        }

        [Fact]
        public void ParsesThousandsSeparators()
        {
            // No fixture shows commas today; the live site may emit them.
            var range = NaukriResultRange.Parse("1,181 - 1,200 of 1,581");

            Assert.Equal(new NaukriResultRange(1181, 1200, 1581), range);
            Assert.Equal(20, range!.PageSize);
            Assert.Equal(80, range.MaxPage);
        }

        [Fact]
        public void ParsesSingleResultPage()
        {
            var range = NaukriResultRange.Parse("1 - 1 of 1");

            Assert.Equal(new NaukriResultRange(1, 1, 1), range);
            Assert.Equal(1, range!.PageSize);
            Assert.Equal(1, range.MaxPage);
        }

        [Fact]
        public void TheObserved303CountIsSixteenPages()
        {
            Assert.Equal(16, NaukriResultRange.Parse("1 - 20 of 303")!.MaxPage);
            Assert.Equal(3, NaukriResultRange.Parse("301 - 303 of 303")!.PageSize);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("no results found")]
        [InlineData("1-20")]
        [InlineData("1 - 20 of many")]
        [InlineData("0 - 20 of 131")]
        [InlineData("20 - 1 of 131")]
        [InlineData("1 - 20 of 10")]
        [InlineData("0 - 0 of 0")]
        [InlineData("1 - 20 of 0")]
        [InlineData("99999999999999999999 - 1 of 131")]
        public void NullOrInconsistentInputYieldsNull(string? raw)
        {
            Assert.Null(NaukriResultRange.Parse(raw));
        }
    }
}
