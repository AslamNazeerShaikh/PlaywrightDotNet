using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JobScrape.Api;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace JobScrape.Api.Tests
{
    public sealed class FeaturesEndpointTests : IClassFixture<ApiFactory>
    {
        private static readonly string[] ExpectedKeys =
        [
            "authentication",
            "csvExport",
            "excelExport",
            "health",
            "logging",
        ];

        private readonly ApiFactory _factory;

        public FeaturesEndpointTests(ApiFactory factory)
        {
            _factory = factory;
        }

        // The brief names these tests with underscores; CA1707 would fail the
        // build, so the rule is suppressed on the two methods, not repo-wide.
        [Fact]
        [SuppressMessage(
            "Naming",
            "CA1707:Identifiers should not contain underscores",
            Justification = "Test names fixed by the feature-flags-foundation task brief."
        )]
        public async Task FeaturesEndpoint_ReturnsShippedDefaults()
        {
            using var client = _factory.CreateClient();

            using var response = await client.GetAsync(
                "api/v1/features",
                TestContext.Current.CancellationToken
            );

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var raw = await response.Content.ReadAsStringAsync(
                TestContext.Current.CancellationToken
            );
            using var document = JsonDocument.Parse(raw);
            var keys = document
                .RootElement.EnumerateObject()
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(ExpectedKeys, keys);
            var payload = await response.Content.ReadFromJsonAsync<FeaturesPayload>(
                TestContext.Current.CancellationToken
            );
            Assert.NotNull(payload);
            Assert.True(payload.Authentication);
            Assert.True(payload.Logging);
            Assert.True(payload.Health);
            Assert.True(payload.CsvExport);
            Assert.True(payload.ExcelExport);
        }

        [Fact]
        [SuppressMessage(
            "Naming",
            "CA1707:Identifiers should not contain underscores",
            Justification = "Test names fixed by the feature-flags-foundation task brief."
        )]
        public async Task FeaturesEndpoint_ReflectsOverride()
        {
            using var factory = _factory.WithWebHostBuilder(builder =>
                builder.UseSetting("JobScrape:Features:Health", "false")
            );
            using var client = factory.CreateClient();

            using var response = await client.GetAsync(
                "api/v1/features",
                TestContext.Current.CancellationToken
            );

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var payload = await response.Content.ReadFromJsonAsync<FeaturesPayload>(
                TestContext.Current.CancellationToken
            );
            Assert.NotNull(payload);
            Assert.False(payload.Health);
            Assert.True(payload.Authentication);
            Assert.True(payload.Logging);
            Assert.True(payload.CsvExport);
            Assert.True(payload.ExcelExport);
        }

        private sealed record FeaturesPayload(
            bool Authentication,
            bool Logging,
            bool Health,
            bool CsvExport,
            bool ExcelExport
        );
    }
}
