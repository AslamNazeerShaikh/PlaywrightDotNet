using JobScrape.Domain.Sourcing;
using JobScrape.Infrastructure.Sources.LinkedIn;
using Xunit;

namespace JobScrape.Infrastructure.Tests.Sources.LinkedIn
{
    public sealed class LinkedInUrlBuilderTests
    {
        private static SearchCriteria Criteria(
            string keywords = "dotnet",
            string[]? locations = null,
            int? postedWithinDays = 7
        )
        {
            return new SearchCriteria(
                keywords,
                locations ?? ["Pune"],
                null,
                postedWithinDays,
                false,
                []
            );
        }

        [Fact]
        public void GuestSearchCarriesWeeklyTimeFilterInSeconds()
        {
            var url = LinkedInUrlBuilder.SearchUrl(Criteria()).AbsoluteUri;

            Assert.Equal(
                "https://www.linkedin.com/jobs/search?keywords=dotnet&location=Pune&f_TPR=r604800",
                url
            );
        }

        [Fact]
        public void LaterPagesCarryStartInStepsOfTwentyFive()
        {
            var url = LinkedInUrlBuilder.SearchUrl(Criteria(), 25).AbsoluteUri;

            Assert.Contains("start=25", url);
        }

        [Fact]
        public void MissingFromageIsOmitted()
        {
            var url = LinkedInUrlBuilder.SearchUrl(Criteria(postedWithinDays: null)).AbsoluteUri;

            Assert.DoesNotContain("f_TPR", url);
        }

        [Fact]
        public void NegativeStartIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                LinkedInUrlBuilder.SearchUrl(Criteria(), -25)
            );
        }
    }
}
