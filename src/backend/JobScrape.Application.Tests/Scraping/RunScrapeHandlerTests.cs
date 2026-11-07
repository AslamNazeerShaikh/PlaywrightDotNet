using JobScrape.Application.Features.Scraping;
using JobScrape.Application.Results;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using Microsoft.Extensions.Logging;
using Xunit;
using static JobScrape.Application.Tests.Scraping.ScrapeHarness;

namespace JobScrape.Application.Tests.Scraping
{
    public sealed class RunScrapeHandlerTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private static async Task<ScrapeRunSummary> RunOkAsync(ScrapeHarness harness)
        {
            var result = await harness.Build().HandleAsync(harness.Request, Ct);
            Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
            return result.Value;
        }

        private static async Task<ErrorInfo> RunFailAsync(ScrapeHarness harness)
        {
            var result = await harness.Build().HandleAsync(harness.Request, Ct);
            Assert.True(result.IsFailure);
            Assert.Empty(harness.Store.Runs);
            return result.Error;
        }

        private static SearchPageResult RangedPage(
            string? next,
            string range,
            params JobSnapshot[] jobs
        )
        {
            return new SearchPageResult(
                jobs,
                next,
                new PageFingerprint(jobs[0].ExternalJobId, range),
                ChallengeAssessment.Clear
            );
        }

        // ---- happy path ----

        [Fact]
        public async Task ReportsTheRunIdOnceRecordedAndNeverForARefusal()
        {
            var harness = new ScrapeHarness();
            harness.Source.Then(Page(null, Job("1")));
            var reported = new List<ScrapeRunId>();

            var result = await harness.Build().HandleAsync(harness.Request, reported.Add, Ct);

            Assert.Equal(result.Value.RunId, Assert.Single(reported).Value);
            Assert.Equal(Assert.Single(harness.Store.Runs).Id, reported[0]);

            var refused = new ScrapeHarness();
            refused.Store.SearchProfiles.Clear();
            var refusal = await refused.Build().HandleAsync(refused.Request, reported.Add, Ct);
            Assert.True(refusal.IsFailure);
            Assert.Single(reported);
        }

        [Fact]
        public async Task PaginatesPersistsAndCompletes()
        {
            var harness = new ScrapeHarness();
            harness.Source.Then(Page("p2", Job("1"), Job("2")), Page(null, Job("3"), Job("4")));

            var summary = await RunOkAsync(harness);

            Assert.Equal(RunStage.Completed, summary.Stage);
            Assert.Equal("naukri", summary.Source);
            Assert.Equal(2, summary.Stats.PagesFetched);
            Assert.Equal(4, summary.Stats.PostingsFound);
            Assert.Equal(4, summary.Stats.PostingsNew);
            Assert.Null(summary.ArtifactDirectory);
            Assert.Equal(4, harness.Store.Postings.Count);
            Assert.Equal(
                [new SearchPageRequest(1, null), new SearchPageRequest(2, "p2")],
                harness.Source.Requests
            );
            Assert.Equal([TimeSpan.FromSeconds(2)], harness.Delay.Waits);
            var quota = Assert.Single(harness.Store.Quotas);
            Assert.Equal(1, quota.RunsStarted);
            Assert.Equal(2, quota.PagesFetched);
            Assert.Equal(CircuitState.Closed, Assert.Single(harness.Store.Circuits).State);
            Assert.Equal(Start, harness.Browser.LastUsedAt);
            Assert.True(harness.Store.Saves >= 4);
        }

        [Fact]
        public async Task StopsAtThePageCap()
        {
            var harness = new ScrapeHarness(maxPages: 1);
            harness.Source.Then(Page("p2", Job("1")));

            var summary = await RunOkAsync(harness);

            Assert.Equal(RunStage.Completed, summary.Stage);
            Assert.Single(harness.Source.Requests);
        }

        [Fact]
        public async Task TruncatesToTheJobCapAndStops()
        {
            var harness = new ScrapeHarness(maxJobs: 3);
            harness.Source.Then(Page("p2", Job("1"), Job("2")), Page("p3", Job("3"), Job("4")));

            var summary = await RunOkAsync(harness);

            Assert.Equal(3, summary.Stats.PostingsFound);
            Assert.Equal(3, harness.Store.Postings.Count);
            Assert.Equal(2, harness.Source.Requests.Count);
        }

