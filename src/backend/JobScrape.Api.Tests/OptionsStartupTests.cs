using JobScrape.Application.Features.Scraping;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace JobScrape.Api.Tests
{
    public sealed class OptionsStartupTests : IClassFixture<ApiFactory>
    {
        private readonly ApiFactory _factory;

        public OptionsStartupTests(ApiFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public void ShippedConfigurationStartsAndBuildsTheCatalog()
        {
            using var client = _factory.CreateClient();
            var services = _factory.Services;

            var catalog = services.GetRequiredService<SourceSettingsCatalog>();
            Assert.True(catalog.Find(SourceKey.Naukri)!.Enabled);
            Assert.False(catalog.Find(SourceKey.Indeed)!.Enabled);
            Assert.False(catalog.Find(SourceKey.LinkedIn)!.Enabled);
            Assert.Equal(
                TimeSpan.FromMinutes(45),
                catalog.Find(SourceKey.Naukri)!.ChallengeCooldown
            );
            Assert.False(
                services
                    .GetRequiredService<RetryPolicy>()
                    .ShouldRetry(ChallengeKind.TransientError, 2)
            );
        }

        [Fact]
        public void ShippedPlaceholderSecretFailsStartupWithoutOverride()
        {
            using var factory = new ApiFactory { UseShippedJwtSecret = true };

            var error = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

            Assert.NotEmpty(error.Failures);
        }

        [Theory]
        [InlineData("JobScrape:Sources:linkedin:Enabled", "true")]
        [InlineData("JobScrape:Sources:indeed:Enabled", "true")]
        [InlineData("JobScrape:Sources:naukri:MaxPagesPerSearch", "0")]
        [InlineData("JobScrape:Sources:naukri:MinDelayMs", "500")]
        [InlineData("JobScrape:Storage:MinimumFreeDiskBytes", "0")]
        [InlineData("JobScrape:Run:RetryMaxAttempts", "9")]
        [InlineData("JobScrape:Logging:RetentionDays", "0")]
        [InlineData("JobScrape:Logging:PlainTextRetentionDays", "0")]
        [InlineData("JobScrape:Logging:PlainTextFileSizeLimitBytes", "1024")]
        [InlineData("JobScrape:Logging:PlainTextRollingInterval", "99")]
        [InlineData("JobScrape:Jwt:Secret", "too-short")]
        [InlineData("JobScrape:Jwt:Secret", "CHANGE-ME-TO-A-SECRET-OF-AT-LEAST-32-CHARS")]
        public void UnsafeConfigurationFailsStartup(string key, string value)
        {
            using var factory = _factory.WithWebHostBuilder(builder =>
                builder.UseSetting(key, value)
            );

            var error = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

            Assert.NotEmpty(error.Failures);
        }
    }
}
