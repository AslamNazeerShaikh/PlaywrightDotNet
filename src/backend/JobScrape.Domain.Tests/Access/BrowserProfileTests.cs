using JobScrape.Domain.Access;
using JobScrape.Domain.Common;
using Xunit;

namespace JobScrape.Domain.Tests.Access
{
    public sealed class BrowserProfileTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

        private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

        private static BrowserProfile Create(string? name = "naukri-main")
        {
            return BrowserProfile.Create(
                name,
                SourceKey.Naukri,
                BrowserEngine.Chromium,
                BrowserChannel.Bundled
            );
        }

        private static BrowserProfile Healthy()
        {
            var profile = Create();
            profile.MarkVerified(Now);
            profile.ClearDomainEvents();
            return profile;
        }

        [Fact]
        public void CreateStartsNewAndDerivesDirectory()
        {
            var profile = Create("  naukri-main ");

            Assert.NotEqual(Guid.Empty, profile.Id.Value);
            Assert.Equal("naukri-main", profile.Name);
            Assert.Same(SourceKey.Naukri, profile.Source);
            Assert.Equal(BrowserEngine.Chromium, profile.Engine);
            Assert.Equal(BrowserChannel.Bundled, profile.Channel);
            Assert.Equal("naukri/naukri-main", profile.UserDataDirectory);
            Assert.Equal(SessionState.New, profile.State);
            Assert.Equal(0, profile.ConsecutiveChallenges);
            Assert.Null(profile.CooldownUntil);
            Assert.Null(profile.LastUsedAt);
            Assert.Null(profile.LastVerifiedAt);
            Assert.True(profile.CanLaunch(Now));
        }

        [Theory]
        [InlineData("a")]
        [InlineData("Profile_1.v2-x")]
        public void CreateAcceptsSafeNames(string name)
        {
            Assert.Equal(name, Create(name).Name);
        }

        [Fact]
        public void CreateAcceptsMaxLengthName()
        {
            var name = new string('p', BrowserProfile.MaxNameLength);

            Assert.Equal(name, Create(name).Name);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("  ")]
        [InlineData("..")]
        [InlineData(".hidden")]
        [InlineData("../escape")]
        [InlineData("a/b")]
        [InlineData("a\\b")]
        [InlineData("with space")]
        [InlineData("naukrí")]
        public void CreateRejectsUnsafeNames(string? name)
        {
            Assert.Throws<DomainException>(() => Create(name));
        }