        [Fact]
        public async Task StartsAtTheConfiguredPageWithoutFetchingEarlierPages()
        {
            var harness = new ScrapeHarness(startPage: 2, maxPages: 2);
            harness.Source.Then(
                Page("p2", Job("skip")),
                Page("p3", Job("a")),
                Page(null, Job("b"))
            );

            var summary = await RunOkAsync(harness);

            Assert.Equal(RunStage.Completed, summary.Stage);
            Assert.Equal(2, summary.Stats.PagesFetched);
            Assert.Equal(2, summary.Stats.PostingsNew);
            Assert.Equal(
                [
                    new SearchPageRequest(1, null),
                    new SearchPageRequest(2, "p2"),
                    new SearchPageRequest(3, "p3"),
                ],
                harness.Source.Requests
            );
            Assert.DoesNotContain(harness.Store.Postings, p => p.ExternalJobId.Value == "skip");
            Assert.Equal(
                ["a", "b"],
                harness.Store.Postings.Select(p => p.ExternalJobId.Value).Order()
            );
        }

        [Fact]
        public async Task ClosedBrowserOnFirstPageEndsTheRunWithoutCrashing()
        {
            // A closed browser surfaces as a transient wall on the probe
            // page: the run fails gracefully instead of crashing.
            var harness = new ScrapeHarness();
            harness.Source.Then(
                Wall(ChallengeKind.TransientError),
                Wall(ChallengeKind.TransientError)
            );

            var summary = await RunOkAsync(harness);

            Assert.Equal(RunStage.Failed, summary.Stage);
            Assert.Equal(1, summary.Stats.Failures);
            Assert.Equal(
                [new SearchPageRequest(1, null), new SearchPageRequest(1, null)],
                harness.Source.Requests
            );
        }

        [Fact]
        public async Task StartPagePastTheLastPageFailsTheRunGracefully()
        {
            var harness = new ScrapeHarness(startPage: 31, maxPages: 2);
            harness.Source.ThenThrow(new StartPageOutOfRangeException(31, 30));

            var summary = await RunOkAsync(harness);

            Assert.Equal(RunStage.Failed, summary.Stage);
            Assert.Equal(1, summary.Stats.Failures);
            Assert.Empty(harness.Store.Postings);
            Assert.Contains("past the last page", summary.FailureReason, StringComparison.Ordinal);
            Assert.Equal([new SearchPageRequest(1, null)], harness.Source.Requests);
        }

        [Fact]
        public async Task RepeatedPageFailsTheRunAsAStall()
        {
            // Next is live and the page did not change: the run re-requests
            // the page once, then fails when the retry repeats too. The
            // fingerprint keys on the first card, so the repeated page keeps
            // Job("1") and adds a never-seen card that must be discarded.
            var harness = new ScrapeHarness();
            harness.Source.Then(
                RangedPage("p2", "1 - 20 of 54", Job("1")),
                RangedPage("p3", "1 - 20 of 54", Job("1"), Job("2")),
                RangedPage("p3", "1 - 20 of 54", Job("1"), Job("2"))
            );

            var summary = await RunOkAsync(harness);

            Assert.Equal(RunStage.Failed, summary.Stage);
            Assert.Equal(1, summary.Stats.Failures);
            Assert.Equal(1, summary.Stats.PostingsFound);
            Assert.Contains("page 2", summary.FailureReason, StringComparison.Ordinal);
            Assert.Contains("1 - 20 of 54", summary.FailureReason, StringComparison.Ordinal);
            Assert.Equal(
                [
                    new SearchPageRequest(1, null),
                    new SearchPageRequest(2, "p2"),
                    new SearchPageRequest(2, "p2"),
                ],
                harness.Source.Requests
            );
            var retry = Assert.Single(
                harness.Logger.Entries,
                e => e.Message.Contains("retrying page 2", StringComparison.Ordinal)
            );
            Assert.Equal(summary.RunId, retry.Properties["RunId"]);
            Assert.Equal(2, retry.Properties["Page"]);
            Assert.DoesNotContain(
                harness.Store.Postings,
                p => p.ExternalJobId.Value == "2"
            );
            var progress = Assert.Single(harness.Store.Runs).Progress;
            Assert.NotNull(progress);
            Assert.Equal("p2", progress.NextCursor);
        }

        [Fact]
        public async Task RetriedPageAdvancesAndTheRunContinues()
        {
            // The stuck page serves page 1 content under the page 2 URL; the
            // one retry gets the real page 2, and the run continues past it.
            var harness = new ScrapeHarness();
            harness.Source.Then(
                RangedPage("p2", "1 - 20 of 54", Job("1")),
                RangedPage("p3", "1 - 20 of 54", Job("1"), Job("2")),
                RangedPage("p4", "21 - 40 of 54", Job("3"), Job("4")),
                RangedPage(null, "41 - 54 of 54", Job("5"))
            );

            var summary = await RunOkAsync(harness);

            Assert.Equal(RunStage.Completed, summary.Stage);
            Assert.Equal(0, summary.Stats.Failures);
            Assert.Equal(4, summary.Stats.PostingsFound);
            Assert.Equal(
                [
                    new SearchPageRequest(1, null),
                    new SearchPageRequest(2, "p2"),
                    new SearchPageRequest(2, "p2"),
                    new SearchPageRequest(3, "p4"),
                ],
                harness.Source.Requests
            );
            Assert.Single(
                harness.Logger.Entries,
                e => e.Message.Contains("retrying page 2", StringComparison.Ordinal)
            );
            Assert.Equal(
                ["1", "3", "4", "5"],
                harness.Store.Postings.Select(p => p.ExternalJobId.Value).Order()
            );
        }

