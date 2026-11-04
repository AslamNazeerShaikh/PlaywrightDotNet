using System.Text.RegularExpressions;
using JobScrape.Domain.Sourcing;
using JobScrape.Infrastructure.Sources.Naukri;
using Xunit;

namespace JobScrape.Infrastructure.Tests.Sources.Naukri
{
    public sealed class NaukriUrlBuilderTests
    {
        [Theory]
        [InlineData("dot net developer", "dot-net-developer")]
        [InlineData("C#", "c-sharp")]
        [InlineData(".NET Core", "dot-net-core")]
        [InlineData("asp.net core c#", "asp-dot-net-core-c-sharp")]
        [InlineData("ASP.Net, ASP.Net Core, C#", "asp-dot-net-asp-dot-net-core-c-sharp")]
        [InlineData("Pune", "pune")]
        [InlineData("Pune, Mumbai", "pune-mumbai")]
        [InlineData("Delhi / NCR", "delhi-ncr")]
        [InlineData("Mumbai (All Areas)", "mumbai-all-areas")]
        [InlineData("  Full-Stack   Developer  ", "full-stack-developer")]
        [InlineData("C++", "c")]
        [InlineData("Zürich", "zrich")]
        [InlineData("--", "")]
        [InlineData("", "")]
        public void SlugFollowsNaukriCanonicalRules(string text, string expected)
        {
            Assert.Equal(expected, NaukriUrlBuilder.Slug(text));
        }

        [Fact]
        public void SearchUrlUsesFirstCityInPathAndAllCitiesInQuery()
        {
            var criteria = new SearchCriteria(
                "dot net developer",
                ["Pune", "Bengaluru"],
                5,
                7,
                remoteOnly: false,
                []
            );

            var url = NaukriUrlBuilder.SearchUrl(criteria);

            Assert.Equal(
                "https://www.naukri.com/dot-net-developer-jobs-in-pune?k=dot%20net%20developer&l=Pune%2C%20Bengaluru&nignbevent_src=jobsearchDeskGNB&experience=5&jobAge=7",
                url.AbsoluteUri
            );
        }

        [Fact]
        public void SearchUrlMatchesTheLiveCanonicalPathThatKeepsFilters()
        {
            var criteria = new SearchCriteria(
                "asp.net",
                ["Pune", "Hyderabad", "Chennai", "Mumbai", "Bangalore", "Noida"],
                6,
                1,
                remoteOnly: false,
                []
            );

            var url = NaukriUrlBuilder.SearchUrl(criteria);

            Assert.Equal(
                "https://www.naukri.com/asp-dot-net-jobs-in-pune?k=asp.net&l=Pune%2C%20Hyderabad%2C%20Chennai%2C%20Mumbai%2C%20Bangalore%2C%20Noida&nignbevent_src=jobsearchDeskGNB&experience=6&jobAge=1",
                url.AbsoluteUri
            );
        }

        [Fact]
        public void SearchUrlOmitsAbsentFiltersAndEncodesKeywords()
        {
            var criteria = new SearchCriteria("C#", ["Pune"], null, null, remoteOnly: false, []);

            var url = NaukriUrlBuilder.SearchUrl(criteria);

            Assert.Equal(
                "https://www.naukri.com/c-sharp-jobs-in-pune?k=C%23&l=Pune&nignbevent_src=jobsearchDeskGNB",
                url.AbsoluteUri
            );
        }

        [Fact]
        public void RemoteOnlySearchHasNoCityAndNoInventedRemoteFilter()
        {
            var criteria = new SearchCriteria("asp.net core", [], 3, null, remoteOnly: true, []);

            var url = NaukriUrlBuilder.SearchUrl(criteria);

            Assert.Equal(
                "https://www.naukri.com/asp-dot-net-core-jobs?k=asp.net%20core&nignbevent_src=jobsearchDeskGNB&experience=3",
                url.AbsoluteUri
            );
        }

        [Fact]
        public void KeywordsWithoutSlugCharactersFallBackToCityPath()
        {
            var criteria = new SearchCriteria(
                "++",
                ["Delhi / NCR"],
                null,
                1,
                remoteOnly: false,
                []
            );

            var url = NaukriUrlBuilder.SearchUrl(criteria);

            Assert.Equal(
                "https://www.naukri.com/jobs-in-delhi-ncr?k=%2B%2B&l=Delhi%20%2F%20NCR&nignbevent_src=jobsearchDeskGNB&jobAge=1",
                url.AbsoluteUri
            );
        }

        [Fact]
        public void PageOneBuildsTheBareSlugUrl()
        {
            var criteria = new SearchCriteria(
                ".net",
                ["Pune", "Mumbai"],
                2,
                30,
                remoteOnly: false,
                []
            );

            var url = NaukriUrlBuilder.SearchUrl(criteria, 1);

            Assert.Equal("https", url.Scheme);
            Assert.Equal("www.naukri.com", url.Host);
            Assert.DoesNotMatch(new Regex(@"-\d+$"), url.AbsolutePath);
            Assert.DoesNotContain("page=", url.Query, StringComparison.Ordinal);
        }

        [Fact]
        public void LaterPagesSuffixThePathSlugAndKeepTheQuery()
        {
            var criteria = new SearchCriteria(
                "asp.net",
                ["Pune", "Hyderabad"],
                6,
                1,
                remoteOnly: false,
                []
            );

            var url = NaukriUrlBuilder.SearchUrl(criteria, 3);

            Assert.Equal(
                "https://www.naukri.com/asp-dot-net-jobs-in-pune-3?k=asp.net&l=Pune%2C%20Hyderabad&nignbevent_src=jobsearchDeskGNB&experience=6&jobAge=1",
                url.AbsoluteUri
            );
        }

        [Fact]
        public void PageBelowOneIsRejected()
        {
            var criteria = new SearchCriteria("C#", ["Pune"], null, null, remoteOnly: false, []);

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                NaukriUrlBuilder.SearchUrl(criteria, 0)
            );
        }

        [Fact]
        public void NullArgumentsThrow()
        {
            Assert.Throws<ArgumentNullException>(() => NaukriUrlBuilder.Slug(null!));
            Assert.Throws<ArgumentNullException>(() => NaukriUrlBuilder.SearchUrl(null!));
        }
    }
}
