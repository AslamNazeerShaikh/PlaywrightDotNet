using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using Xunit;

namespace JobScrape.Domain.Tests.Sourcing
{
    public sealed class JobPostingTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

        private const string Url =
            "https://www.naukri.com/job-listings-senior-dot-net-contoso-pune-123456789";

        // A bare search-card sighting: only the critical fields.
        private static JobSnapshot Card(string title = "Senior .NET Developer")
        {
            return new JobSnapshot
            {
                Source = SourceKey.Naukri,
                ExternalJobId = "123456789",
                Url = Url + "?utm_source=feed",
                Title = title,
            };
        }

        // A detail-page sighting with every optional field observed.
        private static JobSnapshot Detail()
        {
            return Card() with
            {
                Company = " Contoso ",
                CompanyUrl = "https://www.naukri.com/contoso-jobs-careers-1",
                Location = new JobLocation("Pune", "Pune", "Maharashtra", "India", WorkMode.Hybrid),
                ExperienceRaw = "5-8 Yrs",
                ExperienceMinYears = 5,
                ExperienceMaxYears = 8,
                SalaryRaw = "12-18 Lacs P.A.",
                Salary = new SalaryRange(1_200_000m, 1_800_000m, "INR", SalaryPeriod.Year),
                PostedRaw = "3 days ago",
                PostedAt = Now.AddDays(-3),
                Description = "  Build APIs.\nShip features.  ",
                Snippet = "Build APIs",
                Skills = ["C#", " ASP.NET Core ", "c#", "", "SQL"],
                ApplyUrl = "https://careers.contoso.example/apply/42",
            };
        }

        [Fact]
        public void CreateFromCardSetsIdentityAndFlagsGaps()
        {
            var posting = JobPosting.Create(Card(), Now);

            Assert.NotEqual(Guid.Empty, posting.Id.Value);
            Assert.Same(SourceKey.Naukri, posting.Source);
            Assert.Equal("123456789", posting.ExternalJobId.Value);
            Assert.Equal(Url, posting.Url.Value);
            Assert.Equal(Url, posting.ApplyUrl);
            Assert.Equal("Senior .NET Developer", posting.Title.Value);
            Assert.Equal(Now, posting.FirstSeenAt);
            Assert.Equal(Now, posting.LastSeenAt);
            Assert.Equal(JobPostingStatus.Active, posting.Status);
            Assert.Empty(posting.Skills);
            Assert.Equal(
                DataQualityIssues.MissingCompany
                    | DataQualityIssues.MissingLocation
                    | DataQualityIssues.SalaryUnknown
                    | DataQualityIssues.PostedDateUnknown
                    | DataQualityIssues.DescriptionMissing,
                posting.QualityIssues
            );
            Assert.Empty(posting.DomainEvents);
        }

        [Fact]
        public void CreateFromDetailNormalizesEveryField()
        {
            var posting = JobPosting.Create(Detail(), Now);

            Assert.Equal("Contoso", posting.Company?.Value);
            Assert.Equal("https://www.naukri.com/contoso-jobs-careers-1", posting.CompanyUrl);
            Assert.Equal("Pune", posting.Location?.City);
            Assert.Equal("5-8 Yrs", posting.ExperienceRaw);
            Assert.Equal(5, posting.ExperienceMinYears);
            Assert.Equal(8, posting.ExperienceMaxYears);
            Assert.Equal("12-18 Lacs P.A.", posting.SalaryRaw);
            Assert.Equal("INR", posting.Salary?.Currency);
            Assert.Equal("3 days ago", posting.PostedRaw);
            Assert.Equal(Now.AddDays(-3), posting.PostedAt);
            Assert.Equal("Build APIs.\nShip features.", posting.Description);
            Assert.Equal("Build APIs", posting.Snippet);
            Assert.Equal(["C#", "ASP.NET Core", "SQL"], posting.Skills.Select(s => s.Name));
            Assert.Equal("https://careers.contoso.example/apply/42", posting.ApplyUrl);
            Assert.Equal(DataQualityIssues.None, posting.QualityIssues);
        }

        [Fact]
        public void LowConfidenceParseIsFlagged()
        {
            var posting = JobPosting.Create(Detail() with { LowConfidence = true }, Now);

            Assert.Equal(DataQualityIssues.ParseLowConfidence, posting.QualityIssues);
        }

        [Fact]
        public void NullSkillListCountsAsNotObserved()
        {
            var posting = JobPosting.Create(Detail(), Now);

            posting.Refresh(Card() with { Skills = null! }, Now.AddDays(1));

            Assert.Equal(3, posting.Skills.Count);
        }