        [Fact]
        public async Task RepeatedFinalPageWithoutNextCompletesCleanly()
        {
            // Naukri re-reports the last page with Next absent when the
            // listing ends: a repeated fingerprint without a live Next is
            // the clean end of the run, not a stall.
            var harness = new ScrapeHarness();
            harness.Source.Then(Page("p2", Job("1")), Page(null, Job("1")));

            var summary = await RunOkAsync(harness);

            Assert.Equal(RunStage.Completed, summary.Stage);
            Assert.Equal(0, summary.Stats.Failures);
            Assert.Equal(1, summary.Stats.PostingsFound);
            Assert.Null(summary.FailureReason);
        }

        [Fact]
        public async Task RepeatedPreWindowPageRetriesOnceThenAdvancesWithoutKeepingItsCards()
        {
            // StartPage above 1 visits bare pages first; the stuck page 2
            // serves page 1 content with a live Next, so the run re-requests
            // it once, then advances past it instead of failing or keeping
            // its cards.
            var harness = new ScrapeHarness(startPage: 3, maxPages: 2);
            harness.Source.Then(
                RangedPage("p2", "1 - 20 of 54", Job("1")),
                RangedPage("p3", "1 - 20 of 54", Job("1")),
                RangedPage("p3", "1 - 20 of 54", Job("1")),
                RangedPage("p4", "21 - 40 of 54", Job("3")),
                RangedPage(null, "41 - 54 of 54", Job("4"))
            );

            var summary = await RunOkAsync(harness);

            Assert.Equal(RunStage.Completed, summary.Stage);
            Assert.Equal(0, summary.Stats.Failures);
            Assert.Equal(
                [
                    new SearchPageRequest(1, null),
                    new SearchPageRequest(2, "p2"),
                    new SearchPageRequest(2, "p2"),
                    new SearchPageRequest(3, "p3"),
                    new SearchPageRequest(4, "p4"),
                ],
                harness.Source.Requests
            );
            Assert.Single(
                harness.Logger.Entries,
                e => e.Message.Contains("retrying page 2", StringComparison.Ordinal)
            );
            Assert.DoesNotContain(
                harness.Store.Postings,
                p => p.ExternalJobId.Value == "1"
            );
            Assert.Equal(
                ["3", "4"],
                harness.Store.Postings.Select(p => p.ExternalJobId.Value).Order()
            );
        }

        [Fact]
        public async Task ReRunningIsIdempotent()
        {
            var harness = new ScrapeHarness();
            harness.Source.Then(
                Page(null, Job("1"), Job("2")),
                Page(null, Job("1"), Job("2", "Lead"))
            );

            await RunOkAsync(harness);
            harness.Clock.Advance(TimeSpan.FromHours(1));
            var second = await RunOkAsync(harness);

            Assert.Equal(2, harness.Store.Postings.Count);
            Assert.Equal(2, second.Stats.PostingsFound);
            Assert.Equal(0, second.Stats.PostingsNew);
            Assert.Equal(1, second.Stats.PostingsUpdated);
            Assert.All(harness.Store.Postings, p => Assert.Equal(Start.AddHours(1), p.LastSeenAt));
            Assert.All(harness.Store.Postings, p => Assert.Equal(Start, p.FirstSeenAt));
        }

        [Fact]
        public async Task DuplicateInvalidAndForeignCardsAreDropped()
        {
            var harness = new ScrapeHarness();
            harness.Source.Then(
                Page(
                    null,
                    Job("1"),
                    Job("1"),
                    Job("2", title: " "),
                    Job("3") with
                    {
                        Url = "https://evil.example/x",
                    },
                    Job("4", source: SourceKey.Indeed),
                    Job(" "),
                    Job("5")
                )
            );

            var summary = await RunOkAsync(harness);

            Assert.Equal(2, summary.Stats.PostingsFound);
            Assert.Equal(["1", "5"], harness.Store.Postings.Select(p => p.ExternalJobId.Value));
        }

        // ---- gates ----

