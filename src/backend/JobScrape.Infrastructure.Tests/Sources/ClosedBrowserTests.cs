using JobScrape.Application.Features.Scraping;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using JobScrape.Infrastructure.Browser;
using JobScrape.Infrastructure.Sources.Indeed;
using JobScrape.Infrastructure.Sources.LinkedIn;
using JobScrape.Infrastructure.Sources.Naukri;
using JobScrape.Infrastructure.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace JobScrape.Infrastructure.Tests.Sources
{
    /// <summary>
    /// A closed browser must end the fetch gracefully, never crash the run.
    /// The pool is empty here on purpose: no context was ever opened.
    /// </summary>
    public sealed class ClosedBrowserTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 4, 18, 0, 0, TimeSpan.Zero);

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private static SourceRunContext Context(SourceKey source)
        {
            return new SourceRunContext(
                ScrapeRunId.New(),
                source,
                BrowserProfileId.New(),
                new SearchCriteria("c#", ["Pune"], null, null, remoteOnly: false, [])
            );
        }

        private static BrowserSessionPool Pool()
        {
            return new BrowserSessionPool(
                new BrowserSessionSettings(
                    Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
                    Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
                    Headless: true,
                    TimeSpan.FromSeconds(5),
                    AdblockMode: null,
                    ExtensionDir: null
                ),
                NullLogger<BrowserSessionPool>.Instance
            );
        }

        [Fact]
        public async Task NaukriSearchWithoutAnOpenBrowserIsTransient()
        {
            var source = new NaukriJobSource(
                Pool(),
                new FakeTimeProvider(Now),
                NullLogger<NaukriJobSource>.Instance,
                new TimeProviderDelay(new FakeTimeProvider(Now)),
                new SystemRandomSource()
            );

            var result = await source.SearchPageAsync(
                Context(SourceKey.Naukri),
                new SearchPageRequest(31, "next"),
                Ct
            );

            Assert.Empty(result.Jobs);
            Assert.Equal(ChallengeKind.TransientError, result.Challenge.Kind);
        }

        [Fact]
        public async Task NaukriDetailWithoutAnOpenBrowserIsTransient()
        {
            var source = new NaukriJobSource(
                Pool(),
                new FakeTimeProvider(Now),
                NullLogger<NaukriJobSource>.Instance,
                new TimeProviderDelay(new FakeTimeProvider(Now)),
                new SystemRandomSource()
            );

            var result = await source.GetDetailAsync(
                Context(SourceKey.Naukri),
                new JobReference(
                    ExternalJobId.Create("101"),
                    CanonicalUrl.Create(
                        "https://www.naukri.com/job-listings-x-101",
                        SourceKey.Naukri
                    )
                ),
                Ct
            );

            Assert.Null(result.Detail);
            Assert.Equal(ChallengeKind.TransientError, result.Challenge.Kind);
        }

        [Fact]
        public async Task IndeedSearchWithoutAnOpenBrowserIsTransient()
        {
            var source = new IndeedJobSource(
                Pool(),
                new FakeTimeProvider(Now),
                NullLogger<IndeedJobSource>.Instance
            );

            var result = await source.SearchPageAsync(
                Context(SourceKey.Indeed),
                new SearchPageRequest(2, "10"),
                Ct
            );

            Assert.Empty(result.Jobs);
            Assert.Equal(ChallengeKind.TransientError, result.Challenge.Kind);
        }

        [Fact]
        public async Task IndeedDetailWithoutAnOpenBrowserIsTransient()
        {
            var source = new IndeedJobSource(
                Pool(),
                new FakeTimeProvider(Now),
                NullLogger<IndeedJobSource>.Instance
            );

            var result = await source.GetDetailAsync(
                Context(SourceKey.Indeed),
                new JobReference(
                    ExternalJobId.Create("101"),
                    CanonicalUrl.Create(
                        "https://in.indeed.com/job-listings-x-101",
                        SourceKey.Indeed
                    )
                ),
                Ct
            );

            Assert.Null(result.Detail);
            Assert.Equal(ChallengeKind.TransientError, result.Challenge.Kind);
        }

        [Fact]
        public async Task LinkedInSearchWithoutAnOpenBrowserIsTransient()
        {
            var source = new LinkedInJobSource(
                Pool(),
                new FakeTimeProvider(Now),
                NullLogger<LinkedInJobSource>.Instance
            );

            var result = await source.SearchPageAsync(
                Context(SourceKey.LinkedIn),
                new SearchPageRequest(2, "10"),
                Ct
            );

            Assert.Empty(result.Jobs);
            Assert.Equal(ChallengeKind.TransientError, result.Challenge.Kind);
        }
    }
}
