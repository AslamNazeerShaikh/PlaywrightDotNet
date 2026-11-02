using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using Xunit;

namespace JobScrape.Domain.Tests.Sourcing
{
    public sealed class CanonicalUrlTests
    {
        // The three examples from docs/08-site-playbooks.md §2.2, verbatim.
        public static TheoryData<string, SourceKey, string> PlaybookExamples =>
            new()
            {
                {
                    "https://www.naukri.com/job-listings-senior-dot-net-developer-contoso-pune-123456789?utm_source=feed",
                    SourceKey.Naukri,
                    "https://www.naukri.com/job-listings-senior-dot-net-developer-contoso-pune-123456789"
                },
                {
                    "https://www.linkedin.com/jobs/view/4012345678/?refId=abc&trackingId=xyz",
                    SourceKey.LinkedIn,
                    "https://www.linkedin.com/jobs/view/4012345678"
                },
                {
                    "https://in.indeed.com/viewjob?jk=abcdef123456&from=serp&vjs=3",
                    SourceKey.Indeed,
                    "https://in.indeed.com/viewjob?jk=abcdef123456"
                },
            };

        public static TheoryData<string?, SourceKey> Rejected =>
            new()
            {
                { null, SourceKey.Naukri },
                { "   ", SourceKey.Naukri },
                { "javascript:alert(1)", SourceKey.Naukri },
                { "data:text/html,hi", SourceKey.Naukri },
                // Parses as file:///relative/path on Unix; rejected as non-https.
                { "/relative/path", SourceKey.Naukri },
                { "relative/path", SourceKey.Naukri },
                { "not a url", SourceKey.Naukri },
                { "http://www.naukri.com/job-listings-x-1", SourceKey.Naukri },
                { "https://evil.example/job-listings-x-1", SourceKey.Naukri },
                { "https://www.naukri.com.evil.example/job-listings-x-1", SourceKey.Naukri },
                { "https://user:pass@www.naukri.com/job-listings-x-1", SourceKey.Naukri },
                { "https://www.naukri.com:8443/job-listings-x-1", SourceKey.Naukri },
                { "https://www.naukri.com/job-listings-x-1", SourceKey.Indeed },
                { "https://in.indeed.com/viewjob?jk=1", SourceKey.LinkedIn },
            };

        [Theory]
        [MemberData(nameof(PlaybookExamples))]
        public void CanonicalizesPlaybookExamples(string input, SourceKey source, string expected)
        {
            Assert.Equal(expected, CanonicalUrl.Create(input, source).Value);
        }

        [Theory]
        [MemberData(nameof(Rejected))]
        public void RejectsUnsafeOrForeignUrls(string? input, SourceKey source)
        {
            Assert.Throws<DomainException>(() => CanonicalUrl.Create(input, source));
        }

        [Fact]
        public void StripsFragmentAndLowercasesHost()
        {
            var url = CanonicalUrl.Create(
                "  https://WWW.Naukri.com/job-listings-x-123#apply  ",
                SourceKey.Naukri
            );

            Assert.Equal("https://www.naukri.com/job-listings-x-123", url.Value);
            Assert.Equal(url.Value, url.ToString());
        }

        [Fact]
        public void AcceptsExplicitDefaultHttpsPort()
        {
            var url = CanonicalUrl.Create(
                "https://www.naukri.com:443/job-listings-x-1",
                SourceKey.Naukri
            );

            Assert.Equal("https://www.naukri.com/job-listings-x-1", url.Value);
        }

        [Fact]
        public void KeepsRootPath()
        {
            Assert.Equal(
                "https://in.indeed.com/?jk=abc",
                CanonicalUrl.Create("https://in.indeed.com/?utm_x=1&jk=abc", SourceKey.Indeed).Value
            );
        }

        [Fact]
        public void IndeedJkSurvivesInAnyPositionAndOtherParamsDrop()
        {
            var url = CanonicalUrl.Create(
                "https://in.indeed.com/viewjob?from=serp&jk=abc&&vjs=3",
                SourceKey.Indeed
            );

            Assert.Equal("https://in.indeed.com/viewjob?jk=abc", url.Value);
        }

        [Fact]
        public void JkIsNotSignificantForOtherSources()
        {
            var url = CanonicalUrl.Create(
                "https://www.naukri.com/job-listings-x-1?jk=abc",
                SourceKey.Naukri
            );

            Assert.Equal("https://www.naukri.com/job-listings-x-1", url.Value);
        }

        [Fact]
        public void RejectsUrlOverLengthLimit()
        {
            var url = "https://www.naukri.com/" + new string('a', CanonicalUrl.MaxLength);

            Assert.Throws<DomainException>(() => CanonicalUrl.Create(url, SourceKey.Naukri));
        }

        [Fact]
        public void RejectsNullSource()
        {
            Assert.Throws<ArgumentNullException>(() =>
                CanonicalUrl.Create("https://www.naukri.com/x", null!)
            );
        }

        [Fact]
        public void EqualityIsByValue()
        {
            Assert.Equal(
                CanonicalUrl.Create(
                    "https://www.naukri.com/job-listings-x-1?utm=1",
                    SourceKey.Naukri
                ),
                CanonicalUrl.Create("https://www.naukri.com/job-listings-x-1/", SourceKey.Naukri)
            );
        }
    }
}
