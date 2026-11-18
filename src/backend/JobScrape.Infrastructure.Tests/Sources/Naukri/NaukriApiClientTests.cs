using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using JobScrape.Infrastructure.Sources.Naukri;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace JobScrape.Infrastructure.Tests.Sources.Naukri
{
    public sealed class NaukriApiClientTests
    {
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

        private static string Fixture(string name) =>
            File.ReadAllText(
                Path.Combine(AppContext.BaseDirectory, "Sources", "Naukri", "Fixtures", name)
            );

        // ---- fixtures ----

        [Fact]
        public async Task MapsSearchRowsThroughTheSharedGate()
        {
            var handler = new FakeHandler(_ => JsonResponse(Fixture("search-api-normal.json")));
            using var client = Client(handler);

            var page = await client.FetchPageAsync(Criteria(), 1, Ct);

            Assert.Equal(25, page.Total);
            Assert.Equal(0, page.DroppedRows);
            Assert.False(page.EndOfListing);
            Assert.Equal(ChallengeKind.None, page.Challenge.Kind);
            Assert.Equal(2, page.Rows.Count);

            var jobs = page.Rows.Select(row => row.Snapshot).ToArray();
            Assert.All(jobs, job => NaukriTestPages.AssertAccepted(job));

            var job = page.Rows[0].Snapshot;
            Assert.False(page.Rows[0].ExternalPortal);
            Assert.Equal(SourceKey.Naukri, job.Source);
            Assert.Equal("011126000001", job.ExternalJobId);
            Assert.Equal(
                "https://www.naukri.com/job-listings-senior-dotnet-developer-contoso-011126000001",
                job.Url
            );
            Assert.Equal("Senior .NET Developer", job.Title);
            Assert.Equal("Contoso", job.Company);
            Assert.Equal("5-8 Yrs", job.ExperienceRaw);
            Assert.Equal(5, job.ExperienceMinYears);
            Assert.Equal(8, job.ExperienceMaxYears);
            Assert.Equal("1500000-2750000 PA", job.SalaryRaw);
            Assert.Equal(
                new SalaryRange(1_500_000m, 2_750_000m, "INR", SalaryPeriod.Year),
                job.Salary
            );
            Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1_793_527_200_000), job.PostedAt);
            Assert.Equal(["C#", "ASP.NET Core", "Azure", "SQL Server"], job.Skills);
            Assert.Null(job.Snippet);
            Assert.Null(job.ApplyUrl);

            var location = Assert.IsType<JobLocation>(job.Location);
            Assert.Equal("Hybrid - Pune, Gurugram, Bengaluru", location.Raw);
            Assert.Equal("Pune", location.City);
            Assert.Equal(WorkMode.Hybrid, location.WorkMode);

            var remote = page.Rows[1].Snapshot;
            Assert.Null(remote.Salary);
            Assert.Null(remote.SalaryRaw);
            Assert.Equal(3, remote.ExperienceMinYears);
            Assert.Equal(3, remote.ExperienceMaxYears);
            Assert.Equal(WorkMode.Remote, remote.Location?.WorkMode);
            Assert.Null(remote.Location?.City);

            var request = Assert.Single(handler.Requests);
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("/jobapi/v3/search", request.Uri?.AbsolutePath);
            Assert.Equal("www.naukri.com", request.Uri?.IdnHost);

            var query = Query(request.Uri);
            Assert.Equal(9, query.Count);
            Assert.Equal("20", query["noOfResults"]);
            Assert.Equal("1", query["pageNo"]);
            Assert.Equal("search_by_keyword", query["urlType"]);
            Assert.Equal("adv", query["searchType"]);
            Assert.Equal("asp.net core", query["keyword"]);
            Assert.Equal("Pune", query["location"]);
            Assert.Equal("f", query["sort"]);
            Assert.Equal("5", query["experience"]);
            Assert.Equal("7", query["jobAge"]);

            Assert.Equal("109", Header(request, "appid"));
            Assert.Equal("Naukri", Header(request, "systemid"));
            Assert.Equal("application/json", Header(request, "Accept"));
            Assert.Contains("Chrome/", Header(request, "User-Agent"), StringComparison.Ordinal);
            Assert.NotEmpty(Header(request, "nkparam"));

            // A token is minted per request and never replayed.
            await client.FetchPageAsync(Criteria(), 1, Ct);
            Assert.Equal(2, handler.Requests.Count);
            Assert.NotEqual(
                Header(handler.Requests[0], "nkparam"),
                Header(handler.Requests[1], "nkparam")
            );
        }

        [Fact]
        public async Task BadRowsDropWithWarningInsteadOfPoisoning()
        {
            var logger = new RecordingLogger();
            var handler = new FakeHandler(_ => JsonResponse(Fixture("search-api-bad-row.json")));
            using var client = Client(handler, logger);

            var page = await client.FetchPageAsync(Criteria(), 1, Ct);

            var rows = page.Rows.Select(row => row.Snapshot).ToArray();
            Assert.Equal(2, rows.Length);
            Assert.Equal("011126000003", rows[0].ExternalJobId);
            Assert.Null(rows[1].PostedAt);
            Assert.Equal(2, page.DroppedRows);
            Assert.Equal(4, page.Total);
            Assert.Contains(LogLevel.Warning, logger.Levels);
        }

        [Fact]
        public async Task ExternalPortalRowsAreMarkedExternal()
        {
            var handler = new FakeHandler(_ => JsonResponse(Fixture("search-api-external.json")));
            using var client = Client(handler);

            var page = await client.FetchPageAsync(Criteria(), 1, Ct);

            Assert.Equal(4, page.Rows.Count);
            Assert.False(page.Rows[0].ExternalPortal);
            Assert.True(page.Rows[1].ExternalPortal);
            Assert.True(page.Rows[2].ExternalPortal);
            Assert.True(page.Rows[3].ExternalPortal);
            Assert.Null(page.Rows[1].Snapshot.ApplyUrl);
            Assert.Equal(
                "https://careers.litware.example/apply/9?src=naukri",
                page.Rows[2].Snapshot.ApplyUrl
            );
            Assert.Equal("https://jobs.tailspin.example/apply/7", page.Rows[3].Snapshot.ApplyUrl);
        }

        // ---- status mapping ----

        [Fact]
        public async Task Http400MeansCleanEndNotError()
        {
            var handler = new FakeHandler(_ => StatusResponse(HttpStatusCode.BadRequest));
            using var client = Client(handler);

            var page = await client.FetchPageAsync(Criteria(), 4, Ct);

            Assert.Equal(ChallengeKind.None, page.Challenge.Kind);
            Assert.True(page.EndOfListing);
            Assert.Empty(page.Rows);
            Assert.Equal(0, page.Total);
            Assert.Single(handler.Requests);
        }

        [Fact]
        public async Task Http406MapsToRateLimited()
        {
            var handler = new FakeHandler(_ => StatusResponse(HttpStatusCode.NotAcceptable));
            using var client = Client(handler);

            var page = await client.FetchPageAsync(Criteria(), 1, Ct);

            Assert.Equal(ChallengeKind.RateLimited, page.Challenge.Kind);
            Assert.Contains("406", page.Challenge.Evidence, StringComparison.Ordinal);
            Assert.False(page.EndOfListing);
            Assert.Empty(page.Rows);
            Assert.Single(handler.Requests);
        }

        [Fact]
        public async Task RedirectResponseIsClassifiedAsWallNotFollowed()
        {
            var handler = new FakeHandler(_ =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.Redirect);
                response.Headers.Location = new Uri("https://www.naukri.com/nlogin/login");
                return response;
            });
            using var client = Client(handler);

            var page = await client.FetchPageAsync(Criteria(), 1, Ct);

            Assert.Equal(ChallengeKind.SoftChallenge, page.Challenge.Kind);
            Assert.Single(handler.Requests);
            Assert.Equal("/jobapi/v3/search", handler.Requests[0].Uri?.AbsolutePath);

            // The production transport never follows an unexpected redirect.
            using var transport = NaukriApiClient.CreateTransportHandler();
            Assert.False(transport.AllowAutoRedirect);
        }

        [Fact]
        public async Task OtherNon200MapsToTransientError()
        {
            var handler = new FakeHandler(_ => StatusResponse(HttpStatusCode.InternalServerError));
            using var client = Client(handler);

            var page = await client.FetchPageAsync(Criteria(), 1, Ct);

            Assert.Equal(ChallengeKind.TransientError, page.Challenge.Kind);
            Assert.Contains("500", page.Challenge.Evidence, StringComparison.Ordinal);
            Assert.Empty(page.Rows);
        }

        // ---- paging ----

        [Fact]
        public async Task StopsAtReportedTotalAndAtMaxJobsWithBoundedRequestCount()
        {
            // A 25-job listing ends after page 2; the repeated id is deduped.
            var reportedTotal = new FakeHandler(request =>
                PageNumber(request) == 1
                    ? JsonResponse(SearchJson(25, Enumerable.Range(1, 20).Select(Job).ToArray()))
                    : JsonResponse(SearchJson(25, Enumerable.Range(20, 5).Select(Job).ToArray()))
            );
            using (var client = Client(reportedTotal))
            {
                var result = await client.SearchAsync(Criteria(), maxJobs: 100, Ct);

                Assert.Equal(2, reportedTotal.Requests.Count);
                Assert.Equal(2, result.PagesFetched);
                Assert.Equal(25, result.Total);
                Assert.Equal(24, result.Rows.Count);
                Assert.Equal(0, result.DroppedRows);
                Assert.Equal(ChallengeKind.None, result.Challenge.Kind);
            }

            // maxJobs stops the loop before another request is made.
            var capped = new FakeHandler(_ =>
                JsonResponse(SearchJson(100, Enumerable.Range(1, 20).Select(Job).ToArray()))
            );
            using (var client = Client(capped))
            {
                var result = await client.SearchAsync(Criteria(), maxJobs: 15, Ct);

                Assert.Single(capped.Requests);
                Assert.Equal(15, result.Rows.Count);
            }

            // Three pages of 20/20/5 stop exactly at the reported total.
            var threePages = new FakeHandler(request =>
            {
                var pageNumber = PageNumber(request);
                var count = pageNumber < 3 ? 20 : 5;
                var first = ((pageNumber - 1) * 20) + 1;
                return JsonResponse(
                    SearchJson(45, Enumerable.Range(first, count).Select(Job).ToArray())
                );
            });
            using (var client = Client(threePages))
            {
                var result = await client.SearchAsync(Criteria(), maxJobs: 1000, Ct);

                Assert.Equal(3, threePages.Requests.Count);
                Assert.Equal(45, result.Rows.Count);
            }
        }

        // ---- helpers ----

        private static NaukriApiClient Client(
            FakeHandler handler,
            ILogger<NaukriApiClient>? logger = null
        )
        {
            return new NaukriApiClient(
                new HttpClient(handler),
                logger ?? NullLogger<NaukriApiClient>.Instance
            );
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

        private static string SearchJson(int total, params object[] jobs)
        {
            return JsonSerializer.Serialize(new { noOfJobs = total, jobs });
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
            return int.Parse(Query(request.RequestUri)["pageNo"], CultureInfo.InvariantCulture);
        }

        private static Dictionary<string, string> Query(Uri? uri)
        {
            return uri!
                .Query.TrimStart('?')
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Split('=', 2))
                .ToDictionary(
                    pair => Uri.UnescapeDataString(pair[0]),
                    pair => pair.Length > 1 ? Uri.UnescapeDataString(pair[1]) : string.Empty,
                    StringComparer.Ordinal
                );
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

        private sealed class RecordingLogger : ILogger<NaukriApiClient>
        {
            public List<LogLevel> Levels { get; } = [];

            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter
            )
            {
                Levels.Add(logLevel);
            }
        }
    }
}
