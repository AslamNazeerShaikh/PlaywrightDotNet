using JobScrape.Infrastructure.Sources.Indeed;
using Xunit;
using static JobScrape.Infrastructure.Tests.Sources.Indeed.IndeedTestPages;

namespace JobScrape.Infrastructure.Tests.Sources.Indeed
{
    public sealed class IndeedDetailParserTests
    {
        private const string DetailUrl = "https://in.indeed.com/viewjob?jk=abc123def000XYZa";

        [Fact]
        public void NormalDetailExtractsIdentityAndFields()
        {
            var detail = IndeedDetailParser.Parse(Fixture("detail-normal.html"), DetailUrl, Now);

            Assert.NotNull(detail);
            var posting = AssertAccepted(detail);
            Assert.Equal("abc123def000XYZa", detail.ExternalJobId);
            Assert.Equal("https://in.indeed.com/viewjob?jk=abc123def000XYZa", detail.Url);
            Assert.Equal("Senior .NET Developer", detail.Title);
            Assert.Equal("Contoso", detail.Company);
            Assert.Equal("Pune, Maharashtra", detail.Location?.Raw);
            Assert.Equal(new DateTimeOffset(2026, 10, 28, 0, 0, 0, TimeSpan.Zero), detail.PostedAt);
            Assert.Contains("Build services in C# and .NET.", detail.Description);
            Assert.Equal(["C#", ".NET", "Azure"], detail.Skills);
            Assert.Equal("https://careers.contoso.example/apply/123", detail.ApplyUrl);
            Assert.Equal(detail.ExternalJobId, posting.ExternalJobId.Value);
        }

        [Fact]
        public void MinimalDetailKeepsWhatItSees()
        {
            var detail = IndeedDetailParser.Parse(
                Fixture("detail-minimal.html"),
                "https://in.indeed.com/viewjob?jk=xyz987min001&utm_source=feed",
                Now
            );

            Assert.NotNull(detail);
            Assert.Equal("xyz987min001", detail.ExternalJobId);
            Assert.Equal("https://in.indeed.com/viewjob?jk=xyz987min001", detail.Url);
            Assert.Equal("Junior .NET Developer", detail.Title);
            Assert.Null(detail.Company);
            Assert.Equal("Remote", detail.Location?.Raw);
            Assert.Equal(Domain.Sourcing.WorkMode.Remote, detail.Location?.WorkMode);
            Assert.Contains("Learn C#", detail.Description);
            Assert.True(detail.LowConfidence);
            AssertAccepted(detail);
        }

        [Fact]
        public void TrackingQueryIsDroppedFromCanonicalUrl()
        {
            var detail = IndeedDetailParser.Parse(
                Fixture("detail-minimal.html"),
                "https://in.indeed.com/viewjob?jk=xyz987min001&from=serp&vjs=3",
                Now
            );

            Assert.Equal("https://in.indeed.com/viewjob?jk=xyz987min001", detail?.Url);
        }

        [Fact]
        public void BlockPageYieldsNoDetail()
        {
            Assert.Null(
                IndeedDetailParser.Parse(Fixture("search-cloudflare.html"), DetailUrl, Now)
            );
        }
    }
}
