using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.Scraping;
using JobScrape.Application.Features.Sources;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using JobScrape.Mcp.Tools;
using Xunit;

namespace JobScrape.Mcp.Tests
{
    public sealed class SourcesToolsTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private static SourcesTools Tools()
        {
            var settings = new SourceSettings(
                SourceKey.Naukri,
                true,
                25,
                60,
                2,
                3,
                60,
                TimeSpan.FromMinutes(45),
                new RunLimits(3, 60, 60, TimeSpan.FromSeconds(2.5), TimeSpan.FromSeconds(6))
            );
            var handler = new ListSourcesHandler(
                new SourceSettingsCatalog([settings]),
                ComplianceConsent.None,
                new EmptyQuotas(),
                new EmptyCircuits(),
                TimeProvider.System
            );
            return new SourcesTools(handler, new FakeCaller());
        }

        [Fact]
        public async Task ReturnsConfiguredSourceWithZeroUsage()
        {
            var response = await Tools().ListSourcesAsync(Ct);

            var naukri = Assert.Single(response.Items);
            Assert.Equal("naukri", naukri.Key);
            Assert.True(naukri.Enabled);
        }

        [Fact]
        public void RejectsNulls()
        {
            var caller = new FakeCaller();
            var handler = new ListSourcesHandler(
                new SourceSettingsCatalog([]),
                ComplianceConsent.None,
                new EmptyQuotas(),
                new EmptyCircuits(),
                TimeProvider.System
            );
            Assert.Throws<ArgumentNullException>(() => new SourcesTools(null!, caller));
            Assert.Throws<ArgumentNullException>(() => new SourcesTools(handler, null!));
        }

        private sealed class FakeCaller : ICallerContext
        {
            public string ActorId => "tests";

            public IReadOnlyCollection<string> Roles => [];

            public bool MustChangePassword => false;

            public IReadOnlyCollection<string> Permissions { get; init; } =
            [
                "profiles.read",
                "profiles.write",
                "runs.read",
                "runs.write",
                "sessions.read",
                "sources.read",
            ];
        }

        private sealed class EmptyQuotas : ISourceQuotaRepository
        {
            public Task<SourceQuota?> FindAsync(
                SourceKey source,
                DateOnly quotaDate,
                CancellationToken cancellationToken
            )
            {
                return Task.FromResult<SourceQuota?>(null);
            }

            public Task AddAsync(SourceQuota quota, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class EmptyCircuits : ISourceCircuitBreakerRepository
        {
            public Task<SourceCircuitBreaker?> FindAsync(
                SourceKey source,
                CancellationToken cancellationToken
            )
            {
                return Task.FromResult<SourceCircuitBreaker?>(null);
            }

            public Task AddAsync(SourceCircuitBreaker breaker, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }
    }
}
