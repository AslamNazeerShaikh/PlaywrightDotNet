using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using JobScrape.Infrastructure.Sources.Naukri;
using Xunit;
using static JobScrape.Infrastructure.Tests.Sources.Naukri.NaukriTestPages;

namespace JobScrape.Infrastructure.Tests.Sources.Naukri
{
    public sealed class NaukriDetailParserTests
    {
        private const string NormalUrl =
            "https://www.naukri.com/job-listings-senior-dotnet-developer-contoso-pune-gurugram-5-to-8-years-011126000001?src=jobsearchDesk&utm_source=feed";

        private const string MinimalUrl =
            "https://www.naukri.com/job-listings-data-engineer-pune-2-to-4-years-011126000077?src=simjobsjd";

        private static string JsonLdPage(string jsonLd, string body = "")
        {
            return $"""
                <html><head><script type="application/ld+json">{jsonLd}</script></head>
                <body>{body}</body></html>
                """;
        }

        [Fact]
        public void ReadsJsonLdFirstWithDomForWhatItLacks()
        {
            var detail = NaukriDetailParser.Parse(Fixture("detail-normal.html"), NormalUrl, Now);

            Assert.NotNull(detail);
            Assert.Same(SourceKey.Naukri, detail.Source);
            Assert.Equal("011126000001", detail.ExternalJobId);
            Assert.Equal(
                "https://www.naukri.com/job-listings-senior-dotnet-developer-contoso-pune-gurugram-5-to-8-years-011126000001",
                detail.Url
            );
            Assert.Equal("Senior .NET Developer", detail.Title);
            Assert.Equal("Contoso", detail.Company);
            Assert.Equal("https://www.naukri.com/contoso-jobs-careers-1001", detail.CompanyUrl);
            Assert.Equal("5 - 8 years", detail.ExperienceRaw);
            Assert.Equal(5, detail.ExperienceMinYears);
            Assert.Equal(8, detail.ExperienceMaxYears);
            Assert.Equal("15-27.5 Lacs PA", detail.SalaryRaw);
            Assert.Equal(
                new SalaryRange(1_500_000m, 2_750_000m, "INR", SalaryPeriod.Year),
                detail.Salary
            );
            Assert.Equal("5 days ago", detail.PostedRaw);
            Assert.Equal(new DateTimeOffset(2026, 10, 27, 0, 0, 0, TimeSpan.Zero), detail.PostedAt);
            Assert.Equal(["C#", "ASP.NET Core", "Azure", "SQL Server"], detail.Skills);
            Assert.False(detail.LowConfidence);
            Assert.Null(detail.Snippet);

            var location = Assert.IsType<JobLocation>(detail.Location);
            Assert.Equal("Hybrid - Pune, Gurugram", location.Raw);
            Assert.Equal("Pune", location.City);
            Assert.Equal(WorkMode.Hybrid, location.WorkMode);
        }

        [Fact]
        public void DescriptionHtmlBecomesReadablePlainText()
        {
            var detail = NaukriDetailParser.Parse(Fixture("detail-normal.html"), NormalUrl, Now);

            Assert.Equal(
                "Role Overview\nBuild & ship APIs with ASP.NET Core.\n- Design services\n- Review code\nExperience: 5+ Years\nLocation: Pune",
                detail?.Description
            );
        }

        [Fact]
        public void OnlyAVisibleExternalApplyLinkBecomesApplyUrl()
        {
            // The fixture has Naukri's internal apply button, a hidden trap
            // link, and an internal "apply agent" link before the real one.
            var detail = NaukriDetailParser.Parse(Fixture("detail-normal.html"), NormalUrl, Now);

            Assert.Equal("https://careers.contoso.example/jobs/42?ref=naukri", detail?.ApplyUrl);
        }

        [Fact]
        public void MinimalPageParsesWithoutThrowingAndLeavesMissingFieldsNull()
        {
            var detail = NaukriDetailParser.Parse(Fixture("detail-minimal.html"), MinimalUrl, Now);

            Assert.NotNull(detail);
            Assert.Equal("011126000077", detail.ExternalJobId);
            Assert.Equal(
                "https://www.naukri.com/job-listings-data-engineer-pune-2-to-4-years-011126000077",
                detail.Url
            );
            Assert.Equal("Data Engineer", detail.Title);
            Assert.Equal("Work on data pipelines.\nOwn the ingestion layer.", detail.Description);
            Assert.Equal(["Python", "Spark"], detail.Skills);
            Assert.Null(detail.Company);
            Assert.Null(detail.CompanyUrl);
            Assert.Null(detail.SalaryRaw);
            Assert.Null(detail.Salary);
            Assert.Null(detail.Location);
            Assert.Null(detail.ExperienceRaw);
            Assert.Null(detail.ExperienceMinYears);
            Assert.Null(detail.PostedRaw);
            Assert.Null(detail.PostedAt);
            Assert.Null(detail.ApplyUrl);
            Assert.True(detail.LowConfidence);
        }