        [Fact]
        public void BlankOptionalFieldsCountAsAbsent()
        {
            var posting = JobPosting.Create(
                Card() with
                {
                    Company = "  ",
                    Description = " \n ",
                    ApplyUrl = " ",
                    CompanyUrl = "",
                },
                Now
            );

            Assert.Null(posting.Company);
            Assert.Null(posting.Description);
            Assert.Null(posting.CompanyUrl);
            Assert.Equal(Url, posting.ApplyUrl);
        }

        [Fact]
        public void CreateRejectsNullSnapshotOrSource()
        {
            Assert.Throws<ArgumentNullException>(() => JobPosting.Create(null!, Now));
            Assert.Throws<ArgumentNullException>(() =>
                JobPosting.Create(Card() with { Source = null! }, Now)
            );
        }

        public static TheoryData<JobSnapshot> InvalidSnapshots =>
            [
                Card() with
                {
                    ExternalJobId = " ",
                },
                Card() with
                {
                    Title = "",
                },
                Card() with
                {
                    Url = "https://evil.example/job-listings-x-1",
                },
                Card() with
                {
                    ApplyUrl = "javascript:alert(1)",
                },
                Card() with
                {
                    ApplyUrl = "https://user:pw@ats.example/apply",
                },
                Card() with
                {
                    ApplyUrl = "ftp://ats.example/apply",
                },
                Card() with
                {
                    CompanyUrl = "https://ats.example/" + new string('a', 2048),
                },
                Card() with
                {
                    ExperienceMinYears = -1,
                },
                Card() with
                {
                    ExperienceMaxYears = 51,
                },
                Card() with
                {
                    ExperienceMinYears = 9,
                    ExperienceMaxYears = 8,
                },
                Card() with
                {
                    Description = new string('d', JobPosting.MaxDescriptionLength + 1),
                },
                Card() with
                {
                    Snippet = new string('s', JobPosting.MaxDescriptionLength + 1),
                },
                Card() with
                {
                    Skills = Enumerable
                        .Range(1, JobPosting.MaxSkills + 1)
                        .Select(i => $"s{i}")
                        .ToArray(),
                },
            ];

        [Theory]
        [MemberData(nameof(InvalidSnapshots))]
        public void CreateRejectsInvalidSnapshot(JobSnapshot snapshot)
        {
            Assert.Throws<DomainException>(() => JobPosting.Create(snapshot, Now));
        }

        [Fact]
        public void AcceptsFiftySkillsAfterDedupe()
        {
            var skills = Enumerable
                .Range(1, JobPosting.MaxSkills)
                .Select(i => $"s{i}")
                .Concat(["S1", "s2"])
                .ToArray();

            var posting = JobPosting.Create(Card() with { Skills = skills }, Now);

            Assert.Equal(JobPosting.MaxSkills, posting.Skills.Count);
        }

        [Fact]
        public void AcceptsHttpApplyUrlForExternalAts()
        {
            var posting = JobPosting.Create(
                Card() with
                {
                    ApplyUrl = "http://ats.example/apply",
                },
                Now
            );

            Assert.Equal("http://ats.example/apply", posting.ApplyUrl);
        }

        [Fact]
        public void UnchangedResightingOnlyMovesLastSeen()
        {
            var posting = JobPosting.Create(Detail(), Now);
            var hash = posting.ContentHash;

            posting.Refresh(Detail(), Now.AddDays(1));

            Assert.Equal(hash, posting.ContentHash);
            Assert.Equal(Now, posting.FirstSeenAt);
            Assert.Equal(Now.AddDays(1), posting.LastSeenAt);
            Assert.Empty(posting.DomainEvents);
        }

        [Fact]
        public void CardResightingKeepsEnrichedFields()
        {
            var posting = JobPosting.Create(Detail(), Now);
            var hash = posting.ContentHash;

            posting.Refresh(Card(), Now.AddDays(1));

            Assert.Equal("Contoso", posting.Company?.Value);
            Assert.Equal("Build APIs.\nShip features.", posting.Description);
            Assert.Equal(3, posting.Skills.Count);
            Assert.Equal(5, posting.ExperienceMinYears);
            Assert.Equal("https://careers.contoso.example/apply/42", posting.ApplyUrl);
            Assert.Equal(hash, posting.ContentHash);
            Assert.Empty(posting.DomainEvents);
            Assert.Equal(DataQualityIssues.None, posting.QualityIssues);
        }

