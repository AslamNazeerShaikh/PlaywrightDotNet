using JobScrape.Domain.Common;
using Xunit;

namespace JobScrape.Domain.Tests.Common
{
    public sealed class SourceKeyTests
    {
        [Theory]
        [InlineData("naukri")]
        [InlineData(" NAUKRI ")]
        [InlineData("Naukri")]
        public void ParsesNaukriCaseAndWhitespaceInsensitively(string raw)
        {
            var key = SourceKey.Parse(raw);

            Assert.Same(SourceKey.Naukri, key);
            Assert.Equal("naukri", key.Value);
        }

        [Fact]
        public void ParsesIndeed()
        {
            Assert.Same(SourceKey.Indeed, SourceKey.Parse("indeed"));
        }

        [Fact]
        public void ParsesLinkedIn()
        {
            Assert.Same(SourceKey.LinkedIn, SourceKey.Parse("LinkedIn"));
        }

        [Theory]
        [InlineData("monster")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void ParseRejectsUnknownOrMissingSource(string? raw)
        {
            Assert.Throws<DomainException>(() => SourceKey.Parse(raw));
        }

        [Fact]
        public void TryParseReturnsKeyForKnownSource()
        {
            var parsed = SourceKey.TryParse(" indeed ", out var key);

            Assert.True(parsed);
            Assert.Same(SourceKey.Indeed, key);
        }

        [Theory]
        [InlineData("monster")]
        [InlineData(null)]
        public void TryParseReturnsFalseForUnknownSource(string? raw)
        {
            var parsed = SourceKey.TryParse(raw, out var key);

            Assert.False(parsed);
            Assert.Null(key);
        }

        [Fact]
        public void TiersMatchCompliancePolicy()
        {
            Assert.Equal(ComplianceTier.Standard, SourceKey.Naukri.Tier);
            Assert.Equal(ComplianceTier.Restricted, SourceKey.Indeed.Tier);
            Assert.Equal(ComplianceTier.HighRisk, SourceKey.LinkedIn.Tier);
        }

        [Fact]
        public void ToStringReturnsLowercaseKey()
        {
            Assert.Equal("linkedin", SourceKey.LinkedIn.ToString());
        }

        [Fact]
        public void EqualityIsByValue()
        {
            Assert.Equal(SourceKey.Naukri, SourceKey.Parse("NAUKRI"));
            Assert.NotEqual(SourceKey.Naukri, SourceKey.Indeed);
        }
    }
}