        [Fact]
        public async Task RejectsEmptyIdsAndMissingConsentBeforeAnyLookup()
        {
            var harness = new ScrapeHarness();

            var result = await harness
                .Build()
                .HandleAsync(new RunScrapeRequest(default, default, null!), Ct);

            Assert.Equal("VALIDATION_FAILED", result.Error.Code);
            var errors = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string[]>>(
                result.Error.Metadata!["errors"]
            );
            Assert.Equal(["browserProfileId", "consent", "profileId"], errors.Keys.Order());
            Assert.Empty(harness.Store.Circuits);
        }

        [Fact]
        public async Task MissingProfileFails()
        {
            var harness = new ScrapeHarness();
            harness.Store.SearchProfiles.Clear();

            Assert.Equal("PROFILE_NOT_FOUND", (await RunFailAsync(harness)).Code);
        }

        [Fact]
        public async Task DisabledProfileFails()
        {
            var harness = new ScrapeHarness();
            harness.Profile.Disable(Start);

            Assert.Equal("PROFILE_DISABLED", (await RunFailAsync(harness)).Code);
        }

        [Fact]
        public async Task MissingAdapterOrSettingsMeansSourceUnavailable()
        {
            var noAdapter = new ScrapeHarness { RegisterSource = false };
            var noSettings = new ScrapeHarness();
            noSettings.Settings = noSettings.Settings with { Source = SourceKey.Indeed };

            Assert.Equal("SOURCE_UNAVAILABLE", (await RunFailAsync(noAdapter)).Code);
            Assert.Equal("SOURCE_UNAVAILABLE", (await RunFailAsync(noSettings)).Code);
        }

        [Fact]
        public async Task DisabledSourceFails()
        {
            var harness = new ScrapeHarness();
            harness.Settings = harness.Settings with { Enabled = false };

            Assert.Equal("SOURCE_DISABLED", (await RunFailAsync(harness)).Code);
        }

        [Fact]
        public async Task RestrictedSourceWithoutAcknowledgmentIsBlocked()
        {
            var harness = new ScrapeHarness(SourceKey.Indeed) { Consent = ComplianceConsent.None };

            var error = await RunFailAsync(harness);

            Assert.Equal("COMPLIANCE_BLOCKED", error.Code);
            Assert.Contains("AcknowledgeSourceRisk", error.Detail, StringComparison.Ordinal);
        }

        [Fact]
        public async Task OpenCircuitFailsWithRetryHint()
        {
            var harness = new ScrapeHarness();
            var breaker = SourceCircuitBreaker.Create(SourceKey.Naukri);
            breaker.RecordFailure(ChallengeKind.AccessDenied, Start);
            harness.Store.Circuits.Add(breaker);

            var error = await RunFailAsync(harness);

            Assert.Equal("SOURCE_COOLING_DOWN", error.Code);
            Assert.Equal(breaker.OpenUntil, error.Metadata!["retryAfter"]);
        }

        [Fact]
        public async Task MissingBrowserProfileFails()
        {
            var harness = new ScrapeHarness();
            harness.Store.BrowserProfiles.Clear();

            Assert.Equal("BROWSER_PROFILE_NOT_FOUND", (await RunFailAsync(harness)).Code);
        }

        [Fact]
        public async Task BrowserProfileOfAnotherSourceFailsValidation()
        {
            var harness = new ScrapeHarness();
            harness.Store.BrowserProfiles.Clear();
            var other = BrowserProfile.Create(
                "li",
                SourceKey.LinkedIn,
                BrowserEngine.Chromium,
                BrowserChannel.Bundled
            );
            harness.Store.BrowserProfiles.Add(other);

            var result = await harness
                .Build()
                .HandleAsync(harness.Request with { BrowserProfileId = other.Id }, Ct);

            Assert.Equal("VALIDATION_FAILED", result.Error.Code);
        }

        [Fact]
        public async Task CoolingDownOrResetRequiredBrowserFails()
        {
            var cooling = new ScrapeHarness();
            cooling.Browser.MarkChallenged(ChallengeKind.Captcha, Start, TimeSpan.FromHours(1));
            var reset = new ScrapeHarness();
            for (var i = 0; i < BrowserProfile.MaxConsecutiveChallenges; i++)
            {
                reset.Browser.MarkChallenged(
                    ChallengeKind.Captcha,
                    Start.AddHours(-10 + (2 * i)),
                    TimeSpan.FromHours(1)
                );
            }

            Assert.Equal("SESSION_COOLING_DOWN", (await RunFailAsync(cooling)).Code);
            Assert.Equal("SESSION_RESET_REQUIRED", (await RunFailAsync(reset)).Code);
        }