        [Theory]
        [InlineData("detail-normal.html", NormalUrl)]
        [InlineData("detail-minimal.html", MinimalUrl)]
        public void DetailIsAcceptedOnItsOwnAndAsARefresh(string fixture, string url)
        {
            var detail = NaukriDetailParser.Parse(Fixture(fixture), url, Now);
            Assert.NotNull(detail);

            AssertAccepted(detail);

            // RunScrapeHandler.TryMerge refreshes the posting the search card created.
            var fromCard = JobPosting.Create(
                new JobSnapshot
                {
                    Source = SourceKey.Naukri,
                    ExternalJobId = detail.ExternalJobId,
                    Url = url,
                    Title = "Card title",
                },
                Now
            );
            fromCard.Refresh(detail, Now.AddMinutes(5));
            Assert.Equal(detail.Title, fromCard.Title.Value);
            Assert.NotNull(fromCard.Description);
        }

        [Fact]
        public void SearchCardAndDetailOfTheSameJobShareIdentity()
        {
            var card = NaukriSearchParser.Parse(Fixture("search-normal.html"), Now).Jobs[0];
            var detail = NaukriDetailParser.Parse(Fixture("detail-normal.html"), NormalUrl, Now);
            Assert.NotNull(detail);

            var posting = JobPosting.Create(card, Now);
            posting.Refresh(detail, Now.AddMinutes(1));

            Assert.Equal(card.ExternalJobId, posting.ExternalJobId.Value);
            Assert.Equal("Senior .NET Developer", posting.Title.Value);
            Assert.Equal("https://careers.contoso.example/jobs/42?ref=naukri", posting.ApplyUrl);
        }

        [Fact]
        public void CanonicalLinkIsUsedWhenTheUrlIsNotAJobPage()
        {
            var detail = NaukriDetailParser.Parse(
                Fixture("detail-normal.html"),
                "https://www.naukri.com/",
                Now
            );

            Assert.Equal(
                "https://www.naukri.com/job-listings-senior-dotnet-developer-contoso-pune-gurugram-5-to-8-years-011126000001",
                detail?.Url
            );
        }

        [Theory]
        [InlineData("search-challenge.html")]
        [InlineData("search-empty.html")]
        public void NonDetailPagesYieldNull(string fixture)
        {
            Assert.Null(NaukriDetailParser.Parse(Fixture(fixture), MinimalUrl, Now));
        }

        [Fact]
        public void NonNaukriUrlWithoutCanonicalYieldsNull()
        {
            Assert.Null(
                NaukriDetailParser.Parse(
                    Fixture("detail-minimal.html"),
                    "https://evil.example/job-listings-data-engineer-011126000077",
                    Now
                )
            );
        }

        [Fact]
        public void JsonLdIdentifierWinsOverTheUrlSuffix()
        {
            // A redirect to another job must not be merged into the first one.
            var html = JsonLdPage(
                """{"@type":"JobPosting","title":"Other job","identifier":{"value":"011126000999"}}"""
            );

            var detail = NaukriDetailParser.Parse(html, MinimalUrl, Now);

            Assert.Equal("011126000999", detail?.ExternalJobId);
        }

        [Fact]
        public void NumericJsonLdSalaryAndMonthsOfExperienceAreUsed()
        {
            var html = JsonLdPage(
                """
                {"@context":"http://schema.org","@graph":[{"@type":"Organization","name":"x"},{"@type":["JobPosting"],
                 "title":"Platform Engineer","identifier":{"value":"011126000078"},
                 "experienceRequirements":{"monthsOfExperience":36},
                 "jobLocationType":"TELECOMMUTE",
                 "jobLocation":[{"address":{"addressLocality":"Bengaluru"}},{"address":{"addressLocality":["-","Pune"]}}],
                 "skills":"Go; Kubernetes, go",
                 "baseSalary":{"currency":"inr","value":{"minValue":1800000,"maxValue":2400000,"unitText":"YEAR"}}}]}
                """
            );

            var detail = NaukriDetailParser.Parse(html, MinimalUrl, Now);

            Assert.NotNull(detail);
            Assert.Equal(
                new SalaryRange(1_800_000m, 2_400_000m, "INR", SalaryPeriod.Year),
                detail.Salary
            );
            Assert.Null(detail.SalaryRaw);
            Assert.Equal(3, detail.ExperienceMinYears);
            Assert.Null(detail.ExperienceMaxYears);
            Assert.Equal(["Go", "Kubernetes"], detail.Skills);
            Assert.Equal("Bengaluru, Pune", detail.Location?.Raw);
            Assert.Equal(WorkMode.Remote, detail.Location?.WorkMode);
            AssertAccepted(detail);
        }

