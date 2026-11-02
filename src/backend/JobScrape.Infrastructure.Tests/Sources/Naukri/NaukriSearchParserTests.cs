using System.Globalization;
using System.Text;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using JobScrape.Infrastructure.Sources.Naukri;
using Xunit;
using static JobScrape.Infrastructure.Tests.Sources.Naukri.NaukriTestPages;

namespace JobScrape.Infrastructure.Tests.Sources.Naukri
{
    public sealed class NaukriSearchParserTests
    {
        private static NaukriSearchPage ParseFixture(string name)
        {
            return NaukriSearchParser.Parse(Fixture(name), Now);
        }

        private static JobSnapshot ParseSingle(string card)
        {
            return Assert.Single(NaukriSearchParser.Parse(Page(card), Now).Jobs);
        }

        // ---- fixtures ----

        [Fact]
        public void ParsesTwentyCardsFromNormalFixture()
        {
            var page = ParseFixture("search-normal.html");

            Assert.Equal(20, page.Jobs.Count);
            Assert.Equal(20, page.Jobs.Select(job => job.ExternalJobId).Distinct().Count());
            Assert.Equal(0, page.DroppedCards);
            Assert.Equal("1 - 20 of 131", page.ResultRange);
            Assert.Equal("011126000001", page.FirstJobId);
            Assert.True(page.HasNext);
            Assert.All(page.Jobs, job => Assert.Same(SourceKey.Naukri, job.Source));
            Assert.All(page.Jobs, job => Assert.False(job.LowConfidence));
        }

        [Fact]
        public void ReadsEveryFieldOfAStandardCard()
        {
            var job = ParseFixture("search-normal.html").Jobs[0];

            Assert.Equal("011126000001", job.ExternalJobId);
            Assert.Equal(
                "https://www.naukri.com/job-listings-senior-dotnet-developer-contoso-011126000001",
                job.Url
            );
            Assert.Equal("Senior .NET Developer", job.Title);
            Assert.Equal("Contoso", job.Company);
            Assert.Equal("https://www.naukri.com/contoso-jobs-careers-0001", job.CompanyUrl);
            Assert.Equal("5-8 Yrs", job.ExperienceRaw);
            Assert.Equal(5, job.ExperienceMinYears);
            Assert.Equal(8, job.ExperienceMaxYears);
            Assert.Equal("15-27.5 Lacs PA", job.SalaryRaw);
            Assert.Equal(
                new SalaryRange(1_500_000m, 2_750_000m, "INR", SalaryPeriod.Year),
                job.Salary
            );
            Assert.Equal("5 days ago", job.PostedRaw);
            Assert.Equal(new DateTimeOffset(2026, 10, 27, 0, 0, 0, TimeSpan.Zero), job.PostedAt);
            Assert.Equal(
                "Design & build APIs on ASP.NET Core. Own services end to end...",
                job.Snippet
            );
            Assert.Null(job.Description);
            Assert.Null(job.ApplyUrl);

            var location = Assert.IsType<JobLocation>(job.Location);
            Assert.Equal("Hybrid - Pune, Gurugram, Bengaluru", location.Raw);
            Assert.Equal("Pune", location.City);
            Assert.Null(location.Region);
            Assert.Null(location.Country);
            Assert.Equal(WorkMode.Hybrid, location.WorkMode);
        }

        [Fact]
        public void ExtractsKeySkillsFromCardsWithoutDetailPages()
        {
            var job = ParseFixture("search-normal.html").Jobs[0];

            // The card repeats "c#"; the first spelling wins.
            Assert.Equal(["C#", "ASP.NET Core", "Azure", "SQL Server"], job.Skills);
        }

        [Fact]
        public void CanonicalUrlStripsTrackingAndKeepsNumericId()
        {
            var job = ParseFixture("search-normal.html").Jobs[1];

            Assert.Equal("011126000002", job.ExternalJobId);
            Assert.Equal(
                "https://www.naukri.com/job-listings-full-stack-dotnet-developer-fabrikam-011126000002",
                job.Url
            );
            Assert.Equal(job.Url, AssertAccepted(job).Url.Value);
        }