        [Fact]
        public async Task DailyRunQuotaIsEnforced()
        {
            var harness = new ScrapeHarness();
            harness.Settings = harness.Settings with { DailyRunCap = 1 };
            harness.Source.Then(Page(null, Job("1")));
            await RunOkAsync(harness);

            var result = await harness.Build().HandleAsync(harness.Request, Ct);

            Assert.Equal("QUOTA_EXCEEDED", result.Error.Code);
            Assert.Single(harness.Store.Runs);
        }

        [Fact]
        public async Task HighPageCapStopsAtTheLastAvailablePage()
        {
            var harness = new ScrapeHarness(maxPages: 600);
            harness.Source.Then(Page(null, Job("1")));

            var summary = await RunOkAsync(harness);

            Assert.Equal(RunStage.Completed, summary.Stage);
            Assert.Equal(1, summary.Stats.PagesFetched);
        }

        [Fact]
        public async Task NoDailyRunCapAllowsAnotherRun()
        {
            var harness = new ScrapeHarness();
            harness.Settings = harness.Settings with { DailyRunCap = null };
            harness.Source.Then(Page(null, Job("1")));
            await RunOkAsync(harness);
            harness.Source.Then(Page(null, Job("2")));
            await RunOkAsync(harness);
            harness.Source.Then(Page(null, Job("3")));

            var result = await harness.Build().HandleAsync(harness.Request, Ct);

            Assert.True(result.IsSuccess);
            Assert.Equal(3, harness.Store.Runs.Count);
            Assert.Equal(3, Assert.Single(harness.Store.Quotas).RunsStarted);
        }

        // ---- session ----

        [Fact]
        public async Task CaptchaAtSessionCheckFailsRunAndCoolsProfile()
        {
            var harness = new ScrapeHarness();
            harness.Sessions.Result = new ChallengeAssessment(ChallengeKind.Captcha, 1, "verify");

            var summary = await RunOkAsync(harness);

            Assert.Equal(RunStage.Failed, summary.Stage);
            Assert.Contains("Captcha", summary.FailureReason, StringComparison.Ordinal);
            Assert.Equal(SessionState.CoolingDown, harness.Browser.State);
            Assert.Empty(harness.Source.Requests);
        }

        [Fact]
        public async Task LoginWallExpiresAHealthySessionInsteadOfCoolingIt()
        {
            var harness = new ScrapeHarness();
            harness.Sessions.Result = new ChallengeAssessment(
                ChallengeKind.LoginRequired,
                1,
                "authwall"
            );

            var summary = await RunOkAsync(harness);

            Assert.Equal(RunStage.Failed, summary.Stage);
            Assert.Equal(SessionState.Expired, harness.Browser.State);
            Assert.True(harness.Browser.CanLaunch(Start));
        }

        [Fact]
        public async Task LoginWallOnAnUnverifiedProfileLeavesItNew()
        {
            var harness = new ScrapeHarness();
            harness.Browser.Reset();
            harness.Sessions.Result = new ChallengeAssessment(
                ChallengeKind.LoginRequired,
                1,
                "authwall"
            );

            await RunOkAsync(harness);

            Assert.Equal(SessionState.New, harness.Browser.State);
        }

        [Fact]
        public async Task TransientSessionFailureFailsRunWithoutBlamingProfile()
        {
            var harness = new ScrapeHarness();
            harness.Sessions.Result = new ChallengeAssessment(
                ChallengeKind.TransientError,
                1,
                "timeout"
            );

            var summary = await RunOkAsync(harness);

            Assert.Equal(RunStage.Failed, summary.Stage);
            Assert.Equal(SessionState.Healthy, harness.Browser.State);
        }

        // ---- walls ----

        [Fact]
        public async Task CaptchaPausesKeepsEarlierPagesAndCapturesEvidence()
        {
            var harness = new ScrapeHarness();
            harness.Source.Then(Page("p2", Job("1"), Job("2")), Wall(ChallengeKind.Captcha));

            var summary = await RunOkAsync(harness);

            Assert.Equal(RunStage.PausedForChallenge, summary.Stage);
            Assert.Equal("Captcha", summary.PauseReason);
            Assert.Equal($"runs/{summary.RunId}", summary.ArtifactDirectory);
            Assert.Equal(ChallengeKind.Captcha, Assert.Single(harness.Artifacts.Captured).Kind);
            Assert.Equal(2, harness.Store.Postings.Count);
            Assert.Equal(1, summary.Stats.Challenges);
            Assert.Equal(SessionState.CoolingDown, harness.Browser.State);
            Assert.Equal(Start + harness.Settings.ChallengeCooldown, harness.Browser.CooldownUntil);
            Assert.Equal(1, Assert.Single(harness.Store.Quotas).Challenges);
            Assert.Equal(1, Assert.Single(harness.Store.Circuits).ConsecutiveFailures);
            Assert.Equal(2, harness.Source.Requests.Count);
        }