        [Fact]
        public void NotDisclosedJsonLdSalaryKeepsRawOnly()
        {
            var html = JsonLdPage(
                """{"@type":"JobPosting","title":"Analyst","identifier":{"value":"011126000079"},"baseSalary":{"currency":"INR","value":{"value":"Not disclosed","unitText":"P.A."}}}"""
            );

            var detail = NaukriDetailParser.Parse(html, MinimalUrl, Now);

            Assert.Equal("Not disclosed", detail?.SalaryRaw);
            Assert.Null(detail?.Salary);
        }

        [Fact]
        public void JsonLdWithRawControlCharactersStillParses()
        {
            var html = JsonLdPage(
                "{\"@type\":\"JobPosting\",\"title\":\"Tester\",\"identifier\":{\"value\":\"011126000080\"},\"description\":\"<p>Line one\nLine\ttwo</p>\"}"
            );

            var detail = NaukriDetailParser.Parse(html, MinimalUrl, Now);

            Assert.Equal("Tester", detail?.Title);
            Assert.Equal("Line one Line two", detail?.Description);
            Assert.False(detail?.LowConfidence);
        }

        [Fact]
        public void BrokenJsonLdFallsBackToDom()
        {
            var html =
                """<html><head><script type="application/ld+json">{"@type":"JobPosting","title":</script></head><body>"""
                + """<h1 class="styles_jd-header-title__x">Fallback Title</h1></body></html>""";

            var detail = NaukriDetailParser.Parse(html, MinimalUrl, Now);

            Assert.Equal("Fallback Title", detail?.Title);
            Assert.True(detail?.LowConfidence);
        }

        [Fact]
        public void EntityEncodedDescriptionIsDecoded()
        {
            var html = JsonLdPage(
                """{"@type":"JobPosting","title":"Dev","identifier":{"value":"011126000081"},"description":"&lt;p&gt;First&lt;/p&gt;&lt;p&gt;Second &amp;amp; third&lt;/p&gt;"}"""
            );

            var detail = NaukriDetailParser.Parse(html, MinimalUrl, Now);

            Assert.Equal("First\nSecond & third", detail?.Description);
        }

        [Fact]
        public void OverlongDescriptionIsCappedToTheDomainLimit()
        {
            var text = new string('a', 30_000);
            var html = JsonLdPage(
                $$"""{"@type":"JobPosting","title":"Dev","identifier":{"value":"011126000082"},"description":"<p>{{text}}</p>"}"""
            );

            var detail = NaukriDetailParser.Parse(html, MinimalUrl, Now);

            Assert.NotNull(detail);
            Assert.Equal(JobPosting.MaxDescriptionLength, detail.Description?.Length);
            AssertAccepted(detail);
        }

        [Theory]
        [InlineData("")]
        [InlineData("<<<>>>")]
        [InlineData("<script type=\"application/ld+json\">[[[[[[</script>")]
        [InlineData(
            "<script type=\"application/ld+json\">{\"@type\":\"JobPosting\",\"title\":\"\\u0000\",\"identifier\":{\"value\":\"x y\"}}</script>"
        )]
        [InlineData(
            "<script type=\"application/ld+json\">{\"@type\":\"JobPosting\",\"title\":\"T\",\"baseSalary\":{\"currency\":\"rupees\",\"value\":{\"minValue\":-5,\"maxValue\":1e30,\"unitText\":\"YEAR\"}},\"datePosted\":\"yesterday\"}</script>"
        )]
        public void NeverThrowsForHostileMarkup(string html)
        {
            var detail = NaukriDetailParser.Parse(html, MinimalUrl, Now);

            if (detail is not null)
            {
                AssertAccepted(detail);
            }
        }

        [Fact]
        public void NullArgumentsAreProgrammerErrors()
        {
            Assert.Throws<ArgumentNullException>(() =>
                NaukriDetailParser.Parse(null!, MinimalUrl, Now)
            );
            Assert.Throws<ArgumentNullException>(() => NaukriDetailParser.Parse("", null!, Now));
        }
    }
}