        [Theory]
        [InlineData("naukri-main", true)]
        [InlineData("a.b_c-1", true)]
        [InlineData(".hidden", false)]
        [InlineData("a/b", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsValidNameMatchesTheCreateRule(string? name, bool valid)
        {
            Assert.Equal(valid, BrowserProfile.IsValidName(name));
            Assert.False(
                BrowserProfile.IsValidName(new string('p', BrowserProfile.MaxNameLength + 1))
            );
        }

        [Fact]
        public void CreateRejectsOverlongName()
        {
            Assert.Throws<DomainException>(() =>
                Create(new string('p', BrowserProfile.MaxNameLength + 1))
            );
        }

        [Fact]
        public void BrandedChannelsRequireChromium()
        {
            var edge = BrowserProfile.Create(
                "edge",
                SourceKey.Naukri,
                BrowserEngine.Chromium,
                BrowserChannel.Edge
            );

            Assert.Equal(BrowserChannel.Edge, edge.Channel);
            Assert.Throws<DomainException>(() =>
                BrowserProfile.Create(
                    "ff",
                    SourceKey.Naukri,
                    BrowserEngine.Firefox,
                    BrowserChannel.Chrome
                )
            );
            Assert.Equal(
                BrowserEngine.WebKit,
                BrowserProfile
                    .Create("wk", SourceKey.Naukri, BrowserEngine.WebKit, BrowserChannel.Bundled)
                    .Engine
            );
        }

        [Fact]
        public void CreateRejectsUndefinedEnumsAndNullSource()
        {
            Assert.Throws<DomainException>(() =>
                BrowserProfile.Create(
                    "p",
                    SourceKey.Naukri,
                    (BrowserEngine)9,
                    BrowserChannel.Bundled
                )
            );
            Assert.Throws<DomainException>(() =>
                BrowserProfile.Create(
                    "p",
                    SourceKey.Naukri,
                    BrowserEngine.Chromium,
                    (BrowserChannel)9
                )
            );
            Assert.Throws<ArgumentNullException>(() =>
                BrowserProfile.Create("p", null!, BrowserEngine.Chromium, BrowserChannel.Bundled)
            );
        }

        [Fact]
        public void MarkVerifiedMakesHealthyAndRaisesBootstrappedOnce()
        {
            var profile = Create();

            profile.MarkVerified(Now);

            Assert.Equal(SessionState.Healthy, profile.State);
            Assert.Equal(Now, profile.LastVerifiedAt);
            var bootstrapped = Assert.IsType<SessionBootstrapped>(
                Assert.Single(profile.DomainEvents)
            );
            Assert.Equal(profile.Id, bootstrapped.ProfileId);
            Assert.Same(SourceKey.Naukri, bootstrapped.Source);
            Assert.Equal(SessionState.Healthy, bootstrapped.State);
        }

        [Fact]
        public void MarkUsedRecordsLaunch()
        {
            var profile = Healthy();

            profile.MarkUsed(Now.AddMinutes(1));

            Assert.Equal(Now.AddMinutes(1), profile.LastUsedAt);
        }

        [Theory]
        [InlineData(ChallengeKind.Captcha)]
        [InlineData(ChallengeKind.SoftChallenge)]
        [InlineData(ChallengeKind.LoginRequired)]
        [InlineData(ChallengeKind.AccessDenied)]
        [InlineData(ChallengeKind.RateLimited)]
        public void ChallengeStartsCooldownThatBlocksLaunch(ChallengeKind kind)
        {
            var profile = Healthy();

            profile.MarkChallenged(kind, Now, Hour);

            Assert.Equal(SessionState.CoolingDown, profile.State);
            Assert.Equal(Now + Hour, profile.CooldownUntil);
            Assert.Equal(1, profile.ConsecutiveChallenges);
            Assert.False(profile.CanLaunch(Now + Hour - TimeSpan.FromTicks(1)));
            Assert.True(profile.CanLaunch(Now + Hour));
        }

        [Fact]
        public void CoolingDownProfileCannotBeUsedOrVerifiedEarly()
        {
            var profile = Healthy();
            profile.MarkChallenged(ChallengeKind.Captcha, Now, Hour);

            Assert.Throws<DomainException>(() => profile.MarkUsed(Now.AddMinutes(30)));
            Assert.Throws<DomainException>(() => profile.MarkVerified(Now.AddMinutes(30)));
            Assert.Null(profile.LastUsedAt);
        }

        [Fact]
        public void VerificationAfterCooldownRestoresHealthAndResetsCounter()
        {
            var profile = Healthy();
            profile.MarkChallenged(ChallengeKind.Captcha, Now, Hour);
            profile.MarkChallenged(ChallengeKind.Captcha, Now.AddHours(2), Hour);

            profile.MarkVerified(Now.AddHours(4));

            Assert.Equal(SessionState.Healthy, profile.State);
            Assert.Equal(0, profile.ConsecutiveChallenges);
            Assert.Null(profile.CooldownUntil);
        }

        [Fact]
        public void ThirdConsecutiveChallengeRequiresReset()
        {
            var profile = Healthy();

            profile.MarkChallenged(ChallengeKind.Captcha, Now, Hour);
            profile.MarkChallenged(ChallengeKind.Captcha, Now.AddHours(2), Hour);
            profile.MarkChallenged(ChallengeKind.Captcha, Now.AddHours(4), Hour);

            Assert.Equal(SessionState.ResetRequired, profile.State);
            Assert.Equal(BrowserProfile.MaxConsecutiveChallenges, profile.ConsecutiveChallenges);
            Assert.Null(profile.CooldownUntil);
            Assert.False(profile.CanLaunch(Now.AddDays(30)));
            Assert.Throws<DomainException>(() => profile.MarkVerified(Now.AddDays(30)));
            Assert.Throws<DomainException>(() =>
                profile.MarkChallenged(ChallengeKind.Captcha, Now.AddDays(30), Hour)
            );
        }

        [Fact]
        public void ResetReturnsToNewAndClearsHistory()
        {
            var profile = Healthy();
            profile.MarkChallenged(ChallengeKind.Captcha, Now, Hour);
            profile.MarkChallenged(ChallengeKind.Captcha, Now.AddHours(2), Hour);
            profile.MarkChallenged(ChallengeKind.Captcha, Now.AddHours(4), Hour);

            profile.Reset();

            Assert.Equal(SessionState.New, profile.State);
            Assert.Equal(0, profile.ConsecutiveChallenges);
            Assert.Null(profile.CooldownUntil);
            Assert.Null(profile.LastVerifiedAt);
            Assert.True(profile.CanLaunch(Now.AddHours(4)));
        }

        [Theory]
        [InlineData(ChallengeKind.None)]
        [InlineData(ChallengeKind.TransientError)]
        [InlineData(ChallengeKind.SourceChanged)]
        [InlineData((ChallengeKind)99)]
        public void MarkChallengedRejectsNonWalls(ChallengeKind kind)
        {
            Assert.Throws<DomainException>(() => Healthy().MarkChallenged(kind, Now, Hour));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void MarkChallengedRejectsNonPositiveCooldown(int seconds)
        {
            Assert.Throws<DomainException>(() =>
                Healthy().MarkChallenged(ChallengeKind.Captcha, Now, TimeSpan.FromSeconds(seconds))
            );
        }

        [Fact]
        public void HealthySessionCanExpireAndReLoginVerifiesIt()
        {
            var profile = Healthy();

            profile.MarkExpired();
            Assert.Equal(SessionState.Expired, profile.State);
            Assert.True(profile.CanLaunch(Now));

            profile.MarkVerified(Now.AddMinutes(5));
            Assert.Equal(SessionState.Healthy, profile.State);
        }

        [Fact]
        public void OnlyHealthySessionCanExpire()
        {
            Assert.Throws<DomainException>(() => Create().MarkExpired());
        }
    }
}
