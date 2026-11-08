using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.Scraping;
using JobScrape.Domain.Abstractions;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using JobScrape.Infrastructure.Browser;
using JobScrape.Infrastructure.Sources.Indeed;
using JobScrape.Infrastructure.Sources.LinkedIn;
using JobScrape.Infrastructure.Sources.Naukri;
using JobScrape.Infrastructure.Tests.Sources.Naukri;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.Playwright;
using Xunit;

namespace JobScrape.Infrastructure.Tests.Browser
{
    /// <summary>
    /// The browser layer on real headless Chromium (Playwright's bundled
    /// build). Naukri is never contacted: routes serve the synthetic
    /// fixtures for every www.naukri.com request.
    /// </summary>
    [Trait("Category", "Browser")]
    public sealed class BrowserTests : IAsyncLifetime
    {
        private const string Home =
            "<html><head><title>Naukri</title></head><body>Jobs for you</body></html>";

        private static readonly DateTimeOffset Now = NaukriTestPages.Now;

        private readonly string _root = Path.Combine(
            Path.GetTempPath(),
            $"jobscrape-browser-{Guid.NewGuid():N}"
        );

        private readonly BrowserProfile _profile = BrowserProfile.Create(
            "naukri-test",
            SourceKey.Naukri,
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

        /// <summary>
        /// Opens the profile and serves every Naukri URL from <paramref name="serve"/>;
        /// a null answer aborts the request like a dropped connection.
        /// </summary>
        private async Task<IPage> OpenAsync(Func<Uri, (int Status, string Html)?> serve)
        {
            var page = await _pool.OpenAsync(_profile, Ct);
            await page.Context.RouteAsync(
                "https://www.naukri.com/**",
                async route =>
                {
                    var answer = serve(new Uri(route.Request.Url));
                    if (answer is not { } reply)
                    {
                        await route.AbortAsync();
                        return;
                    }

                    await route.FulfillAsync(
                        new RouteFulfillOptions
                        {
                            Status = reply.Status,
                            ContentType = "text/html; charset=utf-8",
                            Body = reply.Html,
                        }
                    );
                }
            );
            return page;
        }

        private static (int, string)? Site(Uri url)
        {
            var path = url.AbsolutePath;
            if (path == "/")
            {
                return (200, Home);
            }

            if (path.StartsWith("/job-listings-", StringComparison.Ordinal))
            {
                return (200, NaukriTestPages.Fixture("detail-normal.html"));
            }

            return path.EndsWith("-2", StringComparison.Ordinal)
                ? (200, NaukriTestPages.Fixture("search-last-page.html"))
                : (200, NaukriTestPages.Fixture("search-normal.html"));
        }

        private PlaywrightSessionManager Sessions()
        {
            return new PlaywrightSessionManager(
                new OneProfile(_profile),
                _pool,
                Registry(),
                new FakeTimeProvider(Now)
            );
        }

        private JobSourceRegistry Registry()
        {
            return new JobSourceRegistry(
                [
                    Source(),
                    new IndeedJobSource(
                        _pool,
                        new FakeTimeProvider(Now),
                        NullLogger<IndeedJobSource>.Instance
                    ),
                    new LinkedInJobSource(
                        _pool,
                        new FakeTimeProvider(Now),
                        NullLogger<LinkedInJobSource>.Instance
                    ),
                ]
            );
        }

        private NaukriJobSource Source()
        {
            return new NaukriJobSource(
                _pool,
                new FakeTimeProvider(Now),
                NullLogger<NaukriJobSource>.Instance,
                new NoDelay(),
                new FixedRandom()
            );
        }

        private SourceRunContext Context()
        {
            return new SourceRunContext(
                ScrapeRunId.New(),
                SourceKey.Naukri,
                _profile.Id,
                new SearchCriteria("dot net developer", ["Pune"], 5, 7, false, [])
            );
        }

        [Fact]
        public async Task PoolKeepsOnePersistentContextPerProfileUntilReleased()
        {
            Assert.Null(_pool.Find(_profile.Id));

            var page = await _pool.OpenAsync(_profile, Ct);
            Assert.Same(page, await _pool.OpenAsync(_profile, Ct));
            Assert.Same(page, _pool.Find(_profile.Id));
            Assert.True(Directory.Exists(Path.Combine(_root, "profiles", "naukri", "naukri-test")));

            await _pool.ReleaseAsync(_profile.Id);
            Assert.Null(_pool.Find(_profile.Id));
            await _pool.ReleaseAsync(_profile.Id);
            await Assert.ThrowsAsync<ArgumentNullException>(() => _pool.OpenAsync(null!, Ct));
        }

        [Fact]
        public async Task WarmUpVerifiesTheSessionOnACleanHomePage()
        {
            await OpenAsync(Site);

            var assessment = await Sessions().EnsureSessionAsync(SourceKey.Naukri, _profile.Id, Ct);

            Assert.Equal(ChallengeKind.None, assessment.Kind);
            Assert.Equal(SessionState.Healthy, _profile.State);
            Assert.Equal(Now, _profile.LastVerifiedAt);
        }

        [Fact]
        public async Task WarmUpReportsAWallAndAnUnreachableHomeWithoutVerifying()
        {
            var blocked = true;
            await OpenAsync(_ =>
                blocked
                    ? (
                        403,
                        "<html><head><title>Access Denied</title></head><body>Access Denied</body></html>"
                    )
                    : null
            );

            var wall = await Sessions().EnsureSessionAsync(SourceKey.Naukri, _profile.Id, Ct);
            Assert.Equal(ChallengeKind.AccessDenied, wall.Kind);

            blocked = false;
            var down = await Sessions().EnsureSessionAsync(SourceKey.Naukri, _profile.Id, Ct);
            Assert.Equal(ChallengeKind.TransientError, down.Kind);
            Assert.Equal(SessionState.New, _profile.State);
        }

        [Fact]
        public async Task NonNaukriSourceVerifiesWithoutNaukriClassifier()
        {
            // Indeed is never verified by the Naukri classifier: the page
            // never navigates, the session reports ready, and the profile
            // is not marked verified.
            await OpenAsync(Site);

            var assessment = await Sessions()
                .EnsureSessionAsync(SourceKey.Indeed, _profile.Id, Ct);

            Assert.Equal(ChallengeKind.None, assessment.Kind);
            Assert.Equal(SessionState.New, _profile.State);
            Assert.Equal("about:blank", _pool.Find(_profile.Id)?.Url);
        }

        [Fact]
        public void NaukriAdapterImplementsTheWarmUpVerifier()
        {
            // Composition-level pin: the session manager dispatches warm-up
            // through ISourceWarmUpVerifier; losing the interface silently
            // skips warm-up instead of failing the run.
            Assert.IsAssignableFrom<ISourceWarmUpVerifier>(Source());
        }

        [Fact]
        public async Task OtherSourcesOpenTheBrowserWithoutAWarmUp()
        {
            var assessment = await Sessions().EnsureSessionAsync(SourceKey.Indeed, _profile.Id, Ct);

            Assert.Equal(ChallengeKind.None, assessment.Kind);
            Assert.NotNull(_pool.Find(_profile.Id));
            Assert.Equal(SessionState.New, _profile.State);
        }

        [Fact]
        public async Task SearchesPageOneThenClicksNextUntilTheLastPage()
        {
            var requested = new List<string>();
            await OpenAsync(url =>
            {
                requested.Add(url.PathAndQuery);
                return Site(url);
            });
            var source = Source();
            var context = Context();

            var first = await source.SearchPageAsync(context, new SearchPageRequest(1, null), Ct);
            Assert.Equal(ChallengeKind.None, first.Challenge.Kind);
            Assert.Equal(20, first.Jobs.Count);
            Assert.Equal(NaukriJobSource.NextCursor, first.NextCursor);
            Assert.Equal("011126000001", first.Fingerprint!.FirstExternalId);
            Assert.StartsWith(
                "/dot-net-developer-jobs-in-pune?k=",
                requested[0],
                StringComparison.Ordinal
            );

            var last = await source.SearchPageAsync(
                context,
                new SearchPageRequest(2, first.NextCursor),
                Ct
            );
            Assert.Equal("011126000301", last.Fingerprint!.FirstExternalId);
            Assert.Null(last.NextCursor);

            // Next is disabled here: the page stays, so the handler's
            // repeated-page guard ends paging.
            var again = await source.SearchPageAsync(context, new SearchPageRequest(3, "next"), Ct);
            Assert.Equal(last.Fingerprint, again.Fingerprint);
            Assert.Equal(source.Key, SourceKey.Naukri);
            Assert.True(source.Capabilities.SupportsDetails);
        }

        [Fact]
        public async Task HiddenCardsAreMarkedInTheBrowserAndSkipped()
        {
            await OpenAsync(url =>
                (
                    200,
                    NaukriTestPages
                        .Fixture("search-normal.html")
                        .Replace(
                            "<div class=\"srp-jobtuple-wrapper\" data-job-id=\"011126000001\"",
                            "<div class=\"srp-jobtuple-wrapper\" style=\"position:absolute;left:-9000px\" data-job-id=\"011126000001\"",
                            StringComparison.Ordinal
                        )
                )
            );

            var page = await Source()
                .SearchPageAsync(Context(), new SearchPageRequest(1, null), Ct);

            Assert.Equal(19, page.Jobs.Count);
            Assert.DoesNotContain(page.Jobs, job => job.ExternalJobId == "011126000001");
        }

        [Fact]
        public async Task AWallIsClassifiedAndADroppedConnectionIsTransient()
        {
            var walled = true;
            await OpenAsync(_ =>
                walled ? (403, NaukriTestPages.Fixture("search-challenge.html")) : null
            );
            var source = Source();

            var wall = await source.SearchPageAsync(Context(), new SearchPageRequest(1, null), Ct);
            Assert.Equal(ChallengeKind.AccessDenied, wall.Challenge.Kind);
            Assert.Empty(wall.Jobs);

            walled = false;
            var down = await source.SearchPageAsync(Context(), new SearchPageRequest(1, null), Ct);
            Assert.Equal(ChallengeKind.TransientError, down.Challenge.Kind);

            var job = new JobReference(
                ExternalJobId.Create("011126000001"),
                CanonicalUrl.Create(
                    "https://www.naukri.com/job-listings-senior-dotnet-developer-011126000001",
                    SourceKey.Naukri
                )
            );
            var detail = await source.GetDetailAsync(Context(), job, Ct);
            Assert.Equal(ChallengeKind.TransientError, detail.Challenge.Kind);
            Assert.Null(detail.Detail);
        }

        [Fact]
        public async Task DetailPagesAreParsedFromTheirJsonLd()
        {
            await OpenAsync(Site);
            var job = new JobReference(
                ExternalJobId.Create("011126000001"),
                CanonicalUrl.Create(
                    "https://www.naukri.com/job-listings-senior-dotnet-developer-contoso-pune-gurugram-5-to-8-years-011126000001",
                    SourceKey.Naukri
                )
            );

            var detail = await Source().GetDetailAsync(Context(), job, Ct);

            Assert.Equal(ChallengeKind.None, detail.Challenge.Kind);
            Assert.Equal("011126000001", detail.Detail!.ExternalJobId);
            Assert.False(string.IsNullOrWhiteSpace(detail.Detail.Description));
        }

        [Fact]
        public async Task NavigationTimeoutsAreTransientAndADetailWallIsClassified()
        {
            // A one-second budget and a site that never answers.
            await _pool.DisposeAsync();
            _pool = new BrowserSessionPool(
                Settings() with
                {
                    NavigationTimeout = TimeSpan.FromSeconds(1),
                }
            );
            var page = await _pool.OpenAsync(_profile, Ct);
            var hang = false;
            await page.Context.RouteAsync(
                "https://www.naukri.com/**",
                async route =>
                {
                    if (hang)
                    {
                        return;
                    }

                    await route.FulfillAsync(
                        new RouteFulfillOptions
                        {
                            Status = 403,
                            ContentType = "text/html",
                            Body =
                                "<html><head><title>Access Denied</title></head><body>Access Denied</body></html>",
                        }
                    );
                }
            );
            var source = Source();
            var job = new JobReference(
                ExternalJobId.Create("011126000001"),
                CanonicalUrl.Create(
                    "https://www.naukri.com/job-listings-senior-dotnet-developer-011126000001",
                    SourceKey.Naukri
                )
            );

            // A wall first: requests left hanging below stay pending.
            var wall = await source.GetDetailAsync(Context(), job, Ct);
            Assert.Equal(ChallengeKind.AccessDenied, wall.Challenge.Kind);
            Assert.Null(wall.Detail);

            hang = true;
            Assert.Equal(
                ChallengeKind.TransientError,
                (await Sessions().EnsureSessionAsync(SourceKey.Naukri, _profile.Id, Ct)).Kind
            );
            Assert.Equal(
                ChallengeKind.TransientError,
                (await source.SearchPageAsync(Context(), new SearchPageRequest(1, null), Ct))
                    .Challenge
                    .Kind
            );
            Assert.Equal(
                ChallengeKind.TransientError,
                (await source.GetDetailAsync(Context(), job, Ct)).Challenge.Kind
            );
        }

        [Fact]
        public async Task AdapterWithoutTheSessionsBrowserReportsTransient()
        {
            var result = await Source()
                .SearchPageAsync(Context(), new SearchPageRequest(1, null), Ct);

            Assert.Equal(ChallengeKind.TransientError, result.Challenge.Kind);
        }

        [Fact]
        public async Task ArtifactsCaptureScreenshotHtmlAndAssessment()
        {
            await OpenAsync(Site);
            var page = _pool.Find(_profile.Id)!;
            await page.GotoAsync("https://www.naukri.com/");
            var store = new FileArtifactStore(
                _pool,
                Settings(),
                new FakeTimeProvider(Now),
                NullLogger<FileArtifactStore>.Instance
            );
            var context = Context();
            var wall = new ChallengeAssessment(ChallengeKind.Captcha, 0.9, "title:'captcha'");

            var relative = await store.CaptureAsync(context, wall, Ct);

            var directory = Path.Combine(_root, "artifacts", relative);
            Assert.StartsWith(
                $"runs/{context.RunId.Value}/20261101T100000-Captcha",
                relative,
                StringComparison.Ordinal
            );
            Assert.True(File.Exists(Path.Combine(directory, "screenshot.png")));
            Assert.Contains(
                "Jobs for you",
                await File.ReadAllTextAsync(Path.Combine(directory, "page.html"), Ct),
                StringComparison.Ordinal
            );
            Assert.Contains(
                "\"kind\":\"Captcha\"",
                await File.ReadAllTextAsync(Path.Combine(directory, "assessment.json"), Ct),
                StringComparison.Ordinal
            );

            // No open browser: the assessment alone is still recorded.
            await _pool.ReleaseAsync(_profile.Id);
            var bare = Path.Combine(
                _root,
                "artifacts",
                await store.CaptureAsync(Context(), wall, Ct)
            );
            Assert.Equal(["assessment.json"], Directory.GetFiles(bare).Select(Path.GetFileName));
        }

        private sealed class OneProfile(BrowserProfile known) : IBrowserProfileRepository
        {
            public Task<BrowserProfile?> GetAsync(
                BrowserProfileId id,
                CancellationToken cancellationToken
            )
            {
                return Task.FromResult<BrowserProfile?>(id == known.Id ? known : null);
            }

            public Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken)
            {
                return Task.FromResult(false);
            }

            public Task AddAsync(BrowserProfile profile, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class NoDelay : IDelay
        {
            public Task WaitAsync(TimeSpan duration, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class FixedRandom : IRandomSource
        {
            public int NextInt32(int minInclusive, int maxExclusive)
            {
                return minInclusive;
            }
        }
    }
}
