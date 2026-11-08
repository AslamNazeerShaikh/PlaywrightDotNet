using JobScrape.Domain.Sourcing;
using JobScrape.Infrastructure.Sources.Naukri;
using Xunit;

namespace JobScrape.Infrastructure.Tests.Sources.Naukri
{
    public sealed class NaukriJobSourceTests
    {
        private static Uri Page1Url() =>
            NaukriUrlBuilder.SearchUrl(
                new SearchCriteria("dot net developer", ["Pune"], 5, 7, remoteOnly: false, [])
            );

        // Live address bar keeps the page-1 path with -N appended before the query.
        private const string Page2Url =
            "https://www.naukri.com/dot-net-developer-jobs-in-pune-2?k=dot%20net%20developer&l=Pune&nignbevent_src=jobsearchDeskGNB&experience=5&jobAge=7";
        private const string Page3Url =
            "https://www.naukri.com/dot-net-developer-jobs-in-pune-3?k=dot%20net%20developer&l=Pune&nignbevent_src=jobsearchDeskGNB&experience=5&jobAge=7";

        [Fact]
        public void IsExpectedPageUrlPassesPage1Always()
        {
            Assert.True(NaukriJobSource.IsExpectedPageUrl(Page1Url().AbsoluteUri, Page1Url(), 1));
            Assert.True(
                NaukriJobSource.IsExpectedPageUrl(
                    "https://www.naukri.com/jobs-in-delhi-ncr?k=other",
                    Page1Url(),
                    1
                )
            );
        }

        [Theory]
        [InlineData(2, Page2Url)]
        [InlineData(3, Page3Url)]
        public void IsExpectedPageUrlPassesRealSlugSuffix(int page, string url)
        {
            Assert.True(NaukriJobSource.IsExpectedPageUrl(url, Page1Url(), page));
        }

        [Fact]
        public void IsExpectedPageUrlFailsWrongSuffix()
        {
            Assert.False(NaukriJobSource.IsExpectedPageUrl(Page3Url, Page1Url(), 2));
        }

        [Fact]
        public void IsExpectedPageUrlFailsRedirectToPage1()
        {
            Assert.False(NaukriJobSource.IsExpectedPageUrl(Page1Url().AbsoluteUri, Page1Url(), 2));
        }

        [Fact]
        public void IsExpectedPageUrlIgnoresQueryString()
        {
            Assert.True(
                NaukriJobSource.IsExpectedPageUrl(
                    "https://www.naukri.com/dot-net-developer-jobs-in-pune-2?k=other&l=Mumbai",
                    Page1Url(),
                    2
                )
            );
            Assert.True(
                NaukriJobSource.IsExpectedPageUrl(
                    "https://www.naukri.com/dot-net-developer-jobs-in-pune-2",
                    Page1Url(),
                    2
                )
            );
        }

        [Fact]
        public void IsExpectedPageUrlMatchesPathCaseInsensitively()
        {
            Assert.True(
                NaukriJobSource.IsExpectedPageUrl(
                    "https://www.naukri.com/DOT-NET-DEVELOPER-JOBS-IN-PUNE-2?k=dot%20net%20developer",
                    Page1Url(),
                    2
                )
            );
        }
    }
}
