using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.Scraping;
using JobScrape.Domain.Abstractions;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using JobScrape.Infrastructure.Composition;
using JobScrape.Infrastructure.Configuration;
using JobScrape.Infrastructure.Sources.Indeed;
using JobScrape.Infrastructure.Sources.LinkedIn;
using JobScrape.Infrastructure.Sources.Naukri;
using JobScrape.Infrastructure.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace JobScrape.Infrastructure.Tests.Sources.Naukri
{
    public sealed class NaukriApiJobSourceTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 18, 8, 0, 0, TimeSpan.Zero);

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private static SearchCriteria Criteria() =>
            new(
                "asp.net core",
                ["Pune"],
                experienceYears: 5,
                postedWithinDays: 7,
                remoteOnly: false,
                requiredSkills: []
            );

        private static SourceRunContext Context(int startPage = 1) =>
            new(
                ScrapeRunId.New(),
                SourceKey.Naukri,
                BrowserProfileId.New(),
                Criteria(),
                startPage
            );

        private static string Fixture(string name) =>
            File.ReadAllText(
                Path.Combine(AppContext.BaseDirectory, "Sources", "Naukri", "Fixtures", name)
            );

        // ---- transport selection ----

        [Fact]
        public async Task FlagOnSelectsApiSourceForNaukri()
        {
            await using var provider = RegistryProvider(Sources(naukriApiOnly: true));
            var registry = provider.GetRequiredService<JobSourceRegistry>();

            var source = registry.Find(SourceKey.Naukri);

            var api = Assert.IsType<NaukriApiJobSource>(source);
            Assert.Equal(SourceKey.Naukri, api.Key);
            Assert.True(api.Capabilities.SupportsDetails);
        }

        [Fact]
        public async Task ApiClientIsASingletonSoOneTransportServesEveryRun()
        {
            await using var provider = RegistryProvider(Sources(naukriApiOnly: true));

            Assert.Same(
                provider.GetRequiredService<NaukriApiClient>(),
                provider.GetRequiredService<NaukriApiClient>()
            );
            Assert.IsAssignableFrom<IDisposable>(
                provider.GetRequiredService<NaukriApiClient>()
            );
        }

        [Fact]
        public async Task FlagOffKeepsBrowserSourceUnchanged()
        {
            // Indeed and LinkedIn share the options class and default to
            // ApiOnly=true when their sections omit it; only the Naukri key
            // may select the API path.
            await using var provider = RegistryProvider(
                Sources(naukriApiOnly: false, indeedApiOnly: true)
            );
            var registry = provider.GetRequiredService<JobSourceRegistry>();

            Assert.IsType<NaukriJobSource>(registry.Find(SourceKey.Naukri));
            Assert.IsType<IndeedJobSource>(registry.Find(SourceKey.Indeed));
            Assert.IsType<LinkedInJobSource>(registry.Find(SourceKey.LinkedIn));
        }

        [Fact]
        public async Task ApiOnlySkipsTheBrowserSessionForNaukriOnly()
        {
            var skipped = new RecordingSessions();
            var apiOnly = new ApiOnlySessionManager(
                skipped,
                Options.Create(Sources(naukriApiOnly: true, indeedApiOnly: true))
            );

            var naukri = await apiOnly.EnsureSessionAsync(
                SourceKey.Naukri,
                BrowserProfileId.New(),
                Ct
            );

            Assert.Equal(ChallengeKind.None, naukri.Kind);
            Assert.Equal(0, skipped.Calls);

            // Indeed keeps its own ApiOnly=true here but must still delegate:
            // only the Naukri key may select the API-only shortcut.
            var indeed = await apiOnly.EnsureSessionAsync(
                SourceKey.Indeed,
                BrowserProfileId.New(),
                Ct
            );
            Assert.Equal(ChallengeKind.None, indeed.Kind);
            Assert.Equal(1, skipped.Calls);
            Assert.Equal(SourceKey.Indeed, skipped.LastSource);

            var browser = new RecordingSessions();
            var browserOnly = new ApiOnlySessionManager(
                browser,
                Options.Create(Sources(naukriApiOnly: false))
            );
            await browserOnly.EnsureSessionAsync(SourceKey.Naukri, BrowserProfileId.New(), Ct);
            Assert.Equal(1, browser.Calls);
            Assert.Equal(SourceKey.Naukri, browser.LastSource);
        }

        // ---- listing walls and partials ----

        [Fact]
        public async Task Single406EndsRunWithPartialsKept()
        {
            var handler = new FakeHandler(request =>
                PageNumber(request) == 1
                    ? JsonResponse(
                        SearchJson(40, Enumerable.Range(1, 20).Select(Job).ToArray())
                    )
                    : StatusResponse(HttpStatusCode.NotAcceptable)
            );

            var outcome = await RunAsync(handler, maxJobs: 50);

            // The 406 is a rate limit: the run stops fetching, keeps the
            // first page, and records a retry window instead of failing.
            Assert.Equal(RunStage.RateLimited, outcome.Summary.Stage);
            Assert.Equal(Now.AddMinutes(30), outcome.Summary.RetryAt);
            Assert.Equal(20, outcome.Summary.Stats.PostingsFound);
            Assert.Equal(20, outcome.Postings.Count);
            Assert.Empty(outcome.Artifacts.Captures);
        }

        [Fact]
        public async Task DroppedRowDoesNotEndPaginationEarly()
        {
            var handler = new FakeHandler(request =>
            {
                var pageNumber = PageNumber(request);
                var jobs = Enumerable
                    .Range(((pageNumber - 1) * 20) + 1, 20)
                    .Select(Job)
                    .ToList();
                if (pageNumber == 1)
                {
                    // One row loses its title; the client drops it, but the
                    // page still counts as full for pagination.
                    jobs[0] = new { jobId = "011126000000" };
                }

                return JsonResponse(SearchJson(40, jobs.ToArray()));
            });

            var outcome = await RunAsync(handler, maxJobs: 50);

            Assert.Equal(2, outcome.Summary.Stats.PagesFetched);
            Assert.Equal(39, outcome.Postings.Count);
        }

        [Fact]
        public async Task Http400PastTheLastPageEndsTheListingCleanly()
        {
            var handler = new FakeHandler(request =>
                PageNumber(request) == 1
                    ? JsonResponse(
                        SearchJson(40, Enumerable.Range(1, 20).Select(Job).ToArray())
                    )
                    : StatusResponse(HttpStatusCode.BadRequest)
            );

            var outcome = await RunAsync(handler, maxJobs: 50);

            // 400 past the last page is the documented clean end, not a wall.
            Assert.Equal(RunStage.Completed, outcome.Summary.Stage);
            Assert.Equal(2, outcome.Summary.Stats.PagesFetched);
            Assert.Equal(20, outcome.Postings.Count);
            Assert.Null(outcome.Summary.PauseReason);
        }

        [Fact]
        public async Task OtherNon200IsRetriedThenEndsWithPartials()
        {
            var handler = new FakeHandler(request =>
                PageNumber(request) == 1
                    ? JsonResponse(
                        SearchJson(40, Enumerable.Range(1, 20).Select(Job).ToArray())
                    )
                    : StatusResponse(HttpStatusCode.InternalServerError)
            );

            var outcome = await RunAsync(handler, maxJobs: 50);

            // Transient failures are retried once by the handler, then stop
            // pagination and let the run complete with what it already kept.
            Assert.Equal(RunStage.Completed, outcome.Summary.Stage);
            Assert.Null(outcome.Summary.PauseReason);
            Assert.Equal(20, outcome.Postings.Count);
            Assert.Equal(3, handler.Requests.Count);
            Assert.Equal(1, outcome.Summary.Stats.PagesFetched);
        }

        [Fact]
        public async Task LoginRedirectWallEndsTheRunWithoutIntervention()
        {
            var handler = new FakeHandler(request =>
                PageNumber(request) == 1
                    ? JsonResponse(
                        SearchJson(40, Enumerable.Range(1, 20).Select(Job).ToArray())
                    )
                    : RedirectResponse("https://www.naukri.com/nlogin/login")
            );

            var outcome = await RunAsync(handler, maxJobs: 50);

            // No login, cookies, or CAPTCHA solving is allowed on the API
            // path, so a redirect must fail the run with its partials rather
            // than park it in a manual-intervention wait.
            Assert.Equal(RunStage.Failed, outcome.Summary.Stage);
            Assert.Null(outcome.Summary.PauseReason);
            Assert.Null(outcome.Summary.RetryAt);
            Assert.Contains("Page structure changed", outcome.Summary.FailureReason);
            Assert.Equal(20, outcome.Summary.Stats.PostingsFound);
            Assert.Equal(20, outcome.Postings.Count);
            var capture = Assert.Single(outcome.Artifacts.Captures);
            Assert.Equal(ChallengeKind.SourceChanged, capture.Kind);
            Assert.False(capture.RequiresManualIntervention);
        }

        [Fact]
        public async Task SearchRedirectIsReportedAsAFinishedWallNotAManualPause()
        {
            var handler = new FakeHandler(_ =>
                RedirectResponse("https://www.naukri.com/nlogin/login")
            );
            using var source = Source(handler);

            var page = await source.SearchPageAsync(Context(), new SearchPageRequest(1, null), Ct);

            Assert.Equal(ChallengeKind.SourceChanged, page.Challenge.Kind);
            Assert.Contains("SoftChallenge", page.Challenge.Evidence, StringComparison.Ordinal);
            Assert.Contains("302", page.Challenge.Evidence, StringComparison.Ordinal);
            Assert.False(page.Challenge.RequiresManualIntervention);
            Assert.Equal(
                ChallengeAction.StopAndInspect,
                ChallengePolicy.Decide(page.Challenge, 0, TimeSpan.FromMinutes(45)).Action
            );
            Assert.Empty(page.Jobs);
        }

        [Fact]
        public async Task StopsRunAndNamesKeyRotationAfterThreeConsecutive406s()
        {
            var handler = new FakeHandler(_ => StatusResponse(HttpStatusCode.NotAcceptable));
            using var source = Source(handler);

            var first = await source.SearchPageAsync(Context(), new SearchPageRequest(1, null), Ct);
            var second = await source.SearchPageAsync(Context(), new SearchPageRequest(1, null), Ct);
            var third = await source.SearchPageAsync(Context(), new SearchPageRequest(1, null), Ct);

            Assert.Equal(ChallengeKind.RateLimited, first.Challenge.Kind);
            Assert.Equal(ChallengeKind.RateLimited, second.Challenge.Kind);
            Assert.Equal(ChallengeKind.RateLimited, third.Challenge.Kind);
            Assert.DoesNotContain("key rotation", first.Challenge.Evidence, StringComparison.Ordinal);
            Assert.DoesNotContain(
                "key rotation",
                second.Challenge.Evidence,
                StringComparison.Ordinal
            );
            Assert.Contains("key rotation", third.Challenge.Evidence, StringComparison.Ordinal);
            Assert.Equal(3, handler.Requests.Count);
        }

        [Fact]
        public async Task SuccessfulPageResetsTheConsecutive406Streak()
        {
            var status = HttpStatusCode.NotAcceptable;
            var handler = new FakeHandler(_ =>
                status == HttpStatusCode.OK
                    ? JsonResponse(SearchJson(20, Enumerable.Range(1, 20).Select(Job).ToArray()))
                    : StatusResponse(status)
            );
            using var source = Source(handler);

            await source.SearchPageAsync(Context(), new SearchPageRequest(1, null), Ct);
            await source.SearchPageAsync(Context(), new SearchPageRequest(1, null), Ct);
            status = HttpStatusCode.OK;
            await source.SearchPageAsync(Context(), new SearchPageRequest(1, null), Ct);
            status = HttpStatusCode.NotAcceptable;
            var afterSuccess = await source.SearchPageAsync(
                Context(),
                new SearchPageRequest(1, null),
                Ct
            );

            Assert.Equal(ChallengeKind.RateLimited, afterSuccess.Challenge.Kind);
            Assert.DoesNotContain(
                "key rotation",
                afterSuccess.Challenge.Evidence,
                StringComparison.Ordinal
            );
        }

        [Fact]
        public async Task ExternalPortalRowsAreSkippedInsteadOfCollected()
        {
            var handler = new FakeHandler(_ => JsonResponse(ExternalRowsJson()));
            using var source = Source(handler);

            var page = await source.SearchPageAsync(Context(), new SearchPageRequest(1, null), Ct);

            Assert.Equal(ChallengeKind.None, page.Challenge.Kind);
            var job = Assert.Single(page.Jobs);
            Assert.Equal("011126000003", job.ExternalJobId);
            Assert.Null(page.NextCursor);
        }

        [Fact]
        public async Task StartPageBeyondTheListingFailsFast()
        {
            var handler = new FakeHandler(_ =>
                JsonResponse(SearchJson(25, Enumerable.Range(1, 20).Select(Job).ToArray()))
            );
            using var source = Source(handler);

            var error = await Assert.ThrowsAsync<StartPageOutOfRangeException>(() =>
                source.SearchPageAsync(Context(startPage: 5), new SearchPageRequest(1, null), Ct)
            );

            Assert.Equal(5, error.StartPage);
            Assert.Equal(2, error.MaxPage);
        }

        // ---- detail enrichment ----

        [Fact]
        public async Task DetailEnrichesThroughJobApiV4()
        {
            var handler = new FakeHandler(_ => JsonResponse(Fixture("detail-api.json")));
            using var source = Source(
                new FakeHandler(_ => throw new InvalidOperationException()),
                handler
            );
            var job = new JobReference(
                ExternalJobId.Create("011126000001"),
                CanonicalUrl.Create(
                    "https://www.naukri.com/job-listings-senior-dotnet-developer-contoso-011126000001",
                    SourceKey.Naukri
                )
            );

            var result = await source.GetDetailAsync(Context(), job, Ct);

            Assert.Equal(ChallengeKind.None, result.Challenge.Kind);
            var detail = Assert.IsType<JobSnapshot>(result.Detail);
            Assert.Equal(SourceKey.Naukri, detail.Source);
            Assert.Equal("011126000001", detail.ExternalJobId);
            Assert.Equal(
                "https://www.naukri.com/job-listings-senior-dotnet-developer-contoso-011126000001",
                detail.Url
            );
            Assert.Equal("Senior .NET Developer", detail.Title);
            Assert.Equal("Contoso", detail.Company);
            Assert.Equal("https://www.naukri.com/contoso-jobs-careers-1", detail.CompanyUrl);
            Assert.Equal("5-8 Yrs", detail.ExperienceRaw);
            Assert.Equal(5, detail.ExperienceMinYears);
            Assert.Equal(8, detail.ExperienceMaxYears);
            Assert.Equal("1500000-2750000 PA", detail.SalaryRaw);
            Assert.Equal(
                new SalaryRange(1_500_000m, 2_750_000m, "INR", SalaryPeriod.Year),
                detail.Salary
            );
            Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1_793_527_200_000), detail.PostedAt);
            Assert.Equal("Build & run .NET services.\n- C#\n- Azure", detail.Description);
            Assert.Equal(["C#", "ASP.NET Core", "Azure"], detail.Skills);
            Assert.Equal("https://careers.contoso.example/apply/1", detail.ApplyUrl);
            Assert.False(detail.LowConfidence);
            var location = Assert.IsType<JobLocation>(detail.Location);
            Assert.Equal("Hybrid - Pune", location.Raw);
            Assert.Equal("Pune", location.City);
            Assert.Equal(WorkMode.Hybrid, location.WorkMode);

            var request = Assert.Single(handler.Requests);
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("/jobapi/v4/job/011126000001", request.Uri?.AbsolutePath);
            Assert.Equal("www.naukri.com", request.Uri?.IdnHost);
            Assert.Equal("121", Header(request, "appid"));
            Assert.Equal("Naukri", Header(request, "systemid"));
            Assert.Equal("application/json", Header(request, "Accept"));
            Assert.Contains("Chrome/", Header(request, "User-Agent"), StringComparison.Ordinal);

            // Detail tokens are minted per request under the job id tag.
            await source.GetDetailAsync(Context(), job, Ct);
            Assert.Equal(2, handler.Requests.Count);
            Assert.NotEmpty(Header(handler.Requests[0], "nkparam"));
            Assert.NotEqual(
                Header(handler.Requests[0], "nkparam"),
                Header(handler.Requests[1], "nkparam")
            );
        }

        [Fact]
        public async Task DetailRedirectIsAWallThatEndsTheRun()
        {
            var handler = new FakeHandler(_ =>
                RedirectResponse("https://www.naukri.com/nlogin/login")
            );
            using var source = Source(
                new FakeHandler(_ => throw new InvalidOperationException()),
                handler
            );
            var job = new JobReference(
                ExternalJobId.Create("011126000001"),
                CanonicalUrl.Create(
                    "https://www.naukri.com/job-listings-x-011126000001",
                    SourceKey.Naukri
                )
            );

            var result = await source.GetDetailAsync(Context(), job, Ct);

            Assert.Null(result.Detail);
            Assert.Equal(ChallengeKind.SourceChanged, result.Challenge.Kind);
            Assert.False(result.Challenge.RequiresManualIntervention);
            Assert.Equal(
                ChallengeAction.StopAndInspect,
                ChallengePolicy.Decide(result.Challenge, 0, TimeSpan.FromMinutes(45)).Action
            );
        }

        // ---- pacing ----

        [Fact]
        public async Task InterPagePauseUsesConfiguredDelays()
        {
            var handler = new FakeHandler(request =>
                JsonResponse(
                    SearchJson(
                        40,
                        Enumerable.Range(((PageNumber(request) - 1) * 20) + 1, 20)
                            .Select(Job)
                            .ToArray()
                    )
                )
            );
            var delay = new RecordingDelay();

            var outcome = await RunAsync(handler, delay: delay, maxJobs: 50);

            Assert.Equal(RunStage.Completed, outcome.Summary.Stage);
            Assert.Equal(40, outcome.Summary.Stats.PostingsFound);
            var wait = Assert.Single(delay.Waits);
            Assert.InRange(wait, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(6));
        }

        // ---- run harness ----

        private static async Task<RunOutcome> RunAsync(
            FakeHandler handler,
            RecordingDelay? delay = null,
            int maxPages = 3,
            int maxJobs = 20
        )
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            var clock = new FakeTimeProvider(Now);
            var profile = SearchProfile.Create(
                "backend",
                SourceKey.Naukri,
                Criteria(),
                new RunLimits(
                    maxPages,
                    maxJobs,
                    0,
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(6)
                ),
                Now
            );
            var browser = BrowserProfile.Create(
                "main",
                SourceKey.Naukri,
                BrowserEngine.Chromium,
                BrowserChannel.Bundled
            );
            browser.MarkVerified(Now);
            await using (var seed = database.Scope())
            {
                seed.Db.SearchProfiles.Add(profile);
                seed.Db.BrowserProfiles.Add(browser);
                await seed.Db.SaveChangesAsync(Ct);
            }

            delay ??= new RecordingDelay();
            var artifacts = new RecordingArtifacts();
            using var client = new NaukriApiClient(
                new HttpClient(handler, disposeHandler: false),
                NullLogger<NaukriApiClient>.Instance
            );
            using var source = new NaukriApiJobSource(
                client,
                new HttpClient(
                    new FakeHandler(_ => throw new InvalidOperationException("No detail visit.")),
                    disposeHandler: false
                ),
                NullLogger<NaukriApiJobSource>.Instance
            );
            await using var scope = database.Scope();
            var runHandler = new RunScrapeHandler(
                scope.Get<ScrapeStores>(),
                new JobSourceRegistry([source]),
                new SourceSettingsCatalog([
                    new SourceSettings(
                        SourceKey.Naukri,
                        true,
                        25,
                        60,
                        2,
                        profile.Limits.MaxPages,
                        profile.Limits.MaxJobs,
                        TimeSpan.FromMinutes(45),
                        profile.Limits
                    ),
                ]),
                new ReadySessions(),
                artifacts,
                delay,
                new RetryPolicy(2, new MinRandom()),
                new MinRandom(),
                clock,
                NullLogger<RunScrapeHandler>.Instance
            );
            var result = await runHandler.HandleAsync(
                new RunScrapeRequest(profile.Id, browser.Id, ComplianceConsent.None, Enrich: false),
                Ct
            );
            Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
            await using var verify = database.Scope();
            var postings = await verify.Db.JobPostings.ToListAsync(Ct);
            return new RunOutcome(result.Value, postings, artifacts);
        }

        private static NaukriApiJobSource Source(FakeHandler search, FakeHandler? detail = null)
        {
            return new NaukriApiJobSource(
                new NaukriApiClient(
                    new HttpClient(search, disposeHandler: false),
                    NullLogger<NaukriApiClient>.Instance
                ),
                new HttpClient(
                    detail
                        ?? new FakeHandler(_ =>
                            throw new InvalidOperationException("No detail visit.")
                        ),
                    disposeHandler: false
                ),
                NullLogger<NaukriApiJobSource>.Instance
            );
        }

        private static ServiceProvider RegistryProvider(SourcesOptions sources)
        {
            var services = new ServiceCollection()
                .AddLogging()
                .AddSingleton(Options.Create(new StorageOptions()))
                .AddSingleton(Options.Create(new BrowserOptions()))
                .AddSingleton(Options.Create(sources))
                .AddSingleton(TimeProvider.System)
                .AddSingleton<IDelay>(new RecordingDelay())
                .AddSingleton<IRandomSource>(new MinRandom());
            services.AddJobScrapeBrowser(Path.GetTempPath());
            return services.BuildServiceProvider(validateScopes: true);
        }

        private static SourcesOptions Sources(bool naukriApiOnly, bool indeedApiOnly = false)
        {
            return new SourcesOptions
            {
                Sources = new Dictionary<string, SourceOptions>(StringComparer.OrdinalIgnoreCase)
                {
                    ["naukri"] = new()
                    {
                        Enabled = true,
                        BaseUrl = "https://www.naukri.com",
                        ApiOnly = naukriApiOnly,
                    },
                    ["indeed"] = new()
                    {
                        Enabled = true,
                        BaseUrl = "https://in.indeed.com",
                        ApiOnly = indeedApiOnly,
                    },
                    ["linkedin"] = new()
                    {
                        Enabled = true,
                        BaseUrl = "https://www.linkedin.com",
                        ApiOnly = indeedApiOnly,
                    },
                },
            };
        }

        private static HttpResponseMessage JsonResponse(string json)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
        }

        private static HttpResponseMessage StatusResponse(HttpStatusCode status)
        {
            return new HttpResponseMessage(status);
        }

        private static HttpResponseMessage RedirectResponse(string location)
        {
            var response = new HttpResponseMessage(HttpStatusCode.Redirect);
            response.Headers.Location = new Uri(location);
            return response;
        }

        private static string SearchJson(int total, params object[] rows)
        {
            // The live body names the row array jobDetails; every handler
            // test drives that wire format end to end.
            return JsonSerializer.Serialize(new { noOfJobs = total, jobDetails = rows });
        }

        private static string ExternalRowsJson()
        {
            return JsonSerializer.Serialize(
                new
                {
                    noOfJobs = 3,
                    jobs = new[]
                    {
                        new
                        {
                            jobId = "011126000001",
                            title = "Developer 1",
                            companyName = "Contoso",
                            jdURL = "/job-listings-developer-contoso-011126000001",
                            companyApplyJob = true,
                        },
                        new
                        {
                            jobId = "011126000002",
                            title = "Developer 2",
                            companyName = "Contoso",
                            jdURL = "/job-listings-developer-contoso-011126000002",
                            clientCareersUrl = "https://careers.contoso.example/apply/2",
                        },
                        Job(3),
                    },
                }
            );
        }

        private static object Job(int number)
        {
            var id = $"01112600{number:D4}";
            return new
            {
                jobId = id,
                title = $"Developer {number}",
                companyName = "Contoso",
                experienceText = "2-4 Yrs",
                tagsAndSkills = "C#",
                jdURL = $"/job-listings-developer-contoso-{id}",
                staticUrl = id,
                salaryDetail = new
                {
                    hideSalary = true,
                    minimumSalary = 0,
                    maximumSalary = 0,
                    currency = "INR",
                },
                placeholders = new[] { new { type = "location", label = "Pune" } },
                companyApplyJob = false,
                clientCareersUrl = string.Empty,
                applyRedirectUrl = string.Empty,
                createdDate = 1_794_994_200_000,
                minimumExperience = 2,
                maximumExperience = 4,
            };
        }

        private static int PageNumber(HttpRequestMessage request)
        {
            var query = request
                .RequestUri!.Query.TrimStart('?')
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Split('=', 2))
                .ToDictionary(
                    pair => Uri.UnescapeDataString(pair[0]),
                    pair => pair.Length > 1 ? Uri.UnescapeDataString(pair[1]) : string.Empty,
                    StringComparer.Ordinal
                );
            return int.Parse(query["pageNo"], CultureInfo.InvariantCulture);
        }

        private static string Header(CapturedRequest request, string name)
        {
            Assert.True(
                request.Headers.TryGetValue(name, out var values),
                $"Missing header {name}."
            );
            Assert.NotEmpty(values);
            return string.Join(' ', values);
        }

        private sealed record RunOutcome(
            ScrapeRunSummary Summary,
            IReadOnlyList<JobPosting> Postings,
            RecordingArtifacts Artifacts
        );

        private sealed record CapturedRequest(
            HttpMethod Method,
            Uri? Uri,
            IReadOnlyDictionary<string, string[]> Headers
        );

        private sealed class FakeHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

            public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
            {
                _respond = respond;
            }

            public List<CapturedRequest> Requests { get; } = [];

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken
            )
            {
                Requests.Add(
                    new CapturedRequest(
                        request.Method,
                        request.RequestUri,
                        request.Headers.ToDictionary(
                            header => header.Key,
                            header => header.Value.ToArray(),
                            StringComparer.OrdinalIgnoreCase
                        )
                    )
                );
                return Task.FromResult(_respond(request));
            }
        }

        private sealed class RecordingDelay : IDelay
        {
            public List<TimeSpan> Waits { get; } = [];

            public Task WaitAsync(TimeSpan duration, CancellationToken cancellationToken)
            {
                Waits.Add(duration);
                return Task.CompletedTask;
            }
        }

        private sealed class RecordingArtifacts : IArtifactStore
        {
            public List<ChallengeAssessment> Captures { get; } = [];

            public Task<string> CaptureAsync(
                SourceRunContext context,
                ChallengeAssessment assessment,
                CancellationToken cancellationToken
            )
            {
                Captures.Add(assessment);
                return Task.FromResult("artifacts/test");
            }
        }

        private sealed class ReadySessions : ISessionManager
        {
            public Task<ChallengeAssessment> EnsureSessionAsync(
                SourceKey source,
                BrowserProfileId profileId,
                CancellationToken cancellationToken
            )
            {
                return Task.FromResult(ChallengeAssessment.Clear);
            }
        }

        private sealed class RecordingSessions : ISessionManager
        {
            public int Calls { get; private set; }

            public SourceKey? LastSource { get; private set; }

            public Task<ChallengeAssessment> EnsureSessionAsync(
                SourceKey source,
                BrowserProfileId profileId,
                CancellationToken cancellationToken
            )
            {
                Calls++;
                LastSource = source;
                return Task.FromResult(ChallengeAssessment.Clear);
            }
        }

        private sealed class MinRandom : IRandomSource
        {
            public int NextInt32(int minInclusive, int maxExclusive)
            {
                return minInclusive;
            }
        }
    }
}
