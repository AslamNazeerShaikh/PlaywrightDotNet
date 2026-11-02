using JobScrape.Domain.Sourcing;
using Xunit;

namespace JobScrape.Domain.Tests.Sourcing
{
    public sealed class SearchCriteriaTests
    {
        private static SearchCriteria Create(
            string? keywords = "asp.net core c#",
            string?[]? locations = null,
            int? experienceYears = 6,
            int? postedWithinDays = 7,
            bool remoteOnly = false,
            string?[]? requiredSkills = null
        )
        {
            return new SearchCriteria(
                keywords,
                locations ?? ["Pune"],
                experienceYears,
                postedWithinDays,
                remoteOnly,
                requiredSkills ?? []
            );
        }

        [Fact]
        public void AcceptsTypicalCriteria()
        {
            var criteria = Create(requiredSkills: ["C#", "ASP.NET Core"]);

            Assert.Equal("asp.net core c#", criteria.Keywords);
            Assert.Equal(["Pune"], criteria.Locations);
            Assert.Equal(6, criteria.ExperienceYears);
            Assert.Equal(7, criteria.PostedWithinDays);
            Assert.False(criteria.RemoteOnly);
            Assert.Equal(["C#", "ASP.NET Core"], criteria.RequiredSkills);
        }

        [Fact]
        public void NormalizesKeywordWhitespace()
        {
            var criteria = Create(keywords: "  asp.net \t core\n c#  ");

            Assert.Equal("asp.net core c#", criteria.Keywords);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void RejectsMissingKeywords(string? keywords)
        {
            Assert.Throws<DomainException>(() => Create(keywords: keywords));
        }

        [Fact]
        public void AcceptsKeywordsAtLengthLimit()
        {
            var keywords = new string('k', SearchCriteria.MaxKeywordsLength);

            Assert.Equal(keywords, Create(keywords: keywords).Keywords);
        }

        [Fact]
        public void RejectsKeywordsOverLengthLimit()
        {
            var keywords = new string('k', SearchCriteria.MaxKeywordsLength + 1);

            Assert.Throws<DomainException>(() => Create(keywords: keywords));
        }

        [Fact]
        public void NormalizesAndDeduplicatesLocations()
        {
            var criteria = Create(locations: [" Pune ", "", null, "pune", "New   Delhi", "  "]);

            Assert.Equal(["Pune", "New Delhi"], criteria.Locations);
        }

        [Fact]
        public void AcceptsTenLocations()
        {
            var locations = Enumerable.Range(1, 10).Select(i => $"City{i}").ToArray();

            Assert.Equal(10, Create(locations: locations).Locations.Count);
        }

        [Fact]
        public void RejectsElevenLocations()
        {
            var locations = Enumerable.Range(1, 11).Select(i => $"City{i}").ToArray();

            Assert.Throws<DomainException>(() => Create(locations: locations));
        }

        [Fact]
        public void RejectsNoLocationWhenNotRemote()
        {
            Assert.Throws<DomainException>(() => Create(locations: ["  "]));
        }

        [Fact]
        public void AcceptsNoLocationWhenRemoteOnly()
        {
            var criteria = Create(locations: [], remoteOnly: true);

            Assert.Empty(criteria.Locations);
            Assert.True(criteria.RemoteOnly);
        }

        [Fact]
        public void RejectsNullLocations()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new SearchCriteria("c#", null!, null, null, true, [])
            );
        }

        [Fact]
        public void RejectsNullRequiredSkills()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new SearchCriteria("c#", ["Pune"], null, null, true, null!)
            );
        }

        [Fact]
        public void NormalizesAndDeduplicatesRequiredSkills()
        {
            var criteria = Create(requiredSkills: [" C# ", "", null, "c#", ".NET  Core", "  "]);

            Assert.Equal(["C#", ".NET Core"], criteria.RequiredSkills);
        }

        [Fact]
        public void AcceptsTwentyRequiredSkills()
        {
            var skills = Enumerable.Range(1, 20).Select(i => $"Skill{i}").ToArray();

            Assert.Equal(20, Create(requiredSkills: skills).RequiredSkills.Count);
        }

        [Fact]
        public void RejectsTwentyOneRequiredSkills()
        {
            var skills = Enumerable.Range(1, 21).Select(i => $"Skill{i}").ToArray();

            Assert.Throws<DomainException>(() => Create(requiredSkills: skills));
        }

        [Fact]
        public void EmptyRequiredSkillsMeansNoFilter()
        {
            Assert.Empty(Create().RequiredSkills);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(0)]
        [InlineData(50)]
        public void AcceptsExperienceBoundsAndAbsence(int? years)
        {
            Assert.Equal(years, Create(experienceYears: years).ExperienceYears);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(51)]
        public void RejectsExperienceOutOfRange(int years)
        {
            Assert.Throws<DomainException>(() => Create(experienceYears: years));
        }

        [Theory]
        [InlineData(null)]
        [InlineData(1)]
        [InlineData(30)]
        public void AcceptsPostedWithinBoundsAndAbsence(int? days)
        {
            Assert.Equal(days, Create(postedWithinDays: days).PostedWithinDays);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(31)]
        public void RejectsPostedWithinOutOfRange(int days)
        {
            Assert.Throws<DomainException>(() => Create(postedWithinDays: days));
        }

        [Fact]
        public void EqualCriteriaAreEqualAndHashAlike()
        {
            var first = Create(locations: ["Pune", "Hyderabad"]);
            var second = Create(keywords: " asp.net  core c# ", locations: ["Pune", "Hyderabad"]);

            Assert.Equal(first, second);
            Assert.Equal(first.GetHashCode(), second.GetHashCode());
        }

        [Fact]
        public void EqualCriteriaIgnoreSkillAndLocationCase()
        {
            var first = Create(locations: ["Pune"], requiredSkills: ["C#"]);
            var second = Create(locations: ["pune"], requiredSkills: ["c#"]);

            Assert.Equal(first, second);
            Assert.Equal(first.GetHashCode(), second.GetHashCode());
        }

        [Fact]
        public void DifferentCriteriaAreNotEqual()
        {
            var baseline = Create();

            Assert.NotEqual(baseline, Create(keywords: "java"));
            Assert.NotEqual(baseline, Create(locations: ["Hyderabad"]));
            Assert.NotEqual(baseline, Create(locations: ["Pune", "Hyderabad"]));
            Assert.NotEqual(baseline, Create(experienceYears: 5));
            Assert.NotEqual(baseline, Create(postedWithinDays: 3));
            Assert.NotEqual(baseline, Create(remoteOnly: true));
            Assert.NotEqual(baseline, Create(requiredSkills: ["C#"]));
            Assert.False(baseline.Equals(null));
        }
    }
}
