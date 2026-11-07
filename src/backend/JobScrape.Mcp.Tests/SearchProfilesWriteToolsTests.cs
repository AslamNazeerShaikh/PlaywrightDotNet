using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.Scraping;
using JobScrape.Application.Features.SearchProfiles;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using JobScrape.Mcp.Tools;
using ModelContextProtocol;
using Xunit;

namespace JobScrape.Mcp.Tests
{
    public sealed class SearchProfilesWriteToolsTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

        private static readonly RunLimits NaukriPolicy = new(
            3,
            60,
            60,
            TimeSpan.FromMilliseconds(2500),
            TimeSpan.FromMilliseconds(6000)
        );

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private static SearchProfilesWriteTools Tools(FakeProfiles profiles)
        {
            var catalog = new SourceSettingsCatalog([
                new SourceSettings(
                    SourceKey.Naukri,
                    true,
                    25,
                    60,
                    2,
                    NaukriPolicy.MaxPages,
                    NaukriPolicy.MaxJobs,
                    TimeSpan.FromMinutes(45),
                    NaukriPolicy
                ),
                new SourceSettings(
                    SourceKey.LinkedIn,
                    false,
                    4,
                    8,
                    1,
                    NaukriPolicy.MaxPages,
                    NaukriPolicy.MaxJobs,
                    TimeSpan.FromHours(1),
                    NaukriPolicy
                ),
            ]);
            var clock = new FixedClock(Now);
            return new SearchProfilesWriteTools(
                new CreateSearchProfileHandler(
                    profiles,
                    profiles,
                    catalog,
                    clock,
                    CallerContext.LocalOperator
                ),
                new UpdateSearchProfileHandler(
                    profiles,
                    profiles,
                    catalog,
                    clock,
                    CallerContext.LocalOperator
                ),
                new FakeCaller()
            );
        }

        [Fact]
        public async Task CreateProfileSavesWithPolicyDefaults()
        {
            var tools = Tools(new FakeProfiles());

            var response = await tools.CreateProfileAsync(
                "backend",
                "naukri",
                "c#",
                ["Pune"],
                null,
                null,
                false,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                Ct
            );

            Assert.Equal("backend", response.Name);
            Assert.Equal("naukri", response.SourceKey);
            Assert.Equal(["Pune"], response.Locations);
            Assert.Equal(3, response.MaxPages);
            Assert.True(response.Enabled);
        }

        [Fact]
        public async Task CreateProfileMissingFieldsFailsValidation()
        {
            var error = await Assert.ThrowsAsync<McpException>(() =>
                Tools(new FakeProfiles()).CreateProfileAsync(cancellationToken: Ct)
            );

            Assert.StartsWith("VALIDATION_FAILED:", error.Message);
        }

        [Fact]
        public async Task CreateProfileDuplicateFailsNameTaken()
        {
            var tools = Tools(new FakeProfiles());
            await tools.CreateProfileAsync(
                "backend",
                "naukri",
                "c#",
                ["Pune"],
                cancellationToken: Ct
            );

            var error = await Assert.ThrowsAsync<McpException>(() =>
                tools.CreateProfileAsync("backend", "naukri", "c#", ["Pune"], cancellationToken: Ct)
            );

            Assert.StartsWith("PROFILE_NAME_TAKEN:", error.Message);
        }

        [Fact]
        public async Task UpdateProfileReplacesEditableFields()
        {
            var tools = Tools(new FakeProfiles());
            var created = await tools.CreateProfileAsync(
                "backend",
                "naukri",
                "c#",
                ["Pune"],
                cancellationToken: Ct
            );

            var response = await tools.UpdateProfileAsync(
                created.Id.ToString(),
                true,
                "backend-api",
                "asp.net",
                ["Hyderabad"],
                cancellationToken: Ct
            );

            Assert.Equal("backend-api", response.Name);
            Assert.Equal("asp.net", response.Keywords);
            Assert.Equal(["Hyderabad"], response.Locations);
            Assert.Equal("naukri", response.SourceKey);
        }

        [Fact]
        public async Task UpdateProfileBadFormatFailsValidation()
        {
            var error = await Assert.ThrowsAsync<McpException>(() =>
                Tools(new FakeProfiles())
                    .UpdateProfileAsync("not-a-guid", true, cancellationToken: Ct)
            );

            Assert.StartsWith("VALIDATION_FAILED:", error.Message);
        }

