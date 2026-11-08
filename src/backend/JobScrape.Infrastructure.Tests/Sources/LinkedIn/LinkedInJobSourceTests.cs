using JobScrape.Application.Features.Scraping;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using JobScrape.Infrastructure.Browser;
using JobScrape.Infrastructure.Sources.LinkedIn;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.Playwright;
using Xunit;

namespace JobScrape.Infrastructure.Tests.Sources.LinkedIn
{
    /// <summary>
    /// The LinkedIn adapter owns its search page end to end: goto, wait,
    /// classify, parse, and the next-cursor rule. Runs on headless Chromium;
    /// every www.linkedin.com request is served from the synthetic fixture.
    /// </summary>
    [Trait("Category", "Browser")]
    public sealed class LinkedInJobSourceTests : IAsyncLifetime
    {
        private static readonly DateTimeOffset Now = LinkedInTestPages.Now;

        private readonly string _root = Path.Combine(
            Path.GetTempPath(),
            $"jobscrape-linkedin-{Guid.NewGuid():N}"
        );

        private readonly BrowserProfile _profile = BrowserProfile.Create(
            "linkedin-test",
            SourceKey.LinkedIn,
            BrowserEngine.Chromium,
            BrowserChannel.Bundled
        );

        private BrowserSessionPool _pool = null!;

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        public ValueTask InitializeAsync()
        {
            _pool = new BrowserSessionPool(Settings());
            return ValueTask.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            await _pool.DisposeAsync();
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        private BrowserSessionSettings Settings()
        {
            return new BrowserSessionSettings(
                Path.Combine(_root, "profiles"),
                Path.Combine(_root, "artifacts"),
                Headless: true,
                TimeSpan.FromSeconds(10),
                AdblockMode: null,
                ExtensionDir: null
            );
        }

        private SourceRunContext Context()
        {
            return new SourceRunContext(
                ScrapeRunId.New(),
                SourceKey.LinkedIn,
                _profile.Id,
                new SearchCriteria("dotnet", ["Pune"], null, null, remoteOnly: false, [])
            );
        }

        [Fact]
        public async Task LinkedInSearchOwnsItsNextCursorWithoutGenericDriver()
        {
            var page = await _pool.OpenAsync(_profile, Ct);
            var html = LinkedInTestPages.Fixture("search-guest.html");
            await page.Context.RouteAsync(
                "https://www.linkedin.com/**",
                async route =>
                {
                    await route.FulfillAsync(
                        new RouteFulfillOptions
                        {
                            Status = 200,
                            ContentType = "text/html; charset=utf-8",
                            Body = html,
                        }
                    );
                }
            );
            var source = new LinkedInJobSource(
                _pool,
                new FakeTimeProvider(Now),
                NullLogger<LinkedInJobSource>.Instance
            );

            var result = await source.SearchPageAsync(
                Context(),
                new SearchPageRequest(1, null),
                Ct
            );

            Assert.Equal(ChallengeKind.None, result.Challenge.Kind);
            Assert.NotEmpty(result.Jobs);
            // Page 1 starts at offset 0; the next page is offset 25.
            Assert.Equal("25", result.NextCursor);
        }
    }
}