        [Fact]
        public void NotDisclosedSalaryKeepsRawTextOnly()
        {
            var job = ParseFixture("search-normal.html").Jobs[2];

            Assert.Equal("Not disclosed", job.SalaryRaw);
            Assert.Null(job.Salary);
            Assert.Equal(10, job.ExperienceMinYears);
            Assert.Null(job.ExperienceMaxYears);
            Assert.Equal(WorkMode.Remote, job.Location?.WorkMode);
            Assert.Null(job.Location?.City);
        }

        [Fact]
        public void CardWithoutSalaryHasNoSalaryFields()
        {
            var job = ParseFixture("search-normal.html").Jobs[1];

            Assert.Null(job.SalaryRaw);
            Assert.Null(job.Salary);
            Assert.Equal(WorkMode.Unknown, job.Location?.WorkMode);
        }

        [Theory]
        [InlineData("search-normal.html")]
        [InlineData("search-promoted.html")]
        [InlineData("search-honeypot.html")]
        [InlineData("search-last-page.html")]
        public void EverySnapshotIsAcceptedByTheDomain(string fixture)
        {
            var page = ParseFixture(fixture);

            Assert.NotEmpty(page.Jobs);
            foreach (var job in page.Jobs)
            {
                var posting = AssertAccepted(job);
                Assert.Equal(job.Title, posting.Title.Value);
                Assert.Equal(job.Skills.Count, posting.Skills.Count);
            }
        }

        [Fact]
        public void PromotedWidgetsAreNotParsed()
        {
            var page = ParseFixture("search-promoted.html");

            Assert.Equal(
                ["011126000101", "011126000102", "011126000103"],
                page.Jobs.Select(job => job.ExternalJobId)
            );
            Assert.Equal(0, page.DroppedCards);
            Assert.False(page.HasNext);
        }

        [Fact]
        public void HoneypotAndHiddenCardsAreIgnored()
        {
            var page = ParseFixture("search-honeypot.html");

            Assert.Equal(
                ["011126000201", "011126000202"],
                page.Jobs.Select(job => job.ExternalJobId)
            );
            Assert.DoesNotContain(
                page.Jobs,
                job => job.Title.StartsWith("Trap", StringComparison.Ordinal)
            );

            // Visible cards whose only link is off-host, javascript:, or hidden
            // are malformed; hidden cards are skipped without counting.
            Assert.Equal(3, page.DroppedCards);
        }

        [Fact]
        public void EmptyResultsPageYieldsNothingAndNoNext()
        {
            var page = ParseFixture("search-empty.html");

            Assert.Empty(page.Jobs);
            Assert.Equal(0, page.DroppedCards);
            Assert.Null(page.ResultRange);
            Assert.Null(page.FirstJobId);
            Assert.False(page.HasNext);
        }

        [Fact]
        public void ChallengePageYieldsZeroJobs()
        {
            var page = ParseFixture("search-challenge.html");

            Assert.Empty(page.Jobs);
            Assert.Equal(0, page.DroppedCards);
            Assert.False(page.HasNext);
        }

        [Fact]
        public void LastPageHasNoNextAndADifferentFingerprint()
        {
            var first = ParseFixture("search-normal.html");
            var last = ParseFixture("search-last-page.html");

            Assert.False(last.HasNext);
            Assert.Equal(11, last.Jobs.Count);
            Assert.Equal("121 - 131 of 131", last.ResultRange);
            Assert.NotEqual(
                new PageFingerprint(first.FirstJobId, first.ResultRange),
                new PageFingerprint(last.FirstJobId, last.ResultRange)
            );
        }

        // ---- field parsing ----

