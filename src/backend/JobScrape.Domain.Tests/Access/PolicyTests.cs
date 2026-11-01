using JobScrape.Domain.Abstractions;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using Xunit;

namespace JobScrape.Domain.Tests.Access
{
    public sealed class PolicyTests
    {
        private static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(45);

        private static RunLimits Limits(double minSeconds = 2, double maxSeconds = 6)
        {
            return new RunLimits(
                3,
                60,
                60,
                TimeSpan.FromSeconds(minSeconds),
                TimeSpan.FromSeconds(maxSeconds)
            );
        }

        // ---- PolitenessPolicy ----

        [Fact]
        public void PolitenessAsksForTheInclusiveMillisecondWindow()
        {
            var random = new ScriptedRandom(3500);

            var delay = new PolitenessPolicy(Limits(), random).NextDelay();

            Assert.Equal(TimeSpan.FromMilliseconds(3500), delay);
            Assert.Equal((2000, 6001), random.LastRange);
        }

        [Theory]
        [InlineData(-5, 2000)]
        [InlineData(0, 2000)]
        [InlineData(999_999, 6000)]
        public void PolitenessNeverLeavesTheWindowEvenWithABadRandomSource(int raw, int expectedMs)
        {
            var delay = new PolitenessPolicy(Limits(), new ScriptedRandom(raw)).NextDelay();

            Assert.Equal(TimeSpan.FromMilliseconds(expectedMs), delay);
        }

