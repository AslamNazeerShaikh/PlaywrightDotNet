using JobScrape.Domain.Access;
using JobScrape.Domain.Common;
using Xunit;

namespace JobScrape.Domain.Tests.Access
{
    public sealed class SourceCircuitBreakerTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

        private static SourceCircuitBreaker Opened(
            ChallengeKind trigger = ChallengeKind.AccessDenied
        )
        {
            var breaker = SourceCircuitBreaker.Create(SourceKey.Naukri);
            breaker.RecordFailure(trigger, Now);
            if (breaker.State != CircuitState.Open)
            {
                breaker.RecordFailure(trigger, Now);
            }

            breaker.ClearDomainEvents();
            return breaker;
        }

        [Fact]
        public void StartsClosedAndAllowsRuns()
        {
            var breaker = SourceCircuitBreaker.Create(SourceKey.Naukri);

            Assert.Same(SourceKey.Naukri, breaker.Source);
            Assert.Equal(CircuitState.Closed, breaker.State);
            Assert.True(breaker.AllowsRun);
            Assert.False(breaker.CanProbe(Now));
            Assert.Null(breaker.OpenUntil);
            Assert.Empty(breaker.RecentFailures);
        }

        [Fact]
        public void AccessDeniedOpensImmediatelyForADay()
        {
            var breaker = SourceCircuitBreaker.Create(SourceKey.Naukri);

            breaker.RecordFailure(ChallengeKind.AccessDenied, Now);

            Assert.Equal(CircuitState.Open, breaker.State);
            Assert.False(breaker.AllowsRun);
            Assert.Equal(Now + SourceCircuitBreaker.ChallengeCooldown, breaker.OpenUntil);
            var opened = Assert.IsType<CircuitOpened>(Assert.Single(breaker.DomainEvents));
            Assert.Same(SourceKey.Naukri, opened.Source);
            Assert.Equal(ChallengeKind.AccessDenied, opened.Trigger);
            Assert.Equal(breaker.OpenUntil, opened.OpenUntil);
        }

        [Fact]
        public void OneOtherWallDoesNotOpen()
        {
            var breaker = SourceCircuitBreaker.Create(SourceKey.Naukri);

            breaker.RecordFailure(ChallengeKind.Captcha, Now);

            Assert.Equal(CircuitState.Closed, breaker.State);
            Assert.Equal(1, breaker.ConsecutiveFailures);
            Assert.Empty(breaker.DomainEvents);
        }

        [Fact]
        public void TwoConsecutiveWallsOpen()
        {
            var breaker = SourceCircuitBreaker.Create(SourceKey.Naukri);

            breaker.RecordFailure(ChallengeKind.Captcha, Now);
            breaker.RecordFailure(ChallengeKind.SoftChallenge, Now.AddHours(1));

            Assert.Equal(CircuitState.Open, breaker.State);
            Assert.Equal(
                Now.AddHours(1) + SourceCircuitBreaker.ChallengeCooldown,
                breaker.OpenUntil
            );
        }

        [Fact]
        public void ThreeWallsInADayOpenEvenWithSuccessesBetween()
        {
            var breaker = SourceCircuitBreaker.Create(SourceKey.Naukri);

            breaker.RecordFailure(ChallengeKind.Captcha, Now);
            breaker.RecordSuccess(Now.AddHours(1));
            breaker.RecordFailure(ChallengeKind.Captcha, Now.AddHours(2));
            breaker.RecordSuccess(Now.AddHours(3));
            Assert.Equal(CircuitState.Closed, breaker.State);

            breaker.RecordFailure(ChallengeKind.Captcha, Now.AddHours(23));

            Assert.Equal(CircuitState.Open, breaker.State);
        }

        [Fact]
        public void WallsOlderThanTheWindowAreForgotten()
        {
            var breaker = SourceCircuitBreaker.Create(SourceKey.Naukri);

            breaker.RecordFailure(ChallengeKind.Captcha, Now);
            breaker.RecordSuccess(Now.AddHours(1));
            breaker.RecordFailure(ChallengeKind.Captcha, Now.AddHours(2));
            breaker.RecordSuccess(Now.AddHours(24));

            Assert.Equal([Now.AddHours(2)], breaker.RecentFailures);

            breaker.RecordFailure(ChallengeKind.Captcha, Now.AddHours(25));

            Assert.Equal(CircuitState.Closed, breaker.State);
        }

