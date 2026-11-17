using JobScrape.Application.Validation;
using JobScrape.Domain.Identity;
using Xunit;

namespace JobScrape.Application.Tests
{
    public sealed class EnumNamesTests
    {
        [Theory]
        [InlineData("Admin", RoleName.Admin)]
        [InlineData("operator", RoleName.Operator)]
        [InlineData(" VIEWER ", RoleName.Viewer)]
        public void MatchesDeclaredNamesIgnoringCaseAndWhitespace(string raw, RoleName expected)
        {
            Assert.True(EnumNames.TryParse<RoleName>(raw, out var parsed));
            Assert.Equal(expected, parsed);
        }

        [Theory]
        [InlineData("1")]
        [InlineData("01")]
        [InlineData("0,1")]
        [InlineData("Admin,Viewer")]
        [InlineData("Wizard")]
        [InlineData("")]
        [InlineData(null)]
        public void RejectsNumbersListsAndUnknownNames(string? raw)
        {
            Assert.False(EnumNames.TryParse<RoleName>(raw, out var parsed));
            Assert.Equal(default, parsed);
        }
    }
}
