using JobScrape.Domain.Common;
using Xunit;

namespace JobScrape.Domain.Tests.Common
{
    public sealed class TextTests
    {
        [Theory]
        [InlineData(null, "")]
        [InlineData("", "")]
        [InlineData("  a \t b\r\n c  ", "a b c")]
        public void CollapseTrimsAndCollapsesWhitespace(string? input, string expected)
        {
            Assert.Equal(expected, Text.Collapse(input));
        }

        [Theory]
        [InlineData(null)]
        [InlineData(" \t ")]
        public void CollapseOrNullMapsBlankToNull(string? input)
        {
            Assert.Null(Text.CollapseOrNull(input));
        }

        [Fact]
        public void CollapseOrNullKeepsText()
        {
            Assert.Equal("a b", Text.CollapseOrNull(" a  b "));
        }

        [Fact]
        public void DetectsControlCharacters()
        {
            Assert.True(Text.HasControlCharacters("a\u0000b"));
            Assert.False(Text.HasControlCharacters("plain text"));
            Assert.Throws<ArgumentNullException>(() => Text.HasControlCharacters(null!));
        }
    }
}
