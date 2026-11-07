using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JobScrape.Api.Controllers;
using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.Runs;
using JobScrape.Application.Features.Scraping;
using JobScrape.Application.Features.SearchProfiles;
using JobScrape.Application.Features.Sessions;
using JobScrape.Domain.Abstractions;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using JobScrape.Infrastructure.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace JobScrape.Api.Tests
{
    /// <summary>
    /// The runs vertical end to end through HTTP, SQLite, the coordinator,
    /// and the real run handler; only the browser-facing ports (adapter,
    /// session manager, artifacts, delays) are fakes.
    /// </summary>
    public sealed class RunsEndpointTests
    {
        private static readonly string[] Pune = ["Pune"];

        private static readonly string[] DotNetSkills = ["C#", "ASP.NET Core"];
        private static readonly string[] Contoso = ["Contoso"];

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private static ApiFactory Factory(FakeSource source)
        {
            return new ApiFactory
            {
                TestServices = services =>
                {
                    services.RemoveAll<IJobSource>();
                    services.AddSingleton<IJobSource>(source);
                    services.RemoveAll<ISessionManager>();
                    services.AddScoped<ISessionManager, CleanSessions>();
                    services.RemoveAll<IArtifactStore>();
                    services.AddSingleton<IArtifactStore, NoArtifacts>();
                    services.RemoveAll<IDelay>();
                    services.AddSingleton<IDelay, NoDelay>();
                },
            };
        }

        private static async Task<(Guid Profile, Guid Session)> SeedAsync(HttpClient client)
        {
            using var profile = await client.PostAsJsonAsync(
                "api/v1/search-profiles",
                new
                {
                    name = "backend",
                    sourceKey = "naukri",
                    keywords = "c#",
                    locations = Pune,
                    companies = Contoso,
                },
                Ct
            );
            using var session = await client.PostAsJsonAsync(
                "api/v1/sessions",
                new { name = "naukri-main", sourceKey = "naukri" },
                Ct
            );
            return (
                (await profile.Content.ReadFromJsonAsync<SearchProfileResponse>(Ct))!.Id,
                (await session.Content.ReadFromJsonAsync<SessionResponse>(Ct))!.Id
            );
        }

        private static async Task<RunResponse> WaitAsync(
            HttpClient client,
            Guid id,
            Func<RunResponse, bool> done
        )
        {
            for (var attempt = 0; attempt < 200; attempt++)
            {
                var run = await client.GetFromJsonAsync<RunResponse>($"api/v1/runs/{id}", Ct);
                if (done(run!))
                {
                    return run!;
                }

                await Task.Delay(50, Ct);
            }

            throw new TimeoutException($"Run {id} did not reach the expected state.");
        }

        [Fact]
        public void RejectsNullHandlers()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new RunsController(null!, null!, null!, null!, null!, null!)
            );
        }

        [Fact]
        public async Task StartFollowAndReadPostingsThroughSqlite()
        {
            await using var factory = Factory(new FakeSource());
            using var client = await factory.CreateAdminClientAsync(Ct);
            var (profile, session) = await SeedAsync(client);

            using var started = await client.PostAsJsonAsync(
                "api/v1/runs",
                new { profileId = profile, browserProfileId = session },
                Ct
            );
            var run = await started.Content.ReadFromJsonAsync<RunResponse>(Ct);

            Assert.Equal(HttpStatusCode.Accepted, started.StatusCode);
            Assert.Equal($"/api/v1/runs/{run!.Id}", started.Headers.Location!.OriginalString);
            Assert.Equal("backend", run.ProfileName);
            Assert.Equal(["Contoso"], run.Companies);

            var done = await WaitAsync(client, run.Id, r => r.IsTerminal);
            Assert.Equal("Completed", done.Stage);
            Assert.Equal(1, done.PagesFetched);
            Assert.Equal(2, done.PostingsFound);
            Assert.Equal(2, done.PostingsNew);
            Assert.NotNull(done.FinishedAt);

            var postings = await client.GetFromJsonAsync<RunPostingListResponse>(
                $"api/v1/runs/{run.Id}/postings",
                Ct
            );
            Assert.Equal(["101", "102"], postings!.Items.Select(p => p.ExternalJobId).Order());
            Assert.Equal(2, postings.Total);
            Assert.All(postings.Items, p => Assert.True(p.IsNew));

            var secondPage = await client.GetFromJsonAsync<RunPostingListResponse>(
                $"api/v1/runs/{run.Id}/postings?offset=1&limit=1",
                Ct
            );
            Assert.Single(secondPage!.Items);
            Assert.Equal(2, secondPage.Total);

            using var csv = await client.GetAsync(
                $"api/v1/runs/{run.Id}/postings/export?format=csv",
                Ct
            );
            Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
            Assert.Equal("text/csv", csv.Content.Headers.ContentType!.MediaType);
            var csvText = await csv.Content.ReadAsStringAsync(Ct);
            Assert.StartsWith("Title,Company,Location,", csvText);
            Assert.Contains("https://www.naukri.com/job-listings-dev-101", csvText);
            var csvName = csv.Content.Headers.ContentDisposition!.FileNameStar!;
            Assert.StartsWith("naukri-backend-", csvName, StringComparison.Ordinal);
            Assert.EndsWith("-postings.csv", csvName, StringComparison.Ordinal);

            using var xlsx = await client.GetAsync(
                $"api/v1/runs/{run.Id}/postings/export?format=xlsx",
                Ct
            );
            Assert.Equal(HttpStatusCode.OK, xlsx.StatusCode);
            Assert.Equal(
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                xlsx.Content.Headers.ContentType!.MediaType
            );
            Assert.True((await xlsx.Content.ReadAsByteArrayAsync(Ct)).Length > 0);
            var xlsxName = xlsx.Content.Headers.ContentDisposition!.FileNameStar!;
            Assert.StartsWith("naukri-backend-", xlsxName, StringComparison.Ordinal);
            Assert.EndsWith("-postings.xlsx", xlsxName, StringComparison.Ordinal);

            using var only = await client.GetAsync(
                $"api/v1/runs/{run.Id}/postings/export?format=csv&group=only",
                Ct
            );
            Assert.Equal(HttpStatusCode.OK, only.StatusCode);
            var onlyText = await only.Content.ReadAsStringAsync(Ct);
            Assert.Contains("101", onlyText, StringComparison.Ordinal);
            Assert.DoesNotContain("102", onlyText, StringComparison.Ordinal);
            Assert.EndsWith(
                "-postings-only-companies.csv",
                only.Content.Headers.ContentDisposition!.FileNameStar!,
                StringComparison.Ordinal
            );

            using var exclude = await client.GetAsync(
                $"api/v1/runs/{run.Id}/postings/export?format=csv&group=exclude",
                Ct
            );
            Assert.Equal(HttpStatusCode.OK, exclude.StatusCode);
            var excludeText = await exclude.Content.ReadAsStringAsync(Ct);
            Assert.DoesNotContain("101", excludeText, StringComparison.Ordinal);
            Assert.Contains("102", excludeText, StringComparison.Ordinal);

            using var badGroup = await client.GetAsync(
                $"api/v1/runs/{run.Id}/postings/export?format=csv&group=nope",
                Ct
            );
            Assert.Equal(HttpStatusCode.BadRequest, badGroup.StatusCode);
            var badGroupBody = await badGroup.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("EXPORT_GROUP_UNSUPPORTED", badGroupBody.GetProperty("code").GetString());
            Assert.Equal(
                "Export group is not supported.",
                badGroupBody.GetProperty("title").GetString()
            );
            Assert.Equal(
                "Export group 'nope' is not supported. Use 'all', 'only' or 'exclude'.",
                badGroupBody.GetProperty("detail").GetString()
            );

            using var badFormat = await client.GetAsync(
                $"api/v1/runs/{run.Id}/postings/export?format=pdf",
                Ct
            );
            Assert.Equal(HttpStatusCode.BadRequest, badFormat.StatusCode);
            var badFormatBody = await badFormat.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal(
                "EXPORT_FORMAT_UNSUPPORTED",
                badFormatBody.GetProperty("code").GetString()
            );
            Assert.Equal(
                "Export format is not supported.",
                badFormatBody.GetProperty("title").GetString()
            );
            Assert.Equal(
                "Export format 'pdf' is not supported. Use 'csv' or 'xlsx'.",
                badFormatBody.GetProperty("detail").GetString()
            );

            using var missing = await client.GetAsync(
                $"api/v1/runs/{Guid.NewGuid()}/postings/export?format=csv",
                Ct
            );
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

            var list = await client.GetFromJsonAsync<RunListResponse>("api/v1/runs", Ct);
            Assert.Equal(run.Id, Assert.Single(list!.Items).Id);
            Assert.Equal(1, list.Total);

            var skipped = await client.GetFromJsonAsync<RunListResponse>(
                "api/v1/runs?offset=1&limit=50",
                Ct
            );
            Assert.Empty(skipped!.Items);
            Assert.Equal(1, skipped.Total);

            var negative = await client.GetFromJsonAsync<RunListResponse>(
                "api/v1/runs?offset=-5&limit=50",
                Ct
            );
            Assert.Equal(run.Id, Assert.Single(negative!.Items).Id);
        }

        [Fact]
        public async Task SkillMatchAndRecencyFlowKeepsOnlyMatchingFreshPostings()
        {
            var source = new FakeSource { Skilled = true };
            await using var factory = Factory(source);
            using var client = await factory.CreateAdminClientAsync(Ct);

            using var profile = await client.PostAsJsonAsync(
                "api/v1/search-profiles",
                new
                {
                    name = "dotnet-fresh",
                    sourceKey = "naukri",
                    keywords = "asp.net core c#",
                    locations = Pune,
                    postedWithinDays = 1,
                    remoteOnly = false,
                    requiredSkills = DotNetSkills,
                },
                Ct
            );
            var profileId = (
                await profile.Content.ReadFromJsonAsync<SearchProfileResponse>(Ct)
            )!.Id;
            using var session = await client.PostAsJsonAsync(
                "api/v1/sessions",
                new { name = "naukri-main", sourceKey = "naukri" },
                Ct
            );
            var sessionId = (await session.Content.ReadFromJsonAsync<SessionResponse>(Ct))!.Id;

            using var started = await client.PostAsJsonAsync(
                "api/v1/runs",
                new { profileId, browserProfileId = sessionId },
                Ct
            );
            var run = await started.Content.ReadFromJsonAsync<RunResponse>(Ct);

            var done = await WaitAsync(client, run!.Id, r => r.IsTerminal);
            Assert.Equal("Completed", done.Stage);
            Assert.Equal(1, done.PagesFetched);
            Assert.Equal(1, done.PostingsFound);
            Assert.Equal(1, done.PostingsNew);

            var postings = await client.GetFromJsonAsync<RunPostingListResponse>(
                $"api/v1/runs/{run.Id}/postings",
                Ct
            );
            Assert.Equal(["101"], postings!.Items.Select(p => p.ExternalJobId));
            Assert.Equal(1, postings.Total);

            using var csv = await client.GetAsync(
                $"api/v1/runs/{run.Id}/postings/export?format=csv",
                Ct
            );
            var csvText = await csv.Content.ReadAsStringAsync(Ct);
            Assert.Contains("job-listings-dev-101", csvText);
            Assert.DoesNotContain("job-listings-dev-102", csvText);
            Assert.DoesNotContain("job-listings-dev-103", csvText);
            var fileName = csv.Content.Headers.ContentDisposition!.FileNameStar!;
            Assert.StartsWith("naukri-dotnet-fresh-", fileName, StringComparison.Ordinal);
            Assert.EndsWith("-postings.csv", fileName, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ResightedPostingWithMismatchedSkillsStaysOutOfPostingsAndExport()
        {
            var source = new FakeSource { Skilled = true };
            await using var factory = Factory(source);
            using var client = await factory.CreateAdminClientAsync(Ct);

            using var profile = await client.PostAsJsonAsync(
                "api/v1/search-profiles",
                new
                {
                    name = "dotnet-fresh",
                    sourceKey = "naukri",
                    keywords = "asp.net core c#",
                    locations = Pune,
                    postedWithinDays = 1,
                    remoteOnly = false,
                    requiredSkills = DotNetSkills,
                },
                Ct
            );
            var profileId = (
                await profile.Content.ReadFromJsonAsync<SearchProfileResponse>(Ct)
            )!.Id;
            using var session = await client.PostAsJsonAsync(
                "api/v1/sessions",
                new { name = "naukri-main", sourceKey = "naukri" },
                Ct
            );
            var sessionId = (await session.Content.ReadFromJsonAsync<SessionResponse>(Ct))!.Id;

            using var first = await client.PostAsJsonAsync(
                "api/v1/runs",
                new { profileId, browserProfileId = sessionId },
                Ct
            );
            var run1 = await first.Content.ReadFromJsonAsync<RunResponse>(Ct);
            var done1 = await WaitAsync(client, run1!.Id, r => r.IsTerminal);
            Assert.Equal("Completed", done1.Stage);
            Assert.Equal(1, done1.PostingsFound);

            source.ResightMismatch = true;

            using var second = await client.PostAsJsonAsync(
                "api/v1/runs",
                new { profileId, browserProfileId = sessionId },
                Ct
            );
            Assert.True(
                second.IsSuccessStatusCode,
                $"Expected success but got {(int)second.StatusCode}."
            );
            var run2 = await second.Content.ReadFromJsonAsync<RunResponse>(Ct);
            var done2 = await WaitAsync(client, run2!.Id, r => r.IsTerminal);
            Assert.Equal("Completed", done2.Stage);
            Assert.Equal(0, done2.PostingsFound);

            var postings2 = await client.GetFromJsonAsync<RunPostingListResponse>(
                $"api/v1/runs/{run2.Id}/postings",
                Ct
            );
            Assert.Empty(postings2!.Items);

            using var csv2 = await client.GetAsync(
                $"api/v1/runs/{run2.Id}/postings/export?format=csv",
                Ct
            );
            var csvText2 = await csv2.Content.ReadAsStringAsync(Ct);
            Assert.DoesNotContain("job-listings-dev-101", csvText2);

            var postings1 = await client.GetFromJsonAsync<RunPostingListResponse>(
                $"api/v1/runs/{run1.Id}/postings",
                Ct
            );
            var stored = Assert.Single(postings1!.Items);
            Assert.Equal("101", stored.ExternalJobId);
            Assert.Contains("C#", stored.Skills);
        }

        [Fact]
        public async Task OneRunAtATimeAndCancelKeepsPartialResults()
        {
            var source = new FakeSource { Blocked = true };
            await using var factory = Factory(source);
            using var client = await factory.CreateAdminClientAsync(Ct);
            var (profile, session) = await SeedAsync(client);
            var body = new { profileId = profile, browserProfileId = session };

            using var first = await client.PostAsJsonAsync("api/v1/runs", body, Ct);
            var run = await first.Content.ReadFromJsonAsync<RunResponse>(Ct);
            await source.Entered.Task.WaitAsync(Ct);

            using var second = await client.PostAsJsonAsync("api/v1/runs", body, Ct);
            Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
            Assert.Equal(
                "RUN_IN_PROGRESS",
                (await second.Content.ReadFromJsonAsync<JsonElement>(Ct))
                    .GetProperty("code")
                    .GetString()
            );

            using var cancel = await client.PostAsync($"api/v1/runs/{run!.Id}/cancel", null, Ct);
            Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
            var cancelled = await WaitAsync(client, run.Id, r => r.IsTerminal);
            Assert.Equal("Cancelled", cancelled.Stage);

            // Idempotent on a terminal run, and the slot is free again.
            using var again = await client.PostAsync($"api/v1/runs/{run.Id}/cancel", null, Ct);
            Assert.Equal(
                "Cancelled",
                (await again.Content.ReadFromJsonAsync<RunResponse>(Ct))!.Stage
            );
            source.Blocked = false;
            using var next = await client.PostAsJsonAsync("api/v1/runs", body, Ct);
            Assert.Equal(HttpStatusCode.Accepted, next.StatusCode);
            var nextRun = await next.Content.ReadFromJsonAsync<RunResponse>(Ct);
            Assert.Equal(
                "Completed",
                (await WaitAsync(client, nextRun!.Id, r => r.IsTerminal)).Stage
            );
        }

        [Fact]
        public async Task ACrashAfterStartFailsTheRunAndFreesTheSlot()
        {
            var source = new FakeSource { Throws = true };
            await using var factory = Factory(source);
            using var client = await factory.CreateAdminClientAsync(Ct);
            var (profile, session) = await SeedAsync(client);
            var body = new { profileId = profile, browserProfileId = session };

            using var started = await client.PostAsJsonAsync("api/v1/runs", body, Ct);
            var run = await started.Content.ReadFromJsonAsync<RunResponse>(Ct);
            Assert.Equal(HttpStatusCode.Accepted, started.StatusCode);

            var failed = await WaitAsync(client, run!.Id, r => r.IsTerminal);
            Assert.Equal("Failed", failed.Stage);
            Assert.Equal("Unexpected error; see logs.", failed.FailureReason);

            source.Throws = false;
            HttpResponseMessage? next = null;
            for (var attempt = 0; attempt < 100; attempt++)
            {
                next?.Dispose();
                next = await client.PostAsJsonAsync("api/v1/runs", body, Ct);
                if (next.StatusCode != HttpStatusCode.Conflict)
                {
                    break;
                }

                // The slot frees just after the run is recorded as failed.
                await Task.Delay(20, Ct);
            }

            Assert.Equal(HttpStatusCode.Accepted, next!.StatusCode);
            next.Dispose();
        }

        [Fact]
        public async Task GateRefusalsAndUnknownRunsAreProblems()
        {
            await using var factory = Factory(new FakeSource());
            using var client = await factory.CreateAdminClientAsync(Ct);

            using var empty = await client.PostAsJsonAsync(
                "api/v1/runs",
                new { profileId = Guid.Empty, browserProfileId = Guid.Empty },
                Ct
            );
            Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
            var errors = (await empty.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty(
                "errors"
            );
            Assert.True(errors.TryGetProperty("profileId", out _));
            Assert.True(errors.TryGetProperty("browserProfileId", out _));

            using var missing = await client.PostAsJsonAsync(
                "api/v1/runs",
                new { profileId = Guid.NewGuid(), browserProfileId = Guid.NewGuid() },
                Ct
            );
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
            Assert.Equal(
                "PROFILE_NOT_FOUND",
                (await missing.Content.ReadFromJsonAsync<JsonElement>(Ct))
                    .GetProperty("code")
                    .GetString()
            );

            var unknown = Guid.NewGuid();
            foreach (
                var path in new[] { $"api/v1/runs/{unknown}", $"api/v1/runs/{unknown}/postings" }
            )
            {
                using var response = await client.GetAsync(path, Ct);
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            }

            using var cancel = await client.PostAsync($"api/v1/runs/{unknown}/cancel", null, Ct);
            Assert.Equal(HttpStatusCode.NotFound, cancel.StatusCode);
        }

        [Fact]
        public async Task StartupFailsRunsAStopLeftExecuting()
        {
            await using var factory = Factory(new FakeSource());
            using var client = await factory.CreateAdminClientAsync(Ct);
            var (profileId, _) = await SeedAsync(client);

            Guid staleId;
            using (var scope = factory.Services.CreateScope())
            {
                var profiles = scope.ServiceProvider.GetRequiredService<ISearchProfileRepository>();
                var profile = await profiles.GetAsync(SearchProfileId.From(profileId), Ct);
                var stale = ScrapeRun.Create(profile!, DateTimeOffset.UtcNow);
                stale.Start(DateTimeOffset.UtcNow);
                stale.BeginSearching();
                await scope
                    .ServiceProvider.GetRequiredService<IScrapeRunRepository>()
                    .AddAsync(stale, Ct);
                await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
                staleId = stale.Id.Value;
            }

            // What the host does on its next start.
            await factory.Services.GetRequiredService<RunCoordinator>().StartAsync(Ct);

            var run = await client.GetFromJsonAsync<RunResponse>($"api/v1/runs/{staleId}", Ct);
            Assert.Equal("Failed", run!.Stage);
            Assert.StartsWith("Interrupted", run.FailureReason, StringComparison.Ordinal);
        }

        private sealed class FakeSource : IJobSource
        {
            public volatile bool Blocked;

            public volatile bool Throws;

            public volatile bool Skilled;

            public volatile bool ResightMismatch;

            public TaskCompletionSource Entered { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            public SourceKey Key => SourceKey.Naukri;

            public SourceCapabilities Capabilities { get; } = new(SupportsDetails: false);

            public async Task<SearchPageResult> SearchPageAsync(
                SourceRunContext context,
                SearchPageRequest request,
                CancellationToken cancellationToken
            )
            {
                Entered.TrySetResult();
                if (Throws)
                {
                    throw new InvalidOperationException("Adapter bug.");
                }

                if (Blocked)
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                }

                var now = DateTimeOffset.UtcNow;
                JobSnapshot[] jobs = Skilled
                    ? ResightMismatch
                        ? [Snapshot("101", ["Java"], now.AddHours(-3))]
                        :
                        [
                            Snapshot("101", ["C#", "ASP.NET Core"], now.AddHours(-2)),
                            Snapshot("102", ["Java"], now.AddHours(-3)),
                            Snapshot("103", ["C#"], now.AddDays(-5)),
                        ]
                    : [Snapshot("101"), Snapshot("102")];
                return new SearchPageResult(
                    jobs,
                    null,
                    new PageFingerprint("101", "1 - 2 of 2"),
                    ChallengeAssessment.Clear
                );
            }

            public Task<JobDetailResult> GetDetailAsync(
                SourceRunContext context,
                JobReference job,
                CancellationToken cancellationToken
            )
            {
                throw new NotSupportedException("Details are off for this fake.");
            }

            private static JobSnapshot Snapshot(
                string id,
                string[]? skills = null,
                DateTimeOffset? postedAt = null
            )
            {
                return new JobSnapshot
                {
                    Source = SourceKey.Naukri,
                    ExternalJobId = id,
                    Url = $"https://www.naukri.com/job-listings-dev-{id}",
                    Title = $"Developer {id}",
                    Company = id == "101" ? "Contoso" : "Fabrikam",
                    Skills = skills ?? [],
                    PostedAt = postedAt,
                };
            }
        }

        private sealed class CleanSessions : ISessionManager
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

        private sealed class NoArtifacts : IArtifactStore
        {
            public Task<string> CaptureAsync(
                SourceRunContext context,
                ChallengeAssessment assessment,
                CancellationToken cancellationToken
            )
            {
                return Task.FromResult("runs/none");
            }
        }

        private sealed class NoDelay : IDelay
        {
            public Task WaitAsync(TimeSpan duration, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }
    }
}
