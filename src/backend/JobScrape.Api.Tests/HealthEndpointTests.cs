using System.Net;
using System.Net.Http.Json;
using JobScrape.Api;
using JobScrape.Api.Controllers;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace JobScrape.Api.Tests
{
    public sealed class HealthEndpointTests : IClassFixture<ApiFactory>
    {
        private readonly ApiFactory _factory;

        public HealthEndpointTests(ApiFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public void RejectsNullHandler()
        {
            Assert.Throws<ArgumentNullException>(() => new HealthController(null!));
        }

        [Fact]
        public async Task GetReturnsHealthyPayload()
        {
            using var client = _factory.CreateClient();

            using var response = await client.GetAsync(
                "api/v1/health",
                TestContext.Current.CancellationToken
            );

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var payload = await response.Content.ReadFromJsonAsync<HealthPayload>(
                TestContext.Current.CancellationToken
            );
            Assert.NotNull(payload);
            Assert.Equal("healthy", payload.Status);
            Assert.True(payload.TimeUtc > DateTimeOffset.MinValue);
        }

        private sealed record HealthPayload(string Status, DateTimeOffset TimeUtc);
    }
}
