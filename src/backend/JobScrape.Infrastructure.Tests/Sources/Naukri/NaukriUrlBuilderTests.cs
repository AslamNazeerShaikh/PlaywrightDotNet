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
        [InlineData("C#", "csharp")]
        [InlineData(".NET Core", "dotnet-core")]
        [InlineData("asp.net core c#", "aspdotnet-core-csharp")]
        [InlineData("Pune", "pune")]
        [InlineData("Pune, Mumbai", "pune-mumbai")]
        [InlineData("Delhi / NCR", "delhi-ncr")]
        [InlineData("Mumbai (All Areas)", "mumbai-all-areas")]
        [InlineData("  Full-Stack   Developer  ", "full-stack-developer")]
        [InlineData("C++", "c")]
        [InlineData("Zürich", "zrich")]
        [InlineData("--", "")]
        [InlineData("", "")]
        public void SlugFollowsPlaybookRules(string text, string expected)
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
                "https://www.naukri.com/dot-net-developer-jobs-in-pune?k=dot%20net%20developer&l=Pune%2C%20Bengaluru&experience=5&jobAge=7",
                url.AbsoluteUri
            );
        }

        [Fact]
        public void SearchUrlOmitsAbsentFiltersAndEncodesKeywords()
        {
            var criteria = new SearchCriteria("C#", ["Pune"], null, null, remoteOnly: false, []);

            var url = NaukriUrlBuilder.SearchUrl(criteria);

            Assert.Equal(
                "https://www.naukri.com/csharp-jobs-in-pune?k=C%23&l=Pune",
                url.AbsoluteUri
            );
        }

        [Fact]
        public void RemoteOnlySearchHasNoCityAndNoInventedRemoteFilter()
        {
            var criteria = new SearchCriteria("asp.net core", [], 3, null, remoteOnly: true, []);

            var url = NaukriUrlBuilder.SearchUrl(criteria);

            Assert.Equal(
                "https://www.naukri.com/aspdotnet-core-jobs?k=asp.net%20core&experience=3",
                url.AbsoluteUri
            );
        }

        [Fact]
        public void KeywordsWithoutSlugCharactersFallBackToCityPath()
        {
            var criteria = new SearchCriteria("++", ["Delhi / NCR"], null, 1, remoteOnly: false, []);

            var url = NaukriUrlBuilder.SearchUrl(criteria);

            Assert.Equal(
                "https://www.naukri.com/jobs-in-delhi-ncr?k=%2B%2B&l=Delhi%20%2F%20NCR&jobAge=1",
                url.AbsoluteUri
            );
        }

        [Fact]
        public void SearchUrlIsNeverAPagedSlugUrl()
        {
            // Playbook §3.5: pages 2+ come from clicking Next, never "-2" URLs.
            var criteria = new SearchCriteria(".net", ["Pune", "Mumbai"], 2, 30, remoteOnly: false, []);

            var url = NaukriUrlBuilder.SearchUrl(criteria);

            Assert.Equal("https", url.Scheme);
            Assert.Equal("www.naukri.com", url.Host);
            Assert.DoesNotMatch(new Regex(@"-\d+$"), url.AbsolutePath);
            Assert.DoesNotContain("page=", url.Query, StringComparison.Ordinal);
        }

        [Fact]
        public void NullArgumentsThrow()
        {
            Assert.Throws<ArgumentNullException>(() => NaukriUrlBuilder.Slug(null!));
            Assert.Throws<ArgumentNullException>(() => NaukriUrlBuilder.SearchUrl(null!));
        }
    }
}