        [Fact]
        public async Task AccessDeniedPausesAndOpensTheCircuit()
        {
            var harness = new ScrapeHarness();
            harness.Source.Then(Wall(ChallengeKind.AccessDenied));

            var summary = await RunOkAsync(harness);

            Assert.Equal(RunStage.PausedForChallenge, summary.Stage);
            Assert.Equal(CircuitState.Open, Assert.Single(harness.Store.Circuits).State);
        }

        [Fact]
        public async Task LoginWallMidSearchPausesAndExpiresSession()
        {
            var harness = new ScrapeHarness();
            harness.Source.Then(Wall(ChallengeKind.LoginRequired));

            var summary = await RunOkAsync(harness);

            Assert.Equal(RunStage.PausedForChallenge, summary.Stage);
            Assert.Equal(SessionState.Expired, harness.Browser.State);
        }

        [Fact]
        public async Task RateLimitWaitsOutTheStatedWindowWithoutEvidenceOrRetry()
        {
            var harness = new ScrapeHarness();
            harness.Source.Then(Wall(ChallengeKind.RateLimited, TimeSpan.FromMinutes(10)));

            var summary = await RunOkAsync(harness);

            Assert.Equal(RunStage.RateLimited, summary.Stage);
            Assert.Equal(Start.AddMinutes(10), summary.RetryAt);
            Assert.Empty(harness.Artifacts.Captured);
            Assert.Single(harness.Source.Requests);
            Assert.Equal(SessionState.Healthy, harness.Browser.State);
            Assert.Equal(1, Assert.Single(harness.Store.Circuits).ConsecutiveFailures);
        }

        [Fact]
        public async Task ChangedPageFailsWithEvidenceButNoCooldown()
        {
            var harness = new ScrapeHarness();
            harness.Source.Then(Wall(ChallengeKind.SourceChanged));

            var summary = await RunOkAsync(harness);

            Assert.Equal(RunStage.Failed, summary.Stage);
            Assert.NotNull(summary.ArtifactDirectory);
            Assert.Equal(SessionState.Healthy, harness.Browser.State);
            Assert.Empty(Assert.Single(harness.Store.Circuits).RecentFailures);
            Assert.Equal(0, Assert.Single(harness.Store.Quotas).Challenges);
        }

        // ---- transient errors and quota ----

        [Fact]
        public async Task TransientErrorIsRetriedOnceThenSucceeds()
        {
            var harness = new ScrapeHarness();
            harness.Source.Then(Wall(ChallengeKind.TransientError), Page(null, Job("1")));

            var summary = await RunOkAsync(harness);

            Assert.Equal(RunStage.Completed, summary.Stage);
            Assert.Equal(2, harness.Source.Requests.Count);
            Assert.Equal([RetryPolicy.BaseDelay], harness.Delay.Waits);
        }

        [Fact]
        public async Task ExhaustedRetriesOnFirstPageFailTheRun()
        {
            var harness = new ScrapeHarness();
            harness.Source.Then(
                Wall(ChallengeKind.TransientError),
                Wall(ChallengeKind.TransientError)
            );

            var summary = await RunOkAsync(harness);

            Assert.Equal(RunStage.Failed, summary.Stage);
            Assert.Equal(1, summary.Stats.Failures);
            Assert.Equal(2, harness.Source.Requests.Count);
        }

        [Fact]
        public async Task ExhaustedRetriesLaterKeepEarlierPagesAndComplete()
        {
            var harness = new ScrapeHarness();
            harness.Source.Then(
                Page("p2", Job("1")),
                Wall(ChallengeKind.TransientError),
                Wall(ChallengeKind.TransientError)
            );

            var summary = await RunOkAsync(harness);

            Assert.Equal(RunStage.Completed, summary.Stage);
            Assert.Equal(1, summary.Stats.Failures);
        }

        [Fact]
        public async Task PageQuotaExhaustionStopsBeforeTheNextPage()
        {
            var harness = new ScrapeHarness();
            harness.Settings = harness.Settings with { DailyPageCap = 1 };
            harness.Source.Then(Page("p2", Job("1")));

            var summary = await RunOkAsync(harness);

            Assert.Equal(RunStage.Completed, summary.Stage);
            Assert.Single(harness.Source.Requests);
        }

        [Fact]
        public async Task ZeroPageQuotaFailsTheRun()
        {
            var harness = new ScrapeHarness();
            harness.Settings = harness.Settings with { DailyPageCap = 0 };

            var summary = await RunOkAsync(harness);

            Assert.Equal(RunStage.Failed, summary.Stage);
            Assert.Empty(harness.Source.Requests);
        }

