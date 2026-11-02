using System.Net;
using System.Net.Http.Json;
using JobScrape.Api.Controllers;
using JobScrape.Application.Features.Sources;
using Xunit;

namespace JobScrape.Api.Tests
{
    public sealed class SourcesEndpointTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact]
        public void RejectsNullHandler()
        {
            Assert.Throws<ArgumentNullException>(() => new SourcesController(null!));
        }

        [Fact]
        public async Task ListsTheShippedSourcesWithPolicyGateQuotaAndCircuit()
        {
            await using var factory = new ApiFactory();
            using var client = factory.CreateClient();

            using var response = await client.GetAsync("api/v1/sources", Ct);
            var body = await response.Content.ReadFromJsonAsync<SourceListResponse>(Ct);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(["indeed", "linkedin", "naukri"], body!.Items.Select(s => s.Key));
            var naukri = body.Items[2];
            Assert.Equal("Standard", naukri.Tier);
            Assert.True(naukri.Enabled);
            Assert.True(naukri.Gate.Allowed);
            Assert.Equal(new SourcePolicy(3, 60, 60, 2500, 6000, 45), naukri.Policy);
            Assert.Equal(25, naukri.Quota.PageCap);
            Assert.Equal(0, naukri.Quota.PagesUsed);
            Assert.Null(naukri.Quota.RunCap);
            Assert.Equal("Closed", naukri.Circuit.State);
            var linkedin = body.Items[1];
            Assert.Equal("HighRisk", linkedin.Tier);
            Assert.False(linkedin.Enabled);
            Assert.False(linkedin.Gate.Allowed);
            Assert.NotNull(linkedin.Gate.Reason);
            Assert.Equal(1, linkedin.Quota.RunCap);
            Assert.Equal(1, body.Items[0].Quota.RunCap);
        }

        [Fact]
        public async Task ConfiguredConsentUnlocksTheRestrictedGate()
        {
            await using var factory = new ApiFactory();
            using var configured = factory.WithWebHostBuilder(builder =>
                builder.UseSetting("JobScrape:Compliance:AcknowledgeSourceRisk", "true")
            );
            using var client = configured.CreateClient();

            var body = await client.GetFromJsonAsync<SourceListResponse>("api/v1/sources", Ct);

            Assert.True(body!.Items.Single(s => s.Key == "indeed").Gate.Allowed);
            Assert.False(body.Items.Single(s => s.Key == "linkedin").Gate.Allowed);
        }
    }
}
