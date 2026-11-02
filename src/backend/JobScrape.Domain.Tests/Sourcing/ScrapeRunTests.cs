using JobScrape.Domain.Access;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using Xunit;

namespace JobScrape.Domain.Tests.Sourcing
{
    public sealed class ScrapeRunTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

        private static readonly PageStats TwentyNew = new(20, 20, 0);

        private static SearchProfile Profile(int maxPages = 3, int maxDetails = 2)
        {
            return SearchProfile.Create(
                "backend",
                SourceKey.Naukri,
                new SearchCriteria("c#", ["Pune"], null, null, remoteOnly: false),
                new RunLimits(
                    maxPages,
                    60,
                    maxDetails,
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(6)
                ),
                Now
            );
        }

        private static ScrapeRun Searching(int maxPages = 3, int maxDetails = 2)
        {
            var run = ScrapeRun.Create(Profile(maxPages, maxDetails), Now);
            run.Start(Now);
            run.BeginSearching();
            return run;
        }

        private static ScrapeRun Enriching()
        {
            var run = Searching();
            run.RecordPage(TwentyNew, Now);
            run.BeginEnriching();
            return run;
        }

        private static ScrapeRun Persisting()
        {
            var run = Searching();
            run.RecordPage(TwentyNew, Now);
            run.BeginPersisting();
            return run;
        }

        [Fact]
        public void CreateSnapshotsProfileAndStartsPending()
        {
            var profile = Profile();

            var run = ScrapeRun.Create(profile, Now);

            Assert.NotEqual(Guid.Empty, run.Id.Value);
            Assert.Equal(profile.Id, run.ProfileId);
            Assert.Same(profile.Source, run.Source);
            Assert.Same(profile.Limits, run.Limits);
            Assert.Equal(RunStage.Pending, run.Stage);
            Assert.Same(RunStats.Empty, run.Stats);
            Assert.Equal(Now, run.CreatedAt);
            Assert.Null(run.StartedAt);
            Assert.Null(run.FinishedAt);
            Assert.False(run.IsTerminal);
            Assert.Empty(run.DomainEvents);
        }

        [Fact]
        public void CreateRejectsNullOrDisabledProfile()
        {
            var profile = Profile();
            profile.Disable(Now);

            Assert.Throws<ArgumentNullException>(() => ScrapeRun.Create(null!, Now));
            Assert.Throws<DomainException>(() => ScrapeRun.Create(profile, Now));
        }

        [Fact]
        public void StartMovesToStartingSessionAndRaisesStartedOnce()
        {
            var run = ScrapeRun.Create(Profile(), Now);

            run.Start(Now.AddSeconds(1));

            Assert.Equal(RunStage.StartingSession, run.Stage);
            Assert.Equal(Now.AddSeconds(1), run.StartedAt);
            var started = Assert.IsType<ScrapeRunStarted>(Assert.Single(run.DomainEvents));
            Assert.Equal(run.Id, started.RunId);
            Assert.Equal(run.ProfileId, started.ProfileId);
            Assert.Same(SourceKey.Naukri, started.Source);
        }

        [Fact]
        public void StartRejectsSecondStartAndTimeBeforeCreation()
        {
            var run = ScrapeRun.Create(Profile(), Now);

            Assert.Throws<DomainException>(() => run.Start(Now.AddTicks(-1)));
            run.Start(Now);
            Assert.Throws<DomainException>(() => run.Start(Now));
        }

        [Fact]
        public void BeginSearchingRequiresStartingSession()
        {
            var run = ScrapeRun.Create(Profile(), Now);

            Assert.Throws<DomainException>(run.BeginSearching);
            run.Start(Now);
            run.BeginSearching();
            Assert.Equal(RunStage.Searching, run.Stage);
        }

        [Fact]
        public void RecordPageAccumulatesStatsAndRaisesPageEvent()
        {
            var run = Searching();
            run.ClearDomainEvents();

            run.RecordPage(new PageStats(20, 15, 3), Now);
            run.RecordPage(new PageStats(10, 2, 1), Now.AddSeconds(5));

            Assert.Equal(2, run.Stats.PagesFetched);
            Assert.Equal(30, run.Stats.PostingsFound);
            Assert.Equal(17, run.Stats.PostingsNew);
            Assert.Equal(4, run.Stats.PostingsUpdated);
            var second = Assert.IsType<SearchPageFetched>(run.DomainEvents[1]);
            Assert.Equal(2, second.PageNumber);
            Assert.Equal(10, second.JobCount);
            Assert.Equal(run.Id, second.RunId);
        }

        [Fact]
        public void RecordPageEnforcesPageCap()
        {
            var run = Searching(maxPages: 1);
            run.RecordPage(TwentyNew, Now);

            Assert.Throws<DomainException>(() => run.RecordPage(TwentyNew, Now));
            Assert.Equal(1, run.Stats.PagesFetched);
        }

