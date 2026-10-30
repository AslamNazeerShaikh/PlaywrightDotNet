using JobScrape.Application.Features.Health;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace JobScrape.Application.Tests
{
    public sealed class GetHealthHandlerTests
    {
        [Fact]
        public async Task ReturnsHealthyWithClockTime()
        {
            var clock = new FakeTimeProvider();
            var fixedTime = new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);
            clock.SetUtcNow(fixedTime);
            var handler = new GetHealthHandler(clock);

            var response = await handler.HandleAsync(TestContext.Current.CancellationToken);

            Assert.Equal("healthy", response.Status);
            Assert.Equal(fixedTime, response.TimeUtc);
        }

        [Fact]
        public void RejectsNullClock()
        {
            Assert.Throws<ArgumentNullException>(() => new GetHealthHandler(null!));
        }
    }
}