        [Fact]
        public void RateLimitsOpenForAnHour()
        {
            var breaker = Opened(ChallengeKind.RateLimited);

            Assert.Equal(Now + SourceCircuitBreaker.RateLimitCooldown, breaker.OpenUntil);
        }

        [Fact]
        public void ProbeOnlyAfterCooldown()
        {
            var breaker = Opened();
            var reopenAt = breaker.OpenUntil!.Value;

            Assert.False(breaker.CanProbe(reopenAt.AddTicks(-1)));
            Assert.Throws<DomainException>(() => breaker.BeginProbe(reopenAt.AddTicks(-1)));
            Assert.True(breaker.CanProbe(reopenAt));

            breaker.BeginProbe(reopenAt);

            Assert.Equal(CircuitState.HalfOpen, breaker.State);
            Assert.Null(breaker.OpenUntil);
            Assert.False(breaker.AllowsRun);
        }

        [Fact]
        public void SuccessfulProbeClosesAndForgets()
        {
            var breaker = Opened();
            var reopenAt = breaker.OpenUntil!.Value;
            breaker.BeginProbe(reopenAt);

            breaker.RecordSuccess(reopenAt.AddMinutes(1));

            Assert.Equal(CircuitState.Closed, breaker.State);
            Assert.Equal(0, breaker.ConsecutiveFailures);
            Assert.Empty(breaker.RecentFailures);
            Assert.True(breaker.AllowsRun);
        }

        [Fact]
        public void FailedProbeReopensForAWeek()
        {
            var breaker = Opened();
            var reopenAt = breaker.OpenUntil!.Value;
            breaker.BeginProbe(reopenAt);

            breaker.RecordFailure(ChallengeKind.RateLimited, reopenAt.AddMinutes(1));

            Assert.Equal(CircuitState.Open, breaker.State);
            Assert.Equal(
                reopenAt.AddMinutes(1) + SourceCircuitBreaker.RepeatedBlockCooldown,
                breaker.OpenUntil
            );
            Assert.Single(breaker.DomainEvents);
        }

        [Fact]
        public void OpenBreakerRejectsOutcomes()
        {
            var breaker = Opened();

            Assert.Throws<DomainException>(() => breaker.RecordSuccess(Now));
            Assert.Throws<DomainException>(() => breaker.RecordFailure(ChallengeKind.Captcha, Now));
        }

        [Fact]
        public void BeginProbeRequiresOpen()
        {
            Assert.Throws<DomainException>(() =>
                SourceCircuitBreaker.Create(SourceKey.Naukri).BeginProbe(Now)
            );
        }

        [Theory]
        [InlineData(ChallengeKind.None)]
        [InlineData(ChallengeKind.TransientError)]
        [InlineData(ChallengeKind.SourceChanged)]
        [InlineData((ChallengeKind)42)]
        public void NonWallsAreRejected(ChallengeKind kind)
        {
            Assert.Throws<DomainException>(() =>
                SourceCircuitBreaker.Create(SourceKey.Naukri).RecordFailure(kind, Now)
            );
        }

        [Fact]
        public void RecentFailureCountIgnoresWallsOutsideTheWindowWithoutAWrite()
        {
            var breaker = SourceCircuitBreaker.Create(SourceKey.Naukri);
            breaker.RecordFailure(ChallengeKind.Captcha, Now);

            Assert.Equal(1, breaker.RecentFailureCount(Now.AddHours(23)));
            Assert.Equal(0, breaker.RecentFailureCount(Now + SourceCircuitBreaker.FailureWindow));
            Assert.Single(breaker.RecentFailures);
        }

        [Fact]
        public void CreateRejectsNullSource()
        {
            Assert.Throws<ArgumentNullException>(() => SourceCircuitBreaker.Create(null!));
        }
    }
}