        [Fact]
        public void RecordPageRequiresSearchingAndAPage()
        {
            Assert.Throws<DomainException>(() =>
                ScrapeRun.Create(Profile(), Now).RecordPage(TwentyNew, Now)
            );
            Assert.Throws<DomainException>(() => Enriching().RecordPage(TwentyNew, Now));
            Assert.Throws<ArgumentNullException>(() => Searching().RecordPage(null!, Now));
        }

        [Fact]
        public void RecordFailureCountsWhileFetching()
        {
            var searching = Searching();
            var enriching = Enriching();

            searching.RecordFailure();
            enriching.RecordFailure();

            Assert.Equal(1, searching.Stats.Failures);
            Assert.Equal(1, enriching.Stats.Failures);
            Assert.Throws<DomainException>(() => Persisting().RecordFailure());
        }

        [Fact]
        public void BeginEnrichingRequiresSearchingAndDetailCap()
        {
            Assert.Throws<DomainException>(() => Searching(maxDetails: 0).BeginEnriching());
            Assert.Throws<DomainException>(() => Enriching().BeginEnriching());
            Assert.Equal(RunStage.Enriching, Enriching().Stage);
        }

        [Fact]
        public void RecordDetailEnforcesDetailCap()
        {
            var run = Enriching();

            run.RecordDetail();
            run.RecordDetail();

            Assert.Equal(2, run.Stats.DetailsFetched);
            Assert.Throws<DomainException>(run.RecordDetail);
            Assert.Throws<DomainException>(() => Searching().RecordDetail());
        }

        [Fact]
        public void BeginPersistingFromSearchingOrEnriching()
        {
            var fromSearching = Searching();
            var fromEnriching = Enriching();

            fromSearching.BeginPersisting();
            fromEnriching.BeginPersisting();

            Assert.Equal(RunStage.Persisting, fromSearching.Stage);
            Assert.Equal(RunStage.Persisting, fromEnriching.Stage);
            Assert.Throws<DomainException>(() => Persisting().BeginPersisting());
        }

        [Theory]
        [InlineData(ChallengeKind.SoftChallenge)]
        [InlineData(ChallengeKind.Captcha)]
        [InlineData(ChallengeKind.LoginRequired)]
        [InlineData(ChallengeKind.AccessDenied)]
        public void PauseForChallengeAcceptsWalls(ChallengeKind kind)
        {
            var run = Searching();
            run.ClearDomainEvents();

            run.PauseForChallenge(kind, Now);

            Assert.Equal(RunStage.PausedForChallenge, run.Stage);
            Assert.Equal(kind, run.PauseReason);
            Assert.Equal(1, run.Stats.Challenges);
            var paused = Assert.IsType<RunPausedForChallenge>(Assert.Single(run.DomainEvents));
            Assert.Equal(kind, paused.Kind);
            Assert.Equal(run.Id, paused.RunId);
        }

        [Theory]
        [InlineData(ChallengeKind.None)]
        [InlineData(ChallengeKind.RateLimited)]
        [InlineData(ChallengeKind.SourceChanged)]
        [InlineData(ChallengeKind.TransientError)]
        public void PauseForChallengeRejectsNonWalls(ChallengeKind kind)
        {
            var run = Searching();

            Assert.Throws<DomainException>(() => run.PauseForChallenge(kind, Now));
            Assert.Equal(RunStage.Searching, run.Stage);
        }

        [Fact]
        public void PauseForChallengeOnlyWhileFetching()
        {
            Assert.Throws<DomainException>(() =>
                Persisting().PauseForChallenge(ChallengeKind.Captcha, Now)
            );
        }

        [Fact]
        public void ResumeReturnsToSearchingAndClearsReason()
        {
            var run = Searching();
            run.PauseForChallenge(ChallengeKind.Captcha, Now);

            run.Resume(Now.AddMinutes(5));

            Assert.Equal(RunStage.Searching, run.Stage);
            Assert.Null(run.PauseReason);
        }

        [Fact]
        public void ResumeReturnsToEnrichingWhenPausedThere()
        {
            var run = Enriching();
            run.PauseForChallenge(ChallengeKind.SoftChallenge, Now);

            run.Resume(Now.AddMinutes(5));

            Assert.Equal(RunStage.Enriching, run.Stage);
        }

        [Fact]
        public void ResumeRejectsActiveRun()
        {
            Assert.Throws<DomainException>(() => Searching().Resume(Now));
        }