        [Fact]
        public async Task UpdateProfileEmptyIdFailsValidation()
        {
            var error = await Assert.ThrowsAsync<McpException>(() =>
                Tools(new FakeProfiles())
                    .UpdateProfileAsync(Guid.Empty.ToString(), true, cancellationToken: Ct)
            );

            Assert.StartsWith("VALIDATION_FAILED:", error.Message);
        }

        [Fact]
        public async Task UpdateProfileMissingFailsNotFound()
        {
            var error = await Assert.ThrowsAsync<McpException>(() =>
                Tools(new FakeProfiles())
                    .UpdateProfileAsync(Guid.NewGuid().ToString(), true, cancellationToken: Ct)
            );

            Assert.StartsWith("PROFILE_NOT_FOUND:", error.Message);
        }

        [Fact]
        public async Task UpdateProfileDuplicateFailsNameTaken()
        {
            var tools = Tools(new FakeProfiles());
            await tools.CreateProfileAsync(
                "backend",
                "naukri",
                "c#",
                ["Pune"],
                cancellationToken: Ct
            );
            var other = await tools.CreateProfileAsync(
                "frontend",
                "naukri",
                "react",
                ["Pune"],
                cancellationToken: Ct
            );

            var error = await Assert.ThrowsAsync<McpException>(() =>
                tools.UpdateProfileAsync(
                    other.Id.ToString(),
                    true,
                    "backend",
                    "react",
                    ["Pune"],
                    cancellationToken: Ct
                )
            );

            Assert.StartsWith("PROFILE_NAME_TAKEN:", error.Message);
        }

        [Fact]
        public async Task UpdateProfileBadFieldsFailsValidation()
        {
            var tools = Tools(new FakeProfiles());
            var created = await tools.CreateProfileAsync(
                "backend",
                "naukri",
                "c#",
                ["Pune"],
                cancellationToken: Ct
            );

            var error = await Assert.ThrowsAsync<McpException>(() =>
                tools.UpdateProfileAsync(
                    created.Id.ToString(),
                    true,
                    " ",
                    "c#",
                    ["Pune"],
                    cancellationToken: Ct
                )
            );

            Assert.StartsWith("VALIDATION_FAILED:", error.Message);
        }

        [Fact]
        public async Task UpdateProfileHighRiskEnablementFailsValidation()
        {
            var tools = Tools(new FakeProfiles());
            var created = await tools.CreateProfileAsync(
                "linkedin-backend",
                "linkedin",
                "c#",
                ["Pune"],
                cancellationToken: Ct
            );
            Assert.False(created.Enabled);

            var error = await Assert.ThrowsAsync<McpException>(() =>
                tools.UpdateProfileAsync(
                    created.Id.ToString(),
                    true,
                    "linkedin-backend",
                    "c#",
                    ["Pune"],
                    cancellationToken: Ct
                )
            );

            Assert.StartsWith("VALIDATION_FAILED:", error.Message);
        }

        [Fact]
        public void RejectsNulls()
        {
            var profiles = new FakeProfiles();
            var catalog = new SourceSettingsCatalog([]);
            var clock = new FixedClock(Now);
            var create = new CreateSearchProfileHandler(
                profiles,
                profiles,
                catalog,
                clock,
                CallerContext.LocalOperator
            );
            var update = new UpdateSearchProfileHandler(
                profiles,
                profiles,
                catalog,
                clock,
                CallerContext.LocalOperator
            );
            var caller = new FakeCaller();
            Assert.Throws<ArgumentNullException>(() =>
                new SearchProfilesWriteTools(null!, update, caller)
            );
            Assert.Throws<ArgumentNullException>(() =>
                new SearchProfilesWriteTools(create, null!, caller)
            );
            Assert.Throws<ArgumentNullException>(() =>
                new SearchProfilesWriteTools(create, update, null!)
            );
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

        private sealed class FixedClock(DateTimeOffset now) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow()
            {
                return now;
            }
        }

        private sealed class FakeProfiles : ISearchProfileRepository, IUnitOfWork
        {
            private readonly List<SearchProfile> _profiles = [];

            public Task<SearchProfile?> GetAsync(
                SearchProfileId id,
                CancellationToken cancellationToken
            )
            {
                return Task.FromResult(_profiles.FirstOrDefault(p => p.Id == id));
            }

            public Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken)
            {
                return Task.FromResult(
                    _profiles.Any(p =>
                        string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)
                    )
                );
            }

            public Task AddAsync(SearchProfile profile, CancellationToken cancellationToken)
            {
                _profiles.Add(profile);
                return Task.CompletedTask;
            }

            public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(0);
            }

            public bool IsUniqueViolation(Exception exception)
            {
                return false;
            }
        }
    }
}
