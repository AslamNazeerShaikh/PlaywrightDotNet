using JobScrape.Application.Features.Scraping;
using JobScrape.Application.Features.Sources;
using JobScrape.Application.Tests.Scraping;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace JobScrape.Application.Tests
{
    public sealed class ListSourcesHandlerTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

        private static readonly RunLimits Policy = new(
            3,
            60,
            60,
            TimeSpan.FromMilliseconds(2500),
            TimeSpan.FromMilliseconds(6000)
        );

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private static SourceSettings Settings(SourceKey source, bool enabled = true)
        {
            return new SourceSettings(
                source,
                enabled,
                25,
                60,
                2,
                Policy.MaxPages,
                Policy.MaxJobs,
                TimeSpan.FromMinutes(45),
                Policy
            );
        }

        private static ListSourcesHandler Create(
            ScrapeHarness.InMemoryStore store,
            ComplianceConsent consent,
            params SourceSettings[] settings
        )
        {
            return new ListSourcesHandler(
                new SourceSettingsCatalog(settings),
                consent,
                store,
                store,
                new FakeTimeProvider(Now)
            );
        }

        [Fact]
        public async Task UntouchedSourcesShowPolicyZeroUsageAndClosedCircuit()
        {
            var store = new ScrapeHarness.InMemoryStore();
            var handler = Create(store, ComplianceConsent.None, Settings(SourceKey.Naukri));

            var result = await handler.HandleAsync(Ct);

            Assert.Equal(Now, result.AsOf);
            var naukri = Assert.Single(result.Items);
            Assert.Equal("naukri", naukri.Key);
            Assert.Equal("Standard", naukri.Tier);
            Assert.True(naukri.Enabled);
            Assert.Equal(new SourceGate(true, null), naukri.Gate);
            Assert.Equal(new SourcePolicy(3, 60, 60, 2500, 6000, 45), naukri.Policy);
            Assert.Equal(new SourceQuotaUsage("2026-11-02", 0, 25, 0, 60, 0, 2, 0), naukri.Quota);
            Assert.Equal(new SourceCircuit("Closed", null, false, 0, 0), naukri.Circuit);
            Assert.Empty(store.Quotas);
            Assert.Empty(store.Circuits);
        }

        [Fact]
        public async Task NullRunCapIsReportedAsNoCap()
        {
            var store = new ScrapeHarness.InMemoryStore();
            var handler = Create(
                store,
                ComplianceConsent.None,
                Settings(SourceKey.Naukri) with { DailyRunCap = null }
            );

            var naukri = Assert.Single((await handler.HandleAsync(Ct)).Items);

            Assert.Null(naukri.Quota.RunCap);
            Assert.Equal(0, naukri.Quota.RunsUsed);
        }

        [Fact]
        public async Task ReportsTodaysQuotaAndCircuitState()
        {
            var store = new ScrapeHarness.InMemoryStore();
            var quota = SourceQuota.StartDay(SourceKey.Naukri, Now);
            quota.TryStartRun(2, Now);
            quota.TryConsumePage(25, Now);
            quota.TryConsumePage(25, Now);
            quota.TryConsumeDetail(60, Now);
            quota.RecordChallenge(Now);
            store.Quotas.Add(quota);
            store.Quotas.Add(SourceQuota.StartDay(SourceKey.Naukri, Now.AddDays(-1)));
            var breaker = SourceCircuitBreaker.Create(SourceKey.Naukri);
            breaker.RecordFailure(ChallengeKind.AccessDenied, Now.AddDays(-2));
            store.Circuits.Add(breaker);
            var handler = Create(store, ComplianceConsent.None, Settings(SourceKey.Naukri));

            var naukri = Assert.Single((await handler.HandleAsync(Ct)).Items);

            Assert.Equal(new SourceQuotaUsage("2026-11-02", 2, 25, 1, 60, 1, 2, 1), naukri.Quota);
            Assert.Equal("Open", naukri.Circuit.State);
            Assert.Equal(breaker.OpenUntil, naukri.Circuit.OpenUntil);
            Assert.True(naukri.Circuit.CanProbe);
            Assert.Equal(1, naukri.Circuit.ConsecutiveFailures);
            Assert.Equal(0, naukri.Circuit.RecentFailures);
        }

        [Fact]
        public async Task GatesFollowTheTierAndConfiguredConsent()
        {
            var store = new ScrapeHarness.InMemoryStore();
            var sources = new[]
            {
                Settings(SourceKey.LinkedIn, enabled: false),
                Settings(SourceKey.Naukri),
                Settings(SourceKey.Indeed, enabled: false),
            };

            var none = await Create(store, ComplianceConsent.None, sources).HandleAsync(Ct);
            var configured = await Create(store, new ComplianceConsent(true, true, false), sources)
                .HandleAsync(Ct);

            Assert.Equal(["indeed", "linkedin", "naukri"], none.Items.Select(s => s.Key));
            Assert.Equal(["Restricted", "HighRisk", "Standard"], none.Items.Select(s => s.Tier));
            Assert.Equal([false, false, true], none.Items.Select(s => s.Gate.Allowed));
            Assert.Contains(
                "AcknowledgeSourceRisk",
                none.Items[0].Gate.Reason,
                StringComparison.Ordinal
            );
            Assert.True(configured.Items[0].Gate.Allowed);

            // Flags alone never unlock a high-risk source: interactive
            // confirmation is per run, so the gate names it as missing.
            Assert.False(configured.Items[1].Gate.Allowed);
            Assert.Contains(
                "interactive",
                configured.Items[1].Gate.Reason,
                StringComparison.Ordinal
            );
        }

        [Fact]
        public void ConstructorRejectsNulls()
        {
            var store = new ScrapeHarness.InMemoryStore();
            var catalog = new SourceSettingsCatalog([]);
            var clock = new FakeTimeProvider();
            var consent = ComplianceConsent.None;

            Assert.Throws<ArgumentNullException>(() =>
                new ListSourcesHandler(null!, consent, store, store, clock)
            );
            Assert.Throws<ArgumentNullException>(() =>
                new ListSourcesHandler(catalog, null!, store, store, clock)
            );
            Assert.Throws<ArgumentNullException>(() =>
                new ListSourcesHandler(catalog, consent, null!, store, clock)
            );
            Assert.Throws<ArgumentNullException>(() =>
                new ListSourcesHandler(catalog, consent, store, null!, clock)
            );
            Assert.Throws<ArgumentNullException>(() =>
                new ListSourcesHandler(catalog, consent, store, store, null!)
            );
        }
    }
}