        [Fact]
        public void EnrichmentChangesHashAndRaisesUpdatedOnce()
        {
            var posting = JobPosting.Create(Card(), Now);
            var oldHash = posting.ContentHash;

            posting.Refresh(Detail(), Now.AddMinutes(5));

            Assert.NotEqual(oldHash, posting.ContentHash);
            var updated = Assert.IsType<JobPostingUpdated>(Assert.Single(posting.DomainEvents));
            Assert.Equal(posting.Id, updated.PostingId);
            Assert.Same(SourceKey.Naukri, updated.Source);
            Assert.Equal(oldHash, updated.OldHash);
            Assert.Equal(posting.ContentHash, updated.NewHash);
            Assert.Equal(Now.AddMinutes(5), updated.OccurredAt);
        }

        [Fact]
        public void TitleChangeIsAContentChange()
        {
            var posting = JobPosting.Create(Card(), Now);

            posting.Refresh(Card("Lead .NET Developer"), Now.AddDays(1));

            Assert.Equal("Lead .NET Developer", posting.Title.Value);
            Assert.Single(posting.DomainEvents);
        }

        [Fact]
        public void SkillOrderAndCaseDoNotChangeHash()
        {
            var posting = JobPosting.Create(Card() with { Skills = ["C#", "SQL"] }, Now);
            var hash = posting.ContentHash;

            posting.Refresh(Card() with { Skills = ["sql", "c#"] }, Now.AddDays(1));

            Assert.Equal(hash, posting.ContentHash);
        }

        [Fact]
        public void VolatileFieldsDoNotChangeHash()
        {
            var posting = JobPosting.Create(Detail(), Now);
            var hash = posting.ContentHash;

            posting.Refresh(
                Detail() with
                {
                    PostedRaw = "4 days ago",
                    ApplyUrl = "https://careers.contoso.example/apply/43",
                },
                Now.AddDays(1)
            );

            Assert.Equal(hash, posting.ContentHash);
            Assert.Equal("4 days ago", posting.PostedRaw);
            Assert.Equal("https://careers.contoso.example/apply/43", posting.ApplyUrl);
        }

        [Fact]
        public void ExperiencePairIsReplacedTogether()
        {
            var posting = JobPosting.Create(Detail(), Now);

            posting.Refresh(Card() with { ExperienceMinYears = 10 }, Now.AddDays(1));

            Assert.Equal(10, posting.ExperienceMinYears);
            Assert.Null(posting.ExperienceMaxYears);
        }

        [Fact]
        public void OutOfOrderSightingNeverMovesLastSeenBack()
        {
            var posting = JobPosting.Create(Card(), Now);
            posting.Refresh(Card(), Now.AddDays(2));

            posting.Refresh(Card(), Now.AddDays(1));

            Assert.Equal(Now.AddDays(2), posting.LastSeenAt);
        }

        [Fact]
        public void RefreshRejectsSightingBeforeFirstSeen()
        {
            var posting = JobPosting.Create(Card(), Now);

            Assert.Throws<DomainException>(() => posting.Refresh(Card(), Now.AddTicks(-1)));
        }

        [Fact]
        public void RefreshRejectsDifferentIdentity()
        {
            var posting = JobPosting.Create(Card(), Now);

            Assert.Throws<DomainException>(() =>
                posting.Refresh(Card() with { ExternalJobId = "999" }, Now)
            );
            Assert.Throws<DomainException>(() =>
                posting.Refresh(
                    Card() with
                    {
                        Source = SourceKey.LinkedIn,
                        Url = "https://www.linkedin.com/jobs/view/123456789",
                    },
                    Now
                )
            );
            Assert.Throws<ArgumentNullException>(() => posting.Refresh(null!, Now));
        }

        [Fact]
        public void InvalidRefreshLeavesPostingUnchanged()
        {
            var posting = JobPosting.Create(Detail(), Now);
            var hash = posting.ContentHash;

            Assert.Throws<DomainException>(() =>
                posting.Refresh(
                    Detail() with
                    {
                        Title = "Changed",
                        ApplyUrl = "javascript:alert(1)",
                    },
                    Now.AddDays(1)
                )
            );

            Assert.Equal("Senior .NET Developer", posting.Title.Value);
            Assert.Equal(hash, posting.ContentHash);
            Assert.Equal(Now, posting.LastSeenAt);
        }

        [Fact]
        public void ExpiredPostingReactivatesWhenSeenAgain()
        {
            var posting = JobPosting.Create(Card(), Now);

            posting.MarkExpired();
            Assert.Equal(JobPostingStatus.Expired, posting.Status);

            posting.Refresh(Card(), Now.AddDays(1));
            Assert.Equal(JobPostingStatus.Active, posting.Status);
        }
    }
}