        [Fact]
        public void PolitenessRejectsNullArguments()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new PolitenessPolicy(null!, new ScriptedRandom(0))
            );
            Assert.Throws<ArgumentNullException>(() => new PolitenessPolicy(Limits(), null!));
        }

        // ---- RetryPolicy ----

        [Theory]
        [InlineData(ChallengeKind.None)]
        [InlineData(ChallengeKind.RateLimited)]
        [InlineData(ChallengeKind.SoftChallenge)]
        [InlineData(ChallengeKind.Captcha)]
        [InlineData(ChallengeKind.LoginRequired)]
        [InlineData(ChallengeKind.AccessDenied)]
        [InlineData(ChallengeKind.SourceChanged)]
        public void RetryNeverRetriesAnythingButTransientErrors(ChallengeKind kind)
        {
            var policy = new RetryPolicy(RetryPolicy.MaxAllowedAttempts, new ScriptedRandom(0));

            Assert.False(policy.ShouldRetry(kind, 1));
        }

        [Fact]
        public void RetryAllowsOneRetryWithTwoAttempts()
        {
            var policy = new RetryPolicy(2, new ScriptedRandom(0));

            Assert.True(policy.ShouldRetry(ChallengeKind.TransientError, 1));
            Assert.False(policy.ShouldRetry(ChallengeKind.TransientError, 2));
        }

        [Fact]
        public void RetryWithOneAttemptNeverRetries()
        {
            Assert.False(
                new RetryPolicy(1, new ScriptedRandom(0)).ShouldRetry(
                    ChallengeKind.TransientError,
                    1
                )
            );
        }

        [Theory]
        [InlineData(0)]
        [InlineData(4)]
        public void RetryRejectsAttemptBudgetOutOfRange(int maxAttempts)
        {
            Assert.Throws<DomainException>(() =>
                new RetryPolicy(maxAttempts, new ScriptedRandom(0))
            );
        }

        [Fact]
        public void RetryRejectsZeroBasedAttemptAndNullRandom()
        {
            var policy = new RetryPolicy(2, new ScriptedRandom(0));

            Assert.Throws<DomainException>(() =>
                policy.ShouldRetry(ChallengeKind.TransientError, 0)
            );
            Assert.Throws<DomainException>(() => policy.Backoff(0, null));
            Assert.Throws<ArgumentNullException>(() => new RetryPolicy(2, null!));
        }

        [Theory]
        [InlineData(1, 2000)]
        [InlineData(2, 8000)]
        [InlineData(3, 32000)]
        [InlineData(9, 32000)]
        public void BackoffIsExponentialFromTwoSecondsAndCapped(int attempt, int expectedMs)
        {
            var policy = new RetryPolicy(3, new ScriptedRandom(0));

            Assert.Equal(TimeSpan.FromMilliseconds(expectedMs), policy.Backoff(attempt, null));
        }

        [Theory]
        [InlineData(400, 2400)]
        [InlineData(-1, 2000)]
        [InlineData(5000, 3000)]
        public void BackoffAddsBoundedJitter(int raw, int expectedMs)
        {
            var random = new ScriptedRandom(raw);

            var backoff = new RetryPolicy(2, random).Backoff(1, null);

            Assert.Equal(TimeSpan.FromMilliseconds(expectedMs), backoff);
            Assert.Equal((0, 1001), random.LastRange);
        }

        [Fact]
        public void BackoffHonorsALongerRetryAfter()
        {
            var policy = new RetryPolicy(2, new ScriptedRandom(0));

            Assert.Equal(TimeSpan.FromSeconds(90), policy.Backoff(1, TimeSpan.FromSeconds(90)));
            Assert.Equal(TimeSpan.FromSeconds(2), policy.Backoff(1, TimeSpan.FromSeconds(1)));
        }

        // ---- ChallengeAssessment ----

        [Fact]
        public void AssessmentNormalizesEvidenceAndFlagsManualKinds()
        {
            var captcha = new ChallengeAssessment(ChallengeKind.Captcha, 0.9, "  title=Verify  ");

            Assert.Equal("title=Verify", captcha.Evidence);
            Assert.True(captcha.RequiresManualIntervention);
            Assert.True(
                new ChallengeAssessment(
                    ChallengeKind.SoftChallenge,
                    1,
                    null
                ).RequiresManualIntervention
            );
            Assert.True(
                new ChallengeAssessment(
                    ChallengeKind.LoginRequired,
                    1,
                    null
                ).RequiresManualIntervention
            );
            Assert.False(
                new ChallengeAssessment(
                    ChallengeKind.AccessDenied,
                    1,
                    null
                ).RequiresManualIntervention
            );
            Assert.Equal(ChallengeKind.None, ChallengeAssessment.Clear.Kind);
            Assert.Equal(string.Empty, ChallengeAssessment.Clear.Evidence);
        }

        [Theory]
        [InlineData(-0.01)]
        [InlineData(1.01)]
        [InlineData(double.NaN)]
        public void AssessmentRejectsConfidenceOutOfRange(double confidence)
        {
            Assert.Throws<DomainException>(() =>
                new ChallengeAssessment(ChallengeKind.Captcha, confidence, "x")
            );
        }

        [Fact]
        public void AssessmentRejectsNegativeRetryAfterAndUnknownKind()
        {
            Assert.Throws<DomainException>(() =>
                new ChallengeAssessment(
                    ChallengeKind.RateLimited,
                    1,
                    "429",
                    TimeSpan.FromSeconds(-1)
                )
            );
            Assert.Throws<DomainException>(() =>
                new ChallengeAssessment((ChallengeKind)77, 1, "x")
            );
        }

        // ---- ChallengePolicy ----

        [Fact]
        public void ChallengePolicyTruthTable()
        {
            static ChallengeDecision Decide(
                ChallengeKind kind,
                int consecutive = 0,
                TimeSpan? retryAfter = null
            )
            {
                return ChallengePolicy.Decide(
                    new ChallengeAssessment(kind, 1, "e", retryAfter),
                    consecutive,
                    Cooldown
                );
            }

            Assert.Equal(
                new ChallengeDecision(ChallengeAction.Continue),
                Decide(ChallengeKind.None)
            );
            Assert.Equal(
                new ChallengeDecision(ChallengeAction.Retry),
                Decide(ChallengeKind.TransientError)
            );
            Assert.Equal(
                new ChallengeDecision(ChallengeAction.Pause, TimeSpan.FromSeconds(120)),
                Decide(ChallengeKind.RateLimited, retryAfter: TimeSpan.FromSeconds(120))
            );
            Assert.Equal(
                new ChallengeDecision(
                    ChallengeAction.Pause,
                    ChallengePolicy.DefaultRateLimitCooldown
                ),
                Decide(ChallengeKind.RateLimited)
            );
            Assert.Equal(
                new ChallengeDecision(ChallengeAction.ManualPause, Cooldown),
                Decide(ChallengeKind.Captcha, 2)
            );
            Assert.Equal(
                new ChallengeDecision(ChallengeAction.ManualPause, Cooldown),
                Decide(ChallengeKind.SoftChallenge)
            );
            Assert.Equal(
                new ChallengeDecision(
                    ChallengeAction.ManualPause,
                    ChallengePolicy.RepeatedChallengeCooldown
                ),
                Decide(ChallengeKind.Captcha, BrowserProfile.MaxConsecutiveChallenges)
            );
            Assert.Equal(ChallengeAction.Stop, Decide(ChallengeKind.LoginRequired).Action);
            Assert.Equal(ChallengeAction.OpenCircuit, Decide(ChallengeKind.AccessDenied).Action);
            Assert.Equal(
                ChallengeAction.StopAndInspect,
                Decide(ChallengeKind.SourceChanged).Action
            );
            Assert.NotNull(Decide(ChallengeKind.AccessDenied).Reason);
        }

        [Fact]
        public void ChallengePolicyRejectsBadArguments()
        {
            Assert.Throws<ArgumentNullException>(() => ChallengePolicy.Decide(null!, 0, Cooldown));
            Assert.Throws<DomainException>(() =>
                ChallengePolicy.Decide(ChallengeAssessment.Clear, -1, Cooldown)
            );
            Assert.Throws<DomainException>(() =>
                ChallengePolicy.Decide(ChallengeAssessment.Clear, 0, TimeSpan.Zero)
            );
        }

        // ---- CompliancePolicy ----

        [Theory]
        // source,    ack,   allowHighRisk, interactive, allowed
        [InlineData("naukri", false, false, false, true)]
        [InlineData("indeed", false, false, false, false)]
        [InlineData("indeed", true, false, false, true)]
        [InlineData("linkedin", false, false, false, false)]
        [InlineData("linkedin", true, false, false, false)]
        [InlineData("linkedin", true, true, false, false)]
        [InlineData("linkedin", true, false, true, false)]
        [InlineData("linkedin", false, true, true, false)]
        [InlineData("linkedin", true, true, true, true)]
        public void ComplianceTruthTable(
            string source,
            bool acknowledge,
            bool allowHighRisk,
            bool interactive,
            bool allowed
        )
        {
            var decision = CompliancePolicy.CanRun(
                SourceKey.Parse(source),
                new ComplianceConsent(acknowledge, allowHighRisk, interactive)
            );

            Assert.Equal(allowed, decision.IsAllowed);
            Assert.Equal(allowed, decision.Reason is null);
        }

        [Fact]
        public void ComplianceBlockNamesWhatIsMissing()
        {
            var decision = CompliancePolicy.CanRun(SourceKey.LinkedIn, ComplianceConsent.None);

            Assert.Contains("AllowHighRiskSources", decision.Reason, StringComparison.Ordinal);
            Assert.Contains("interactive", decision.Reason, StringComparison.Ordinal);
        }

        [Fact]
        public void ComplianceRejectsNullArguments()
        {
            Assert.Throws<ArgumentNullException>(() =>
                CompliancePolicy.CanRun(null!, ComplianceConsent.None)
            );
            Assert.Throws<ArgumentNullException>(() =>
                CompliancePolicy.CanRun(SourceKey.Naukri, null!)
            );
        }

        private sealed class ScriptedRandom : IRandomSource
        {
            private readonly int _value;

            public ScriptedRandom(int value)
            {
                _value = value;
            }

            public (int Min, int Max) LastRange { get; private set; }

            public int NextInt32(int minInclusive, int maxExclusive)
            {
                LastRange = (minInclusive, maxExclusive);
                return _value;
            }
        }
    }
}
