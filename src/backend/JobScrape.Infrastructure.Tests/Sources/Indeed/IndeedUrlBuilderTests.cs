using JobScrape.Domain.Sourcing;
using JobScrape.Infrastructure.Sources.Indeed;
using Xunit;

namespace JobScrape.Infrastructure.Tests.Sources.Indeed
{
    public sealed class IndeedUrlBuilderTests
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
        public void FirstPageHasNoStartOffset()
        {
            var url = IndeedUrlBuilder.SearchUrl(Criteria()).AbsoluteUri;

            Assert.Equal(
                "https://in.indeed.com/jobs?q=dotnet&l=Pune&fromage=7",
                url
            );
        }

        [Fact]
        public void LaterPagesCarryStartInStepsOfTen()
        {
            var url = IndeedUrlBuilder.SearchUrl(Criteria(), 20).AbsoluteUri;

            Assert.Contains("start=20", url);
        }

        [Fact]
        public void MultiCityLocationsStayInQuery()
        {
            var url = IndeedUrlBuilder
                .SearchUrl(Criteria(locations: ["Pune", "Bengaluru"]))
                .AbsoluteUri;

            Assert.Contains("l=Pune%2C%20Bengaluru", url);
        }

        [Fact]
        public void NegativeStartIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => IndeedUrlBuilder.SearchUrl(Criteria(), -10)
            );
        }

        [Fact]
        public void MissingFromageIsOmitted()
        {
            var url = IndeedUrlBuilder.SearchUrl(Criteria(postedWithinDays: null)).AbsoluteUri;

            Assert.DoesNotContain("fromage", url);
        }
    }
}
