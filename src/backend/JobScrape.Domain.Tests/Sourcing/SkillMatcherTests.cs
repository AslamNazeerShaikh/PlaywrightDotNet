using JobScrape.Domain.Sourcing;
using Xunit;

namespace JobScrape.Domain.Tests.Sourcing
{
    public sealed class SkillMatcherTests
    {
        [Fact]
        public void EmptyRequiredListPassesEverything()
        {
            Assert.True(SkillMatcher.Matches([JobSkill.Create("Java")], []));
            Assert.True(SkillMatcher.Matches([], []));
            Assert.True(SkillMatcher.Matches(null, []));
            Assert.True(SkillMatcher.Matches([JobSkill.Create("Java")], null));
        }

        [Theory]
        [InlineData("C#", "c#")]
        [InlineData("ASP.NET Core", "asp.net core")]
        [InlineData(".NET", ".net")]
        public void SingleOverlapMatchesIgnoringCase(string posted, string required)
        {
            Assert.True(
                SkillMatcher.Matches([JobSkill.Create(posted)], [required])
            );
        }

        [Fact]
        public void AnyOverlapAmongManyMatches()
        {
            Assert.True(
                SkillMatcher.Matches(
                    [JobSkill.Create("Java"), JobSkill.Create("ASP.NET Core")],
                    ["c#", ".net", "asp.net core"]
                )
            );
        }

        [Fact]
        public void NoOverlapFails()
        {
            Assert.False(
                SkillMatcher.Matches(
                    [JobSkill.Create("Java"), JobSkill.Create("Spring")],
                    ["c#", "asp.net"]
                )
            );
        }

        [Fact]
        public void SubstringIsNotAMatch()
        {
            Assert.False(SkillMatcher.Matches([JobSkill.Create("CSS")], ["C"]));
            Assert.False(SkillMatcher.Matches([JobSkill.Create("ASP.NET Core")], ["ASP.NET"]));
        }

        [Fact]
        public void NoPostingSkillsNeverMatchesAFilter()
        {
            Assert.False(SkillMatcher.Matches([], ["c#"]));
            Assert.False(SkillMatcher.Matches(null, ["c#"]));
        }

        [Fact]
        public void BlankEntriesAreIgnoredOnBothSides()
        {
            Assert.True(
                SkillMatcher.Matches(["  ", null, "C#"], ["", "  ", null, "c#"])
            );
            Assert.False(
                SkillMatcher.Matches(["  ", null], ["", "  ", null, "c#"])
            );
            Assert.True(SkillMatcher.Matches(["C#"], ["", "  "]));
        }

        [Fact]
        public void RawNameOverloadAgreesWithSkillOverload()
        {
            Assert.True(SkillMatcher.Matches(["c#"], ["C#"]));
            Assert.False(SkillMatcher.Matches(["java"], ["C#"]));
        }
    }
}