        [Fact]
        public void RateLimitWaitsForRetryWindow()
        {
            var run = Searching();
            run.ClearDomainEvents();
            var retryAt = Now.AddMinutes(30);

            run.RateLimit(retryAt, Now);

            Assert.Equal(RunStage.RateLimited, run.Stage);
            Assert.Equal(retryAt, run.RetryAt);
            var limited = Assert.IsType<RunRateLimited>(Assert.Single(run.DomainEvents));
            Assert.Equal(retryAt, limited.RetryAt);
            Assert.Throws<DomainException>(() => run.Resume(retryAt.AddTicks(-1)));

            run.Resume(retryAt);

            Assert.Equal(RunStage.Searching, run.Stage);
            Assert.Null(run.RetryAt);
        }

        [Fact]
        public void RateLimitRequiresSearchingAndFutureRetry()
        {
            Assert.Throws<DomainException>(() => Searching().RateLimit(Now.AddTicks(-1), Now));
            Assert.Throws<DomainException>(() => Enriching().RateLimit(Now.AddMinutes(1), Now));
        }

        [Fact]
        public void CompleteFinishesPersistedRunAndRaisesCompleted()
        {
            var run = Persisting();
            run.ClearDomainEvents();

            run.Complete(Now.AddMinutes(2));

            Assert.Equal(RunStage.Completed, run.Stage);
            Assert.True(run.IsTerminal);
            Assert.Equal(Now.AddMinutes(2), run.FinishedAt);
            var completed = Assert.IsType<RunCompleted>(Assert.Single(run.DomainEvents));
            Assert.Equal(run.Stats, completed.Stats);
        }

        [Fact]
        public void CannotCompleteBeforePersisting()
        {
            Assert.Throws<DomainException>(() => ScrapeRun.Create(Profile(), Now).Complete(Now));
            Assert.Throws<DomainException>(() => Searching().Complete(Now));
        }

        [Fact]
        public void CannotCompleteWithoutAPage()
        {
            var run = Searching();
            run.BeginPersisting();

            Assert.Throws<DomainException>(() => run.Complete(Now));
        }

        [Fact]
        public void CannotFinishBeforeStart()
        {
            var run = ScrapeRun.Create(Profile(), Now);
            run.Start(Now.AddMinutes(1));

            Assert.Throws<DomainException>(() => run.Cancel(Now));
            Assert.Throws<DomainException>(() => Persisting().Complete(Now.AddTicks(-1)));
        }

        [Fact]
        public void FailRecordsTrimmedReasonFromAnyActiveStage()
        {
            var run = Enriching();
            run.PauseForChallenge(ChallengeKind.Captcha, Now);
            run.ClearDomainEvents();

            run.Fail("  repeated challenge  ", Now.AddHours(1));

            Assert.Equal(RunStage.Failed, run.Stage);
            Assert.Equal("repeated challenge", run.FailureReason);
            Assert.Null(run.PauseReason);
            var failed = Assert.IsType<RunFailed>(Assert.Single(run.DomainEvents));
            Assert.Equal("repeated challenge", failed.Reason);
        }

        [Fact]
        public void FailFromStartingSessionIsAllowed()
        {
            var run = ScrapeRun.Create(Profile(), Now);
            run.Start(Now);

            run.Fail("session bootstrap failed", Now);

            Assert.Equal(RunStage.Failed, run.Stage);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("   ")]
        public void FailRequiresReason(string? reason)
        {
            Assert.Throws<DomainException>(() => Searching().Fail(reason, Now));
        }

        [Fact]
        public void CancelKeepsPartialStatsAndClearsRetryWindow()
        {
            var run = Searching();
            run.RecordPage(TwentyNew, Now);
            run.RateLimit(Now.AddMinutes(30), Now);
            run.ClearDomainEvents();

            run.Cancel(Now.AddMinutes(1));

            Assert.Equal(RunStage.Cancelled, run.Stage);
            Assert.Null(run.RetryAt);
            Assert.Equal(20, run.Stats.PostingsFound);
            var cancelled = Assert.IsType<RunCancelled>(Assert.Single(run.DomainEvents));
            Assert.Equal(20, cancelled.Stats.PostingsFound);
        }

        [Fact]
        public void CancelPendingRunIsAllowed()
        {
            var run = ScrapeRun.Create(Profile(), Now);

            run.Cancel(Now);

            Assert.Equal(RunStage.Cancelled, run.Stage);
        }

        [Fact]
        public void TerminalRunRejectsEveryTransition()
        {
            var run = Persisting();
            run.Complete(Now);

            Assert.Throws<DomainException>(() => run.Cancel(Now));
            Assert.Throws<DomainException>(() => run.Fail("x", Now));
            Assert.Throws<DomainException>(() => run.Resume(Now));
            Assert.Throws<DomainException>(() => run.Start(Now));
            Assert.Throws<DomainException>(run.RecordFailure);
            Assert.Throws<DomainException>(() => run.Complete(Now));
        }
    }
}