        // ---- cancellation ----

        [Fact]
        public async Task CancellationRecordsCancelledRunKeepsPartialResultsAndPropagates()
        {
            var harness = new ScrapeHarness();
            harness.Source.Then(Page("p2", Job("1")), Page(null, Job("2")));
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
            harness.Delay.CancelOnWait = cts;
            var savesBefore = 0;

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                savesBefore = harness.Store.Saves;
                await harness.Build().HandleAsync(harness.Request, cts.Token);
            });

            var run = Assert.Single(harness.Store.Runs);
            Assert.Equal(RunStage.Cancelled, run.Stage);
            Assert.Equal(1, run.Stats.PagesFetched);
            Assert.Single(harness.Store.Postings);
            Assert.True(harness.Store.Saves > savesBefore);
        }

        [Fact]
        public async Task CancellationDuringTheFinalSaveKeepsTheCompletedStage()
        {
            var harness = new ScrapeHarness();
            harness.Source.Then(Page(null, Job("1")));
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
            harness.Store.CancelOnSave = cts;
            harness.Store.CancelAtSave = 3; // start, page 1, then the completing save

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                harness.Build().HandleAsync(harness.Request, cts.Token)
            );

            Assert.Equal(RunStage.Completed, Assert.Single(harness.Store.Runs).Stage);
            Assert.Equal(4, harness.Store.Saves);
        }

        // ---- empty results and unexpected failures ----

        [Fact]
        public async Task EmptyFirstPageCompletesWithNoPostings()
        {
            var harness = new ScrapeHarness();
            harness.Source.Then(Page(null));

            var summary = await RunOkAsync(harness);

            Assert.Equal(RunStage.Completed, summary.Stage);
            Assert.Equal(1, summary.Stats.PagesFetched);
            Assert.Equal(0, summary.Stats.PostingsFound);
        }

        [Fact]
        public async Task UnexpectedAdapterExceptionFailsTheRunAndPropagates()
        {
            var harness = new ScrapeHarness();
            var boom = new InvalidOperationException("adapter bug");
            harness.Source.Then(Page("p2", Job("1"))).ThenThrow(boom);

            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                harness.Build().HandleAsync(harness.Request, Ct)
            );

            Assert.Same(boom, thrown);
            var run = Assert.Single(harness.Store.Runs);
            Assert.Equal(RunStage.Failed, run.Stage);
            Assert.Equal("Unexpected error; see logs.", run.FailureReason);
            Assert.Single(harness.Store.Postings);
            var logged = Assert.Single(harness.Logger.Entries, e => e.Level == LogLevel.Error);
            Assert.Same(boom, logged.Exception);
        }

        [Fact]
        public async Task UnexpectedExceptionAfterATerminalStageIsNotRecordedTwice()
        {
            var harness = new ScrapeHarness();
            harness.Sessions.Result = new ChallengeAssessment(ChallengeKind.Captcha, 1, "verify");
            harness.Store.FailAtSave = 2; // the save that records the failed run

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                harness.Build().HandleAsync(harness.Request, Ct)
            );

            var run = Assert.Single(harness.Store.Runs);
            Assert.Equal(RunStage.Failed, run.Stage);
            Assert.StartsWith("Session not ready", run.FailureReason, StringComparison.Ordinal);
        }

        // ---- structured logs ----

        [Fact]
        public async Task LogsUseNamedPropertiesForEachMilestone()
        {
            var harness = new ScrapeHarness();
            harness.Source.Then(
                Wall(ChallengeKind.TransientError),
                Page("p2", Job("1"), Job(" ")),
                Wall(ChallengeKind.Captcha)
            );

            var summary = await RunOkAsync(harness);

            var entries = harness.Logger.Entries;
            Assert.Contains(
                entries,
                e =>
                    e.Properties.ContainsKey("{OriginalFormat}")
                    && e.Message.Contains("started", StringComparison.Ordinal)
            );
            var retry = Assert.Single(entries, e => e.Properties.ContainsKey("Attempt"));
            Assert.Equal(1, retry.Properties["Attempt"]);
            var page = Assert.Single(entries, e => e.Properties.ContainsKey("Updated"));
            Assert.Equal(1, page.Properties["Found"]);
            Assert.Single(entries, e => e.Properties.ContainsKey("ExternalJobId"));
            var wall = Assert.Single(entries, e => e.Properties.ContainsKey("Evidence"));
            Assert.Equal(ChallengeKind.Captcha, wall.Properties["ChallengeKind"]);
            Assert.Equal("evidence:Captcha", wall.Properties["Evidence"]);
            var finished = Assert.Single(entries, e => e.Properties.ContainsKey("Stage"));
            Assert.Equal(RunStage.PausedForChallenge, finished.Properties["Stage"]);
            Assert.Equal(summary.RunId, finished.Properties["RunId"]);
        }

        [Fact]
        public async Task RefusalAndPaginationStopsAreLogged()
        {
            var refused = new ScrapeHarness();
            refused.Settings = refused.Settings with { Enabled = false };
            var capped = new ScrapeHarness(maxJobs: 1);
            capped.Source.Then(Page("p2", Job("1")));

            await refused.Build().HandleAsync(refused.Request, Ct);
            await RunOkAsync(capped);

            Assert.Equal(
                "SOURCE_DISABLED",
                Assert.Single(refused.Logger.Entries).Properties["ErrorCode"]
            );
            Assert.Contains(
                capped.Logger.Entries,
                e => Equals(e.Properties.GetValueOrDefault("Reason"), "job cap reached")
            );
        }

        // ---- guards ----

        [Fact]
        public async Task RejectsNullRequest()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                new ScrapeHarness().Build().HandleAsync(null!, Ct)
            );
        }

        [Fact]
        public void ConstructorRejectsNulls()
        {
            var h = new ScrapeHarness();
            var stores = h.Store.AsStores();
            var registry = new JobSourceRegistry([]);
            var catalog = new SourceSettingsCatalog([]);
            var retry = new RetryPolicy(2, new MinRandom());
            var logger = Microsoft
                .Extensions
                .Logging
                .Abstractions
                .NullLogger<RunScrapeHandler>
                .Instance;

            Assert.Throws<ArgumentNullException>(() =>
                new RunScrapeHandler(
                    null!,
                    registry,
                    catalog,
                    h.Sessions,
                    h.Artifacts,
                    h.Delay,
                    retry,
                    new MinRandom(),
                    h.Clock,
                    logger
                )
            );
            Assert.Throws<ArgumentNullException>(() =>
                new RunScrapeHandler(
                    stores,
                    null!,
                    catalog,
                    h.Sessions,
                    h.Artifacts,
                    h.Delay,
                    retry,
                    new MinRandom(),
                    h.Clock,
                    logger
                )
            );
            Assert.Throws<ArgumentNullException>(() =>
                new RunScrapeHandler(
                    stores,
                    registry,
                    null!,
                    h.Sessions,
                    h.Artifacts,
                    h.Delay,
                    retry,
                    new MinRandom(),
                    h.Clock,
                    logger
                )
            );
            Assert.Throws<ArgumentNullException>(() =>
                new RunScrapeHandler(
                    stores,
                    registry,
                    catalog,
                    null!,
                    h.Artifacts,
                    h.Delay,
                    retry,
                    new MinRandom(),
                    h.Clock,
                    logger
                )
            );
            Assert.Throws<ArgumentNullException>(() =>
                new RunScrapeHandler(
                    stores,
                    registry,
                    catalog,
                    h.Sessions,
                    null!,
                    h.Delay,
                    retry,
                    new MinRandom(),
                    h.Clock,
                    logger
                )
            );
            Assert.Throws<ArgumentNullException>(() =>
                new RunScrapeHandler(
                    stores,
                    registry,
                    catalog,
                    h.Sessions,
                    h.Artifacts,
                    null!,
                    retry,
                    new MinRandom(),
                    h.Clock,
                    logger
                )
            );
            Assert.Throws<ArgumentNullException>(() =>
                new RunScrapeHandler(
                    stores,
                    registry,
                    catalog,
                    h.Sessions,
                    h.Artifacts,
                    h.Delay,
                    null!,
                    new MinRandom(),
                    h.Clock,
                    logger
                )
            );
            Assert.Throws<ArgumentNullException>(() =>
                new RunScrapeHandler(
                    stores,
                    registry,
                    catalog,
                    h.Sessions,
                    h.Artifacts,
                    h.Delay,
                    retry,
                    null!,
                    h.Clock,
                    logger
                )
            );
            Assert.Throws<ArgumentNullException>(() =>
                new RunScrapeHandler(
                    stores,
                    registry,
                    catalog,
                    h.Sessions,
                    h.Artifacts,
                    h.Delay,
                    retry,
                    new MinRandom(),
                    null!,
                    logger
                )
            );
            Assert.Throws<ArgumentNullException>(() =>
                new RunScrapeHandler(
                    stores,
                    registry,
                    catalog,
                    h.Sessions,
                    h.Artifacts,
                    h.Delay,
                    retry,
                    new MinRandom(),
                    h.Clock,
                    null!
                )
            );
            Assert.Throws<ArgumentNullException>(() => ScrapeRunSummary.From(null!, null));
        }
    }
}
