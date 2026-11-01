using System.Net;
using System.Net.Http.Json;
using JobScrape.Api;
using JobScrape.Api.Controllers;
using JobScrape.Application.Features.SearchProfiles;
using JobScrape.Infrastructure.SearchProfiles;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace JobScrape.Api.Tests
{
    public sealed class SearchProfilesEndpointTests : IClassFixture<ApiFactory>
    {
        private readonly ApiFactory _factory;

        public SearchProfilesEndpointTests(ApiFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public void RejectsNullHandler()
        {
            Assert.Throws<ArgumentNullException>(() => new SearchProfilesController(null!));
        }

        [Fact]
        public async Task GetReturnsProfileOnHit()
        {
            // Each test gets a fresh server so the singleton in-memory store
            // starts empty; seeding through the resolved singleton keeps the
            // production Program free of test-only hooks.
            await using var factory = new ApiFactory();
            var profile = new SearchProfileResponse(Guid.NewGuid(), "Backend", "naukri");
            Seed(factory, profile);
            using var client = factory.CreateClient();

            using var response = await client.GetAsync(
                $"api/v1/search-profiles/{profile.Id}",
                TestContext.Current.CancellationToken
            );

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var payload = await response.Content.ReadFromJsonAsync<SearchProfileResponse>(
                TestContext.Current.CancellationToken
            );
            Assert.Equal(profile, payload);
        }

        [Fact]
        public async Task GetMissingReturnsNotFoundProblem()
        {
            using var client = _factory.CreateClient();

            using var response = await client.GetAsync(
                $"api/v1/search-profiles/{Guid.NewGuid()}",
                TestContext.Current.CancellationToken
            );

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal(
                "application/problem+json",
                response.Content.Headers.ContentType?.MediaType
            );
            var body = await response.Content.ReadAsStringAsync(
                TestContext.Current.CancellationToken
            );
            Assert.Contains("PROFILE_NOT_FOUND", body);
        }

        [Fact]
        public async Task GetEmptyIdReturnsValidationProblem()
        {
            using var client = _factory.CreateClient();

            using var response = await client.GetAsync(
                $"api/v1/search-profiles/{Guid.Empty}",
                TestContext.Current.CancellationToken
            );

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync(
                TestContext.Current.CancellationToken
            );
            Assert.Contains("VALIDATION_FAILED", body);
        }

        private static void Seed(ApiFactory factory, SearchProfileResponse profile)
        {
            var store = (InMemorySearchProfileStore)
                factory.Services.GetRequiredService<ISearchProfileStore>();
            store.Add(profile);
        }
    }
}
