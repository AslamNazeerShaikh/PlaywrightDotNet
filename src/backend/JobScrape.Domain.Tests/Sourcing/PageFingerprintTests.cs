using JobScrape.Domain.Sourcing;
using Xunit;

namespace JobScrape.Domain.Tests.Sourcing
{
    public sealed class PageFingerprintTests
    {
        [Fact]
        public void NormalizesComponentsAndComparesByValue()
        {
            var first = new PageFingerprint(" 123 ", "1 -  20 of 431");
            var same = new PageFingerprint("123", "1 - 20 of 431");

            Assert.Equal("123", first.FirstExternalId);
            Assert.Equal("1 - 20 of 431", first.ResultRange);
            Assert.Equal(first, same);
            Assert.NotEqual(first, new PageFingerprint("124", "1 - 20 of 431"));
        }

        [Fact]
        public void EitherComponentAloneIsEnough()
        {
            Assert.Null(new PageFingerprint("123", "  ").ResultRange);
            Assert.Null(new PageFingerprint(null, "21 - 40").FirstExternalId);
        }

        [Fact]
        public void RejectsBothBlank()
        {
            Assert.Throws<DomainException>(() => new PageFingerprint(" ", null));
        }
    }
}
