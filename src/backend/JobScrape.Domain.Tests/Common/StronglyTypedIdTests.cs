using JobScrape.Domain.Common;
using Xunit;

namespace JobScrape.Domain.Tests.Common
{
    public sealed class StronglyTypedIdTests
    {
        private static readonly Guid Known = new("6f9619ff-8b86-d011-b42d-00c04fc964ff");

        [Fact]
        public void NewProducesDistinctNonEmptyIds()
        {
            var first = SearchProfileId.New();
            var second = SearchProfileId.New();

            Assert.NotEqual(Guid.Empty, first.Value);
            Assert.NotEqual(first, second);
        }

        [Fact]
        public void FromPreservesValueAndEquality()
        {
            Assert.Equal(Known, SearchProfileId.From(Known).Value);
            Assert.Equal(ScrapeRunId.From(Known), ScrapeRunId.From(Known));
            Assert.Equal(JobPostingId.From(Known).Value, BrowserProfileId.From(Known).Value);
        }

        [Fact]
        public void FromRejectsEmptyGuid()
        {
            Assert.Throws<DomainException>(() => SearchProfileId.From(Guid.Empty));
            Assert.Throws<DomainException>(() => ScrapeRunId.From(Guid.Empty));
            Assert.Throws<DomainException>(() => JobPostingId.From(Guid.Empty));
            Assert.Throws<DomainException>(() => BrowserProfileId.From(Guid.Empty));
        }

        [Fact]
        public void NewWorksForEveryIdType()
        {
            Assert.NotEqual(Guid.Empty, ScrapeRunId.New().Value);
            Assert.NotEqual(Guid.Empty, JobPostingId.New().Value);
            Assert.NotEqual(Guid.Empty, BrowserProfileId.New().Value);
        }

        [Fact]
        public void ToStringIsTheGuid()
        {
            var expected = Known.ToString();

            Assert.Equal(expected, SearchProfileId.From(Known).ToString());
            Assert.Equal(expected, ScrapeRunId.From(Known).ToString());
            Assert.Equal(expected, JobPostingId.From(Known).ToString());
            Assert.Equal(expected, BrowserProfileId.From(Known).ToString());
        }
    }
}