        [Theory]
        [InlineData("15-27.5 Lacs PA", 1_500_000, 2_750_000, SalaryPeriod.Year)]
        [InlineData("12-18 Lacs P.A.", 1_200_000, 1_800_000, SalaryPeriod.Year)]
        [InlineData("3.5-5 Lakhs PA", 350_000, 500_000, SalaryPeriod.Year)]
        [InlineData("25 Lacs PA", 2_500_000, 2_500_000, SalaryPeriod.Year)]
        [InlineData("1-1.5 Cr PA", 10_000_000, 15_000_000, SalaryPeriod.Year)]
        [InlineData("₹ 8 LPA", 800_000, 800_000, SalaryPeriod.Year)]
        [InlineData("5,00,000 - 8,00,000 P.A.", 500_000, 800_000, SalaryPeriod.Year)]
        [InlineData("50,000-60,000 PM", 50_000, 60_000, SalaryPeriod.Month)]
        public void ParsesSalary(string raw, int min, int max, SalaryPeriod period)
        {
            var job = ParseSingle(Card(salary: raw));

            Assert.Equal(raw, job.SalaryRaw);
            Assert.Equal(new SalaryRange(min, max, "INR", period), job.Salary);
            AssertAccepted(job);
        }

        [Theory]
        [InlineData("Not disclosed")]
        [InlineData("15-27.5 Lacs")]
        [InlineData("27.5-15 Lacs PA")]
        [InlineData("50,000 - 1 Lacs PA")]
        [InlineData("15-27.5 PA")]
        [InlineData("8 LPA PM")]
        [InlineData("Best in industry")]
        [InlineData("99999999999999999999999 Cr PA")]
        public void UnclearSalaryStaysNullButRawIsKept(string raw)
        {
            var job = ParseSingle(Card(salary: raw));

            Assert.Equal(raw, job.SalaryRaw);
            Assert.Null(job.Salary);
            AssertAccepted(job);
        }

        [Theory]
        [InlineData("5-8 Yrs", 5, 8)]
        [InlineData("0-5 Yrs", 0, 5)]
        [InlineData("10+ Yrs", 10, null)]
        [InlineData("3 Yrs", 3, 3)]
        [InlineData("4 - 9 years", 4, 9)]
        [InlineData("8-5 Yrs", null, null)]
        [InlineData("45-60 Yrs", null, null)]
        [InlineData("Fresher", null, null)]
        public void ParsesExperience(string raw, int? min, int? max)
        {
            var job = ParseSingle(Card(experience: raw));

            Assert.Equal(raw, job.ExperienceRaw);
            Assert.Equal(min, job.ExperienceMinYears);
            Assert.Equal(max, job.ExperienceMaxYears);
            AssertAccepted(job);
        }

        [Theory]
        [InlineData("Just now", "2026-11-01")]
        [InlineData("Today", "2026-11-01")]
        [InlineData("Few hours ago", "2026-11-01")]
        [InlineData("3 hours ago", "2026-11-01")]
        [InlineData("1 day ago", "2026-10-31")]
        [InlineData("Yesterday", "2026-10-31")]
        [InlineData("5 days ago", "2026-10-27")]
        [InlineData("5 Days Ago", "2026-10-27")]
        [InlineData("30 days ago", "2026-10-02")]
        [InlineData("30+ days ago", null)]
        [InlineData("Recently", null)]
        public void ParsesPostedDateAgainstRunTime(string raw, string? expected)
        {
            var job = ParseSingle(Card(posted: raw));

            Assert.Equal(raw, job.PostedRaw);
            Assert.Equal(
                expected is null
                    ? null
                    : DateTimeOffset.Parse(
                        expected + "T00:00:00Z",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal
                    ),
                job.PostedAt
            );
        }

        [Fact]
        public void PostedDateUsesTheUtcDayOfTheRunTime()
        {
            // 02:00 in India is still the previous day in UTC.
            var now = new DateTimeOffset(2026, 11, 1, 2, 0, 0, TimeSpan.FromHours(5.5));

            var job = Assert.Single(
                NaukriSearchParser.Parse(Page(Card(posted: "1 day ago")), now).Jobs
            );

            Assert.Equal(new DateTimeOffset(2026, 10, 30, 0, 0, 0, TimeSpan.Zero), job.PostedAt);
            Assert.Equal(TimeSpan.Zero, job.PostedAt?.Offset);
        }

