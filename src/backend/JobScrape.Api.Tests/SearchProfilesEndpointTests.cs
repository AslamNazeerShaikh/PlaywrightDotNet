using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JobScrape.Api.Controllers;
using JobScrape.Application.Features.SearchProfiles;
using Xunit;

namespace JobScrape.Api.Tests
{
    public sealed class SearchProfilesEndpointTests
    {
        private static readonly string[] Pune = ["Pune"];

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private static object Valid(string name = "naukri-dotnet-pune")
        {
            return new
            {
                name,
                sourceKey = "naukri",
                keywords = "asp.net core c#",
                locations = new[] { "Pune", "Hyderabad" },
                experienceYears = 6,
                postedWithinDays = 7,
                remoteOnly = false,
            };
        }

        [Fact]
        public void RejectsNullHandlers()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new SearchProfilesController(null!, null!, null!)
            );
        }

        [Fact]
        public async Task CreateThenGetAndListRoundTripThroughSqlite()
        {
            await using var factory = new ApiFactory();
            using var client = factory.CreateClient();

            using var empty = await client.GetAsync("api/v1/search-profiles", Ct);
            var none = await empty.Content.ReadFromJsonAsync<SearchProfileListResponse>(Ct);
            Assert.Equal(0, none!.Total);

            using var created = await client.PostAsJsonAsync("api/v1/search-profiles", Valid(), Ct);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var profile = await created.Content.ReadFromJsonAsync<SearchProfileResponse>(Ct);
            Assert.NotNull(profile);
            Assert.Equal(
                $"/api/v1/search-profiles/{profile.Id}",
                created.Headers.Location!.OriginalString
            );
            Assert.Equal("naukri-dotnet-pune", profile.Name);
            Assert.Equal(["Pune", "Hyderabad"], profile.Locations);
            Assert.True(profile.Enabled);

            // Omitted caps default to the shipped naukri policy limits.
            Assert.Equal(3, profile.MaxPages);
            Assert.Equal(60, profile.MaxJobs);
            Assert.Equal(60, profile.MaxDetails);
            Assert.Equal(2500, profile.MinDelayMs);
            Assert.Equal(6000, profile.MaxDelayMs);

            var fetched = await client.GetFromJsonAsync<SearchProfileResponse>(
                $"api/v1/search-profiles/{profile.Id}",
                Ct
            );
            Assert.Equal(profile.Keywords, fetched!.Keywords);

            var list = await client.GetFromJsonAsync<SearchProfileListResponse>(
                "api/v1/search-profiles",
                Ct
            );
            Assert.Equal(profile.Id, Assert.Single(list!.Items).Id);
            Assert.True(File.Exists(factory.DatabasePath));
        }

        [Fact]
        public async Task ListIsOrderedByName()
        {
            await using var factory = new ApiFactory();
            using var client = factory.CreateClient();
            foreach (var name in new[] { "zeta", "alpha", "mid" })
            {
                using var response = await client.PostAsJsonAsync(
                    "api/v1/search-profiles",
                    Valid(name),
                    Ct
                );
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            }

            var list = await client.GetFromJsonAsync<SearchProfileListResponse>(
                "api/v1/search-profiles",
                Ct
            );

            Assert.Equal(["alpha", "mid", "zeta"], list!.Items.Select(p => p.Name));
            Assert.Equal(3, list.Total);
        }

        [Fact]
        public async Task InvalidFieldsReturnValidationProblemKeyedByJsonName()
        {
            await using var factory = new ApiFactory();
            using var client = factory.CreateClient();

            using var response = await client.PostAsJsonAsync(
                "api/v1/search-profiles",
                new
                {
                    name = "",
                    sourceKey = "monster",
                    keywords = " ",
                    locations = Array.Empty<string>(),
                    remoteOnly = false,
                },
                Ct
            );

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
            Assert.Equal("VALIDATION_FAILED", body.RootElement.GetProperty("code").GetString());
            var errors = body.RootElement.GetProperty("errors");
            foreach (var field in new[] { "name", "sourceKey", "keywords", "locations" })
            {
                Assert.True(errors.TryGetProperty(field, out _), field);
            }
        }

        [Fact]
        public async Task CapsAboveThePolicyAreRejected()
        {
            await using var factory = new ApiFactory();
            using var client = factory.CreateClient();

            using var response = await client.PostAsJsonAsync(
                "api/v1/search-profiles",
                new
                {
                    name = "greedy",
                    sourceKey = "naukri",
                    keywords = "c#",
                    locations = Pune,
                    remoteOnly = false,
                    maxPages = 30,
                    minDelayMs = 1000,
                },
                Ct
            );

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var text = await response.Content.ReadAsStringAsync(Ct);
            Assert.Contains("maxPages", text, StringComparison.Ordinal);
            Assert.Contains("minDelayMs", text, StringComparison.Ordinal);
            Assert.Contains("naukri policy", text, StringComparison.Ordinal);
        }

        [Fact]
        public async Task DuplicateNameIsAConflict()
        {
            await using var factory = new ApiFactory();
            using var client = factory.CreateClient();
            using var first = await client.PostAsJsonAsync(
                "api/v1/search-profiles",
                Valid("dup"),
                Ct
            );

            using var second = await client.PostAsJsonAsync(
                "api/v1/search-profiles",
                Valid("dup"),
                Ct
            );

            Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
            Assert.Contains(
                "PROFILE_NAME_TAKEN",
                await second.Content.ReadAsStringAsync(Ct),
                StringComparison.Ordinal
            );
        }

        [Fact]
        public async Task HighRiskSourceProfileStartsDisabled()
        {
            await using var factory = new ApiFactory();
            using var client = factory.CreateClient();

            using var response = await client.PostAsJsonAsync(
                "api/v1/search-profiles",
                new
                {
                    name = "li",
                    sourceKey = "linkedin",
                    keywords = "c#",
                    locations = Pune,
                    remoteOnly = false,
                },
                Ct
            );

            var profile = await response.Content.ReadFromJsonAsync<SearchProfileResponse>(Ct);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.False(profile!.Enabled);
        }

        [Fact]
        public async Task UnknownIdIsNotFoundAndEmptyIdIsInvalid()
        {
            await using var factory = new ApiFactory();
            using var client = factory.CreateClient();

            using var missing = await client.GetAsync(
                $"api/v1/search-profiles/{Guid.NewGuid()}",
                Ct
            );
            using var empty = await client.GetAsync($"api/v1/search-profiles/{Guid.Empty}", Ct);

            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
            Assert.Contains(
                "PROFILE_NOT_FOUND",
                await missing.Content.ReadAsStringAsync(Ct),
                StringComparison.Ordinal
            );
            Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);

            // Regression: field errors must survive MVC's problem writer.
            using var problem = JsonDocument.Parse(await empty.Content.ReadAsStringAsync(Ct));
            Assert.Equal("VALIDATION_FAILED", problem.RootElement.GetProperty("code").GetString());
            Assert.Equal(
                "https://jobscrape.local/problems/validation-failed",
                problem.RootElement.GetProperty("type").GetString()
            );
            Assert.Equal(
                "A profile id is required.",
                problem.RootElement.GetProperty("errors").GetProperty("id")[0].GetString()
            );
        }
    }
}
