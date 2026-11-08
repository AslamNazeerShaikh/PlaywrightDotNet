using System.Net;
using JobScrape.Api;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace JobScrape.Api.Tests
{
    public sealed class FeatureFlagBootTests : IClassFixture<ApiFactory>
    {
        private readonly ApiFactory _factory;

        public FeatureFlagBootTests(ApiFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task BootSucceedsWithLoggingOffAndEmptyDirectory()
        {
            using var factory = _factory.WithWebHostBuilder(builder =>
            {
                builder.UseSetting("JobScrape:Features:Logging", "false");
                builder.UseSetting("JobScrape:Logging:Directory", string.Empty);
            });
            using var client = factory.CreateClient();

            using var response = await client.GetAsync(
                "api/v1/features",
                TestContext.Current.CancellationToken
            );

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public void BootFailsWithInvalidBoolFeatureValue()
        {
            using var factory = _factory.WithWebHostBuilder(builder =>
                builder.UseSetting("JobScrape:Features:Health", "notabool")
            );

            Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        }
    }
}