        [Theory]
        [InlineData("Hybrid - Pune, Gurugram, Bengaluru", WorkMode.Hybrid, "Pune")]
        [InlineData("Pune", WorkMode.Unknown, "Pune")]
        [InlineData("Remote", WorkMode.Remote, null)]
        [InlineData("Work from home", WorkMode.Remote, null)]
        [InlineData("Temp. WFH - Bengaluru", WorkMode.Remote, "Bengaluru")]
        [InlineData("Work from office - Pune", WorkMode.Onsite, "Pune")]
        [InlineData("Pune, Bengaluru, Mumbai (All Areas)", WorkMode.Unknown, "Pune")]
        [InlineData("Mumbai (All Areas)", WorkMode.Unknown, "Mumbai")]
        [InlineData("Delhi / NCR", WorkMode.Unknown, "Delhi / NCR")]
        public void ParsesLocationAndWorkMode(string raw, WorkMode mode, string? city)
        {
            var job = ParseSingle(Card(location: raw));

            var location = Assert.IsType<JobLocation>(job.Location);
            Assert.Equal(raw, location.Raw);
            Assert.Equal(mode, location.WorkMode);
            Assert.Equal(city, location.City);
            Assert.Null(location.Country);
        }

        [Fact]
        public void MissingOptionalFieldsStayNull()
        {
            var job = ParseSingle(
                Card(experience: null, location: null, posted: null, company: null, skills: [])
            );

            Assert.Null(job.Company);
            Assert.Null(job.CompanyUrl);
            Assert.Null(job.Location);
            Assert.Null(job.ExperienceRaw);
            Assert.Null(job.ExperienceMinYears);
            Assert.Null(job.PostedRaw);
            Assert.Null(job.PostedAt);
            Assert.Empty(job.Skills);
            var posting = AssertAccepted(job);
            Assert.True(posting.QualityIssues.HasFlag(DataQualityIssues.MissingCompany));
        }

        // ---- identity and malformed cards ----

        [Fact]
        public void IdFallsBackToTheHrefSuffix()
        {
            var job = ParseSingle(
                Card(
                    id: "",
                    href: "https://www.naukri.com/job-listings-qa-engineer-litware-011126000777?src=x"
                )
            );

            Assert.Equal("011126000777", job.ExternalJobId);
            Assert.Equal(
                "https://www.naukri.com/job-listings-qa-engineer-litware-011126000777",
                job.Url
            );
        }

        [Fact]
        public void RootRelativeJobLinkIsResolvedToNaukri()
        {
            var job = ParseSingle(Card(href: "/job-listings-qa-engineer-litware-011126000501"));

            Assert.Equal(
                "https://www.naukri.com/job-listings-qa-engineer-litware-011126000501",
                job.Url
            );
        }

        [Theory]
        [InlineData("011126000601", null, null)]
        [InlineData("011126000602", "Title", "http://www.naukri.com/job-listings-x-011126000602")]
        [InlineData("011126000603", "Title", "https://www.naukri.com/jobs-in-pune")]
        [InlineData("011126000604", "Title", "https://naukri.com/job-listings-x-011126000604")]
        [InlineData("not-a-number", "Title", "https://www.naukri.com/job-listings-x")]
        [InlineData("011126000605", "   ", null)]
        public void MalformedCardIsDroppedAndCounted(string id, string? title, string? href)
        {
            var page = NaukriSearchParser.Parse(
                Page(Card(id: "011126000600"), Card(id: id, title: title, href: href)),
                Now
            );

            Assert.Equal("011126000600", Assert.Single(page.Jobs).ExternalJobId);
            Assert.Equal(1, page.DroppedCards);
        }

        [Fact]
        public void DuplicateCardsAreEmittedOnce()
        {
            var page = NaukriSearchParser.Parse(
                Page(Card(), Card(), Card(id: "011126000502")),
                Now
            );

            Assert.Equal(
                ["011126000501", "011126000502"],
                page.Jobs.Select(job => job.ExternalJobId)
            );
            Assert.Equal(0, page.DroppedCards);
        }

        [Fact]
        public void OverlongTitleIsCappedAndControlCharactersRemoved()
        {
            var job = ParseSingle(Card(title: "Lead\u0001 " + new string('x', 400)));

            Assert.Equal(JobTitle.MaxLength, job.Title.Length);
            Assert.StartsWith("Lead x", job.Title, StringComparison.Ordinal);
            AssertAccepted(job);
        }

