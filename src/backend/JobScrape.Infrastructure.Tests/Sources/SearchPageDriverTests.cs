using JobScrape.Infrastructure.Sources;
using Xunit;

namespace JobScrape.Infrastructure.Tests.Sources
{
    /// <summary>
    /// Cursor parsing for offset-paged sources: an unusable cursor must
    /// fall back to the first page instead of escaping into the URL as a
    /// negative or overflowing start offset.
    /// </summary>
    public sealed class SearchPageDriverTests
    {
        [Fact]
        public void NullCursorYieldsZeroOffset()
        {
            Assert.Equal(0, SearchPageDriver.CursorOffset(null));
        }

        [Theory]
        [InlineData("")]
        [InlineData("abc")]
        [InlineData("10.5")]
        [InlineData("+10")]
        [InlineData(" 10")]
        [InlineData("-1")]
        [InlineData("-1000000")]
        [InlineData("99999999999999999999")]
        public void UnusableCursorFallsBackToZero(string cursor)
        {
            Assert.Equal(0, SearchPageDriver.CursorOffset(cursor));
        }

        [Fact]
        public void ZeroCursorYieldsZeroOffset()
        {
            Assert.Equal(0, SearchPageDriver.CursorOffset("0"));
        }

        [Fact]
        public void InRangeCursorYieldsItsOffset()
        {
            Assert.Equal(10, SearchPageDriver.CursorOffset("10"));
            Assert.Equal(
                int.MaxValue,
                SearchPageDriver.CursorOffset(
                    int.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture)
                )
            );
        }
    }
}
