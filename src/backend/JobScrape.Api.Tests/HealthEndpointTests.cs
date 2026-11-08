using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JobScrape.Api;
using JobScrape.Api.Controllers;
using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.Health;
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

        private static FeatureFlags Flags => new(
            Authentication: true,
            Logging: true,
            Health: true,
            CsvExport: true,
            ExcelExport: true
        );

        [Fact]
        public void RejectsNullHandler()
        {
            Assert.Throws<ArgumentNullException>(() => new HealthController(null!, Flags));
        }

        [Fact]
        public void RejectsNullFlags()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new HealthController(new GetHealthHandler(TimeProvider.System), null!)
            );
        }

        // The brief names the test with underscores; CA1707 would fail the
        // build, so the rule is suppressed on the method, not repo-wide.
        [Fact]
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Naming",
            "CA1707:Identifiers should not contain underscores",
            Justification = "Test names fixed by the feature-flags-foundation task brief."
        )]
        public async Task HealthEndpoint_RespectsFlag()
        {
            using var factory = _factory.WithWebHostBuilder(builder =>
                builder.UseSetting("JobScrape:Features:Health", "false")
            );
            using var client = factory.CreateClient();

            using var response = await client.GetAsync(
                "api/v1/health",
                TestContext.Current.CancellationToken
            );

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            using var body = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(
                    TestContext.Current.CancellationToken
                )
            );
            Assert.Equal(
                "FEATURE_DISABLED",
                body.RootElement.GetProperty("code").GetString()
            );
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