        [Fact]
        public void TooManySkillsAreCappedToTheDomainLimit()
        {
            var skills = Enumerable.Range(1, 80).Select(n => $"Skill {n}").ToArray();

            var job = ParseSingle(Card(skills: skills));

            Assert.Equal(JobPosting.MaxSkills, job.Skills.Count);
            AssertAccepted(job);
        }

        [Fact]
        public void FallbackSelectorParsesIdCardsInResultsListWithLowConfidence()
        {
            const string html = """
                <ul aria-label="Job search results">
                  <li><article data-job-id="011126000901"><h2><a class="jobTitle" href="https://www.naukri.com/job-listings-renamed-card-011126000901">Renamed Card</a></h2></article></li>
                  <li><section class="naukri-wdgt"><article data-job-id="011126000902"><a class="title" href="https://www.naukri.com/job-listings-widget-011126000902">Widget</a></article></section></li>
                </ul>
                """;

            var page = NaukriSearchParser.Parse(html, Now);

            var job = Assert.Single(page.Jobs);
            Assert.Equal("011126000901", job.ExternalJobId);
            Assert.True(job.LowConfidence);
            AssertAccepted(job);
        }

        // ---- pagination ----

        [Theory]
        [InlineData("<a aria-disabled=\"false\" href=\"/x-2\"><span>Next</span></a>", true)]
        [InlineData("<a href=\"/x-2\"> Next </a>", true)]
        [InlineData("<a aria-disabled=\"true\"><span>Next</span></a>", false)]
        [InlineData("<a disabled=\"\"><span>Next</span></a>", false)]
        [InlineData("<a aria-disabled=\"false\" style=\"display:none\">Next</a>", false)]
        [InlineData("<button>Next</button>", false)]
        [InlineData("<a href=\"/x-2\">Next page of offers</a>", false)]
        [InlineData("", false)]
        public void DetectsAnEnabledNativeNextLink(string next, bool expected)
        {
            var page = NaukriSearchParser.Parse($"<html><body>{next}</body></html>", Now);

            Assert.Equal(expected, page.HasNext);
        }

        // ---- robustness ----

        public static TheoryData<string> HostileInputs()
        {
            var deep = new StringBuilder();
            for (var i = 0; i < 3_000; i++)
            {
                deep.Append("<div class=\"srp-jobtuple-wrapper\" data-job-id=\"1\">");
            }

            return new TheoryData<string>
            {
                string.Empty,
                "<",
                "<<<>>>",
                "\0\u0001\u0002 binary ￿",
                "<div class=\"srp-jobtuple-wrapper\" data-job-id=\"1\">",
                "<div class=\"srp-jobtuple-wrapper\"><a class=\"title\" href=\"javascript:alert(1)\">x</a></div>",
                "<div class=\"srp-jobtuple-wrapper\" data-job-id=\"9\" style=\"opacity:abc;display\"><a class=\"title\" href=\"https://www.naukri.com/job-listings-x-9\">x</a></div>",
                "<div class=\"srp-jobtuple-wrapper\"><a class=\"title\" href=\"https://www.naukri.com:8443/job-listings-x-9\">x</a></div>",
                "<div class=\"srp-jobtuple-wrapper\"><a class=\"title\" href=\"https://user:pw@www.naukri.com/job-listings-x-9\">x</a></div>",
                "<div class=\"srp-jobtuple-wrapper\"><a class=\"title\" href=\"https://www.naukri.com/job-listings-"
                    + new string('x', 5_000)
                    + "-9\">x</a></div>",
                deep.ToString(),
            };
        }

        [Theory]
        [MemberData(nameof(HostileInputs))]
        public void NeverThrowsForHostileMarkup(string html)
        {
            var page = NaukriSearchParser.Parse(html, Now);

            Assert.All(page.Jobs, job => AssertAccepted(job));
        }

        [Fact]
        public void NullHtmlIsAProgrammerError()
        {
            Assert.Throws<ArgumentNullException>(() => NaukriSearchParser.Parse(null!, Now));
        }
    }
}
