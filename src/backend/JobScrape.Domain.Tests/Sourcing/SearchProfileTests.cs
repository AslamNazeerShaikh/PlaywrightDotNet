using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using Xunit;

namespace JobScrape.Domain.Tests.Sourcing
{
    public sealed class SearchProfileTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

        private static SearchCriteria Criteria(string keywords = "c#")
        {
            return new SearchCriteria(keywords, ["Pune"], 6, 7, remoteOnly: false, []);
        }

        private static RunLimits Limits(int maxPages = 3)
        {
            return new RunLimits(
                maxPages,
                60,
                60,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(6)
            );
        }

        private static SearchProfile Create(SourceKey? source = null, string? name = "backend")
        {
            return SearchProfile.Create(
                name,
                source ?? SourceKey.Naukri,
                Criteria(),
                Limits(),
                Now
            );
        }

        [Fact]
        public void CreateSetsStateAndRaisesCreatedOnce()
        {
            var profile = Create(name: "  naukri-dotnet-pune  ");

            Assert.NotEqual(Guid.Empty, profile.Id.Value);
            Assert.Equal("naukri-dotnet-pune", profile.Name);
            Assert.Same(SourceKey.Naukri, profile.Source);
            Assert.Equal(Criteria(), profile.Criteria);
            Assert.Equal(Limits(), profile.Limits);
            Assert.Equal(Now, profile.CreatedAt);
            Assert.Equal(Now, profile.UpdatedAt);
            var created = Assert.IsType<SearchProfileCreated>(Assert.Single(profile.DomainEvents));
            Assert.Equal(profile.Id, created.ProfileId);
            Assert.Same(SourceKey.Naukri, created.Source);
            Assert.Equal(Now, created.OccurredAt);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void CreateRejectsBlankName(string? name)
        {
            Assert.Throws<DomainException>(() => Create(name: name));
        }

        [Fact]
        public void CreateRejectsNullArguments()
        {
            Assert.Throws<ArgumentNullException>(() =>
                SearchProfile.Create("n", null!, Criteria(), Limits(), Now)
            );
            Assert.Throws<ArgumentNullException>(() =>
                SearchProfile.Create("n", SourceKey.Naukri, null!, Limits(), Now)
            );
            Assert.Throws<ArgumentNullException>(() =>
                SearchProfile.Create("n", SourceKey.Naukri, Criteria(), null!, Now)
            );
        }

        [Fact]
        public void StandardAndRestrictedSourcesStartEnabled()
        {
            Assert.True(Create(SourceKey.Naukri).Enabled);
            Assert.True(Create(SourceKey.Indeed).Enabled);
        }

        [Fact]
        public void HighRiskSourceStartsDisabled()
        {
            Assert.False(Create(SourceKey.LinkedIn).Enabled);
        }

        [Fact]
        public void HighRiskProfileCannotBeEnabledWithoutConsent()
        {
            var profile = Create(SourceKey.LinkedIn);

            Assert.Throws<DomainException>(() =>
                profile.Enable(highRiskConsentAcknowledged: false, Now)
            );
            Assert.False(profile.Enabled);
        }

        [Fact]
        public void HighRiskProfileEnablesWithConsent()
        {
            var profile = Create(SourceKey.LinkedIn);
            profile.ClearDomainEvents();
            var later = Now.AddMinutes(1);

            profile.Enable(highRiskConsentAcknowledged: true, later);

            Assert.True(profile.Enabled);
            Assert.Equal(later, profile.UpdatedAt);
            var updated = Assert.IsType<SearchProfileUpdated>(Assert.Single(profile.DomainEvents));
            Assert.Equal("enabled", updated.ChangedField);
        }

        [Fact]
        public void EnablingAnEnabledProfileIsANoOp()
        {
            var profile = Create();
            profile.ClearDomainEvents();

            profile.Enable(highRiskConsentAcknowledged: false, Now.AddMinutes(1));

            Assert.Empty(profile.DomainEvents);
            Assert.Equal(Now, profile.UpdatedAt);
        }

        [Fact]
        public void DisableThenReEnableStandardProfile()
        {
            var profile = Create();
            profile.ClearDomainEvents();

            profile.Disable(Now.AddMinutes(1));
            profile.Disable(Now.AddMinutes(2));
            profile.Enable(highRiskConsentAcknowledged: false, Now.AddMinutes(3));

            Assert.True(profile.Enabled);
            Assert.Equal(Now.AddMinutes(3), profile.UpdatedAt);
            Assert.Equal(2, profile.DomainEvents.Count);
        }

        [Fact]
        public void UpdateCriteriaReplacesAndRaisesOnlyOnChange()
        {
            var profile = Create();
            profile.ClearDomainEvents();

            profile.UpdateCriteria(Criteria(), Now.AddMinutes(1));
            profile.UpdateCriteria(Criteria("java"), Now.AddMinutes(2));

            Assert.Equal("java", profile.Criteria.Keywords);
            Assert.Equal(Now.AddMinutes(2), profile.UpdatedAt);
            var updated = Assert.IsType<SearchProfileUpdated>(Assert.Single(profile.DomainEvents));
            Assert.Equal("criteria", updated.ChangedField);
            Assert.Equal(profile.Id, updated.ProfileId);
        }

        [Fact]
        public void UpdateLimitsReplacesAndRaisesOnlyOnChange()
        {
            var profile = Create();
            profile.ClearDomainEvents();

            profile.UpdateLimits(Limits(), Now.AddMinutes(1));
            profile.UpdateLimits(Limits(maxPages: 5), Now.AddMinutes(2));

            Assert.Equal(5, profile.Limits.MaxPages);
            var updated = Assert.IsType<SearchProfileUpdated>(Assert.Single(profile.DomainEvents));
            Assert.Equal("limits", updated.ChangedField);
        }

        [Fact]
        public void UpdatesRejectNull()
        {
            var profile = Create();

            Assert.Throws<ArgumentNullException>(() => profile.UpdateCriteria(null!, Now));
            Assert.Throws<ArgumentNullException>(() => profile.UpdateLimits(null!, Now));
        }

        [Fact]
        public void RenameChangesNameAndRaisesOnlyOnChange()
        {
            var profile = Create();
            profile.ClearDomainEvents();

            profile.Rename("  renamed  ", Now.AddMinutes(1));
            profile.Rename("renamed", Now.AddMinutes(2));

            Assert.Equal("renamed", profile.Name);
            Assert.Equal(Now.AddMinutes(1), profile.UpdatedAt);
            var updated = Assert.IsType<SearchProfileUpdated>(Assert.Single(profile.DomainEvents));
            Assert.Equal("name", updated.ChangedField);
        }

        [Fact]
        public void RenameRejectsBlankName()
        {
            var profile = Create();

            Assert.Throws<DomainException>(() => profile.Rename("  ", Now));
        }
    }
}
