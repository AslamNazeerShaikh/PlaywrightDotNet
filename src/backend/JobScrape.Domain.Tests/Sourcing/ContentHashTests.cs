using JobScrape.Domain.Sourcing;
using Xunit;

namespace JobScrape.Domain.Tests.Sourcing
{
    public sealed class ContentHashTests
    {
        [Fact]
        public void ComputeIsDeterministicLowercaseSha256Hex()
        {
            var first = ContentHash.Compute(["Senior .NET Developer", "Contoso"]);
            var second = ContentHash.Compute(["Senior .NET Developer", "Contoso"]);

            Assert.Equal(first, second);
            Assert.Equal(64, first.Value.Length);
            Assert.Matches("^[0-9a-f]{64}$", first.Value);
            Assert.Equal(first.Value, first.ToString());
        }

        [Fact]
        public void DistinguishesNullFromEmptyAndFieldBoundaries()
        {
            var nullField = ContentHash.Compute([null]);
            var emptyField = ContentHash.Compute([""]);
            var joined = ContentHash.Compute(["ab", "c"]);
            var split = ContentHash.Compute(["a", "bc"]);

            Assert.NotEqual(nullField, emptyField);
            Assert.NotEqual(joined, split);
        }

        [Fact]
        public void ComputeRejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => ContentHash.Compute(null!));
        }

        [Fact]
        public void ParseRoundTripsComputedValue()
        {
            var hash = ContentHash.Compute(["x"]);

            Assert.Equal(hash, ContentHash.Parse(hash.Value));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("abc")]
        [InlineData("ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789")]
        [InlineData("g000000000000000000000000000000000000000000000000000000000000000")]
        public void ParseRejectsNonDigest(string? value)
        {
            Assert.Throws<DomainException>(() => ContentHash.Parse(value));
        }
    }
}
