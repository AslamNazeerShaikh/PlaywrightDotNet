using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.Health;
using JobScrape.Mcp.Tools;
using ModelContextProtocol;
using Xunit;

namespace JobScrape.Mcp.Tests
{
    public sealed class HealthToolsTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private static HealthTools Tools()
        {
            return new HealthTools(new GetHealthHandler(TimeProvider.System), new FakeCaller());
        }

        [Fact]
        public async Task ReturnsHealthyPayload()
        {
            var response = await Tools().GetHealthAsync(Ct);

            Assert.Equal("healthy", response.Status);
        }

        [Fact]
        public void RejectsNulls()
        {
            var caller = new FakeCaller();
            Assert.Throws<ArgumentNullException>(() => new HealthTools(null!, caller));
            Assert.Throws<ArgumentNullException>(() =>
                new HealthTools(new GetHealthHandler(TimeProvider.System), null!)
            );
        }

        private sealed class FakeCaller : ICallerContext
        {
            public string ActorId => "tests";

            public IReadOnlyCollection<string> Roles => [];
        }
    }
}
