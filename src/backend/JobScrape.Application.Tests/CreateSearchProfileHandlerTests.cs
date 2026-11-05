using JobScrape.Application.Features.Scraping;
using JobScrape.Application.Features.SearchProfiles;
using JobScrape.Application.Results;
using JobScrape.Application.Tests.Scraping;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace JobScrape.Application.Tests
{
    public sealed class CreateSearchProfileHandlerTests
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

        private static (
            CreateSearchProfileHandler Handler,
            ScrapeHarness.InMemoryStore Store
        ) Create()
        {
            var store = new ScrapeHarness.InMemoryStore();
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
            return (
                new CreateSearchProfileHandler(store, store, catalog, new FakeTimeProvider(Now)),
                store
            );
        }

        private static CreateSearchProfileRequest Valid()
        {
            return new CreateSearchProfileRequest(
                "  naukri-dotnet   pune ",
                " NAUKRI ",
                "asp.net core c#",
                ["Pune", " ", "pune", "Hyderabad"],
                6,
                7,
                RemoteOnly: false
            );
        }

        private static IReadOnlyDictionary<string, string[]> FieldErrors(
            Result<SearchProfileResponse> result
        )
        {
            Assert.True(result.IsFailure);
            Assert.Equal("VALIDATION_FAILED", result.Error.Code);
            return Assert.IsAssignableFrom<IReadOnlyDictionary<string, string[]>>(
                result.Error.Metadata!["errors"]
            );
        }

        [Fact]
        public async Task AcceptsAPageCapUpToTheCeiling()
        {
            var store = new ScrapeHarness.InMemoryStore();
            var settings = new SourceSettings(
                SourceKey.Naukri,
                true,
                25,
                60,
                2,
                1000,
                20_000,
                TimeSpan.FromMinutes(45),
                NaukriPolicy
            );
            var handler = new CreateSearchProfileHandler(
                store,
                store,
                new SourceSettingsCatalog([settings]),
                new FakeTimeProvider(Now)
            );

            var result = await handler.HandleAsync(
                Valid() with { MaxPages = 500, MaxJobs = 10_000 },
                Ct
            );

            Assert.True(result.IsSuccess);
            Assert.Equal(500, result.Value.MaxPages);
            Assert.Equal(10_000, result.Value.MaxJobs);
        }

        [Fact]
        public async Task AcceptsAStartPagePastPageOne()
        {
            var store = new ScrapeHarness.InMemoryStore();
            var settings = new SourceSettings(
                SourceKey.Naukri,
                true,
                250,
                600,
                20,
                1000,
                20_000,
                TimeSpan.FromMinutes(45),
                NaukriPolicy
            );
            var handler = new CreateSearchProfileHandler(
                store,
                store,
                new SourceSettingsCatalog([settings]),
                new FakeTimeProvider(Now)
            );

            var result = await handler.HandleAsync(
                Valid() with { StartPage = 31, MaxPages = 30 },
                Ct
            );

            Assert.True(result.IsSuccess);
            Assert.Equal(31, result.Value.StartPage);
            Assert.Equal(30, result.Value.MaxPages);
        }

        [Fact]
        public async Task OmitsStartPageAsPageOne()
        {
            var (handler, _) = Create();

            var result = await handler.HandleAsync(Valid(), Ct);

            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.Value.StartPage);
        }

        [Fact]
        public async Task RejectsAStartPageOfZero()
        {
            var (handler, _) = Create();

            var result = await handler.HandleAsync(Valid() with { StartPage = 0 }, Ct);
            var errors = FieldErrors(result);

            Assert.True(errors.ContainsKey("startPage"));
        }

        [Fact]
        public async Task RejectsAWindowPastTheCeiling()
        {
            var (handler, _) = Create();

            var result = await handler.HandleAsync(
                Valid() with { StartPage = 3, MaxPages = 2 },
                Ct
            );
            var errors = FieldErrors(result);

            Assert.True(errors.ContainsKey("startPage"));
        }

        [Fact]
        public async Task ChecksTheEffectiveWindowWhenMaxPagesIsOmitted()
        {
            var (handler, _) = Create();

            // The default cap is 3 pages here, so starting at 3 already
            // runs past the ceiling even with no explicit maxPages.
            var result = await handler.HandleAsync(Valid() with { StartPage = 3 }, Ct);
            var errors = FieldErrors(result);

            Assert.True(errors.ContainsKey("startPage"));
        }

        [Fact]
        public async Task RejectsMoreThanTheCompanyLimit()
        {
            var (handler, _) = Create();
            var companies = Enumerable
                .Range(0, SearchCriteria.MaxCompanies + 1)
                .Select(index => $"Company {index}")
                .ToArray();

            var errors = FieldErrors(
                await handler.HandleAsync(Valid() with { Companies = companies }, Ct)
            );

            Assert.Contains("companies", errors.Keys);
        }

        [Fact]
        public async Task CreatesWithPolicyDefaultsAndSaves()
        {
            var (handler, store) = Create();

            var result = await handler.HandleAsync(Valid(), Ct);

            Assert.True(result.IsSuccess);
            var profile = result.Value;
            Assert.Equal("naukri-dotnet pune", profile.Name);
            Assert.Equal("naukri", profile.SourceKey);
            Assert.Equal(["Pune", "Hyderabad"], profile.Locations);
            Assert.Equal(6, profile.ExperienceYears);
            Assert.Equal(7, profile.PostedWithinDays);
            Assert.Equal(3, profile.MaxPages);
            Assert.Equal(60, profile.MaxJobs);
            Assert.Equal(60, profile.MaxDetails);
            Assert.Equal(2500, profile.MinDelayMs);
            Assert.Equal(6000, profile.MaxDelayMs);
            Assert.True(profile.Enabled);
            Assert.Equal(Now, profile.CreatedAt);
            Assert.Equal(Now, profile.UpdatedAt);
            Assert.Single(store.SearchProfiles);
            Assert.Equal(1, store.Saves);
        }

        [Fact]
        public async Task TighterCapsAreAccepted()
        {
            var (handler, _) = Create();

            var result = await handler.HandleAsync(
                Valid() with
                {
                    MaxPages = 1,
                    MaxJobs = 20,
                    MaxDetails = 0,
                    MinDelayMs = 5000,
                    MaxDelayMs = 9000,
                },
                Ct
            );

            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.Value.MaxPages);
            Assert.Equal(0, result.Value.MaxDetails);
            Assert.Equal(5000, result.Value.MinDelayMs);
            Assert.Equal(9000, result.Value.MaxDelayMs);
        }

        [Fact]
        public async Task RemoteOnlyNeedsNoLocation()
        {
            var (handler, _) = Create();

            var result = await handler.HandleAsync(
                Valid() with
                {
                    Locations = null,
                    RemoteOnly = true,
                },
                Ct
            );

            Assert.True(result.IsSuccess);
            Assert.Empty(result.Value.Locations);
            Assert.True(result.Value.RemoteOnly);
        }

        [Fact]
        public async Task HighRiskSourceStartsDisabled()
        {
            var (handler, _) = Create();

            var result = await handler.HandleAsync(Valid() with { SourceKey = "linkedin" }, Ct);

            Assert.False(result.Value.Enabled);
        }

        [Fact]
        public async Task ReportsEveryInvalidFieldAtOnceAndSavesNothing()
        {
            var (handler, store) = Create();

            var result = await handler.HandleAsync(
                new CreateSearchProfileRequest(
                    " ",
                    "naukri",
                    new string('k', SearchCriteria.MaxKeywordsLength + 1),
                    [],
                    51,
                    0,
                    RemoteOnly: false,
                    MaxPages: 4,
                    MaxJobs: 0,
                    MaxDetails: 61,
                    MinDelayMs: 1000
                ),
                Ct
            );

            var errors = FieldErrors(result);
            Assert.Equal(
                [
                    "experienceYears",
                    "keywords",
                    "locations",
                    "maxDetails",
                    "maxJobs",
                    "maxPages",
                    "minDelayMs",
                    "name",
                    "postedWithinDays",
                ],
                errors.Keys.Order()
            );
            Assert.Contains("naukri policy", errors["maxPages"][0], StringComparison.Ordinal);
            Assert.Empty(store.SearchProfiles);
            Assert.Equal(0, store.Saves);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("monster")]
        [InlineData("indeed")]
        public async Task UnknownOrUnconfiguredSourceIsAFieldError(string? source)
        {
            var (handler, _) = Create();

            var errors = FieldErrors(
                await handler.HandleAsync(Valid() with { SourceKey = source }, Ct)
            );

            Assert.Equal(["sourceKey"], errors.Keys);
        }

        [Fact]
        public async Task TooManyLocationsAndOverlongNameAreRejected()
        {
            var (handler, _) = Create();
            var eleven = Enumerable.Range(1, 11).Select(i => $"City{i}").ToArray();

            var errors = FieldErrors(
                await handler.HandleAsync(
                    Valid() with
                    {
                        Name = new string('n', SearchProfileValidator.MaxNameLength + 1),
                        Locations = eleven,
                    },
                    Ct
                )
            );

            Assert.Equal(["locations", "name"], errors.Keys.Order());
        }

        [Theory]
        [InlineData(5000, 5500)]
        [InlineData(2500, 120_001)]
        public async Task MaxDelayMustCoverPolicyAndMinDelay(int minDelay, int maxDelay)
        {
            var (handler, _) = Create();

            var errors = FieldErrors(
                await handler.HandleAsync(
                    Valid() with
                    {
                        MinDelayMs = minDelay,
                        MaxDelayMs = maxDelay,
                    },
                    Ct
                )
            );

            Assert.Equal(["maxDelayMs"], errors.Keys);
        }

        [Fact]
        public async Task MinDelayAboveDefaultMaxNeedsAnExplicitMax()
        {
            var (handler, _) = Create();

            var errors = FieldErrors(
                await handler.HandleAsync(Valid() with { MinDelayMs = 7000 }, Ct)
            );

            Assert.Equal(["maxDelayMs"], errors.Keys);
        }

        [Fact]
        public async Task MinDelayOverAMinuteIsRejected()
        {
            var (handler, _) = Create();

            var errors = FieldErrors(
                await handler.HandleAsync(
                    Valid() with
                    {
                        MinDelayMs = 60_001,
                        MaxDelayMs = 90_000,
                    },
                    Ct
                )
            );

            Assert.Contains("minDelayMs", errors.Keys);
        }

        [Fact]
        public async Task DuplicateNameIsAConflict()
        {
            var (handler, store) = Create();
            await handler.HandleAsync(Valid(), Ct);

            var result = await handler.HandleAsync(Valid(), Ct);

            Assert.Equal("PROFILE_NAME_TAKEN", result.Error.Code);
            Assert.Equal(ErrorKind.Conflict, result.Error.Kind);
            Assert.Single(store.SearchProfiles);
        }

        [Fact]
        public async Task RequiredSkillsAreNormalizedAndSaved()
        {
            var (handler, _) = Create();

            var result = await handler.HandleAsync(
                Valid() with
                {
                    RequiredSkills = [" C# ", "", "c#", ".NET  Core"],
                },
                Ct
            );

            Assert.True(result.IsSuccess);
            Assert.Equal(["C#", ".NET Core"], result.Value.RequiredSkills);
        }

        [Fact]
        public async Task OmittedRequiredSkillsMeansNoFilter()
        {
            var (handler, _) = Create();

            var result = await handler.HandleAsync(Valid() with { RequiredSkills = null }, Ct);

            Assert.True(result.IsSuccess);
            Assert.Empty(result.Value.RequiredSkills);
        }

        [Fact]
        public async Task TooManyRequiredSkillsAreRejected()
        {
            var (handler, store) = Create();
            var skills = Enumerable
                .Range(1, SearchCriteria.MaxRequiredSkills + 1)
                .Select(i => $"Skill{i}")
                .ToArray();

            var errors = FieldErrors(
                await handler.HandleAsync(Valid() with { RequiredSkills = skills }, Ct)
            );

            Assert.Equal(["requiredSkills"], errors.Keys);
            Assert.Empty(store.SearchProfiles);
        }

        [Fact]
        public async Task GuardsRejectNulls()
        {
            var store = new ScrapeHarness.InMemoryStore();
            var catalog = new SourceSettingsCatalog([]);
            var clock = new FakeTimeProvider();

            Assert.Throws<ArgumentNullException>(() =>
                new CreateSearchProfileHandler(null!, store, catalog, clock)
            );
            Assert.Throws<ArgumentNullException>(() =>
                new CreateSearchProfileHandler(store, null!, catalog, clock)
            );
            Assert.Throws<ArgumentNullException>(() =>
                new CreateSearchProfileHandler(store, store, null!, clock)
            );
            Assert.Throws<ArgumentNullException>(() =>
                new CreateSearchProfileHandler(store, store, catalog, null!)
            );
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                Create().Handler.HandleAsync(null!, Ct)
            );
            Assert.Throws<ArgumentNullException>(() => Errors.ProfileNameTaken(null!));
            Assert.Throws<ArgumentNullException>(() => SearchProfileResponse.From(null!));
            Assert.Throws<ArgumentNullException>(() => new ListSearchProfilesHandler(null!));
        }

        [Fact]
        public async Task ListWrapsTheStoreItemsWithTheirCount()
        {
            var (handler, _) = Create();
            var created = (await handler.HandleAsync(Valid(), Ct)).Value;

            var list = await new ListSearchProfilesHandler(
                new OneProfileStore(created)
            ).HandleAsync(Ct);

            Assert.Equal(1, list.Total);
            Assert.Same(created, Assert.Single(list.Items));
        }

        private sealed class OneProfileStore(SearchProfileResponse profile) : ISearchProfileStore
        {
            public Task<SearchProfileResponse?> GetAsync(
                Guid id,
                CancellationToken cancellationToken
            )
            {
                return Task.FromResult<SearchProfileResponse?>(profile);
            }

            public Task<IReadOnlyList<SearchProfileResponse>> ListAsync(
                CancellationToken cancellationToken
            )
            {
                return Task.FromResult<IReadOnlyList<SearchProfileResponse>>([profile]);
            }
        }
    }
}
