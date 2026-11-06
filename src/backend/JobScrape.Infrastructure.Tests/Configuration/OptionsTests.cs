using System.ComponentModel.DataAnnotations;
using JobScrape.Domain.Common;
using JobScrape.Infrastructure.Configuration;
using JobScrape.Infrastructure.Runtime;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace JobScrape.Infrastructure.Tests.Configuration
{
    public sealed class OptionsTests
    {
        private static SourceOptions Naukri(bool enabled = true)
        {
            return new SourceOptions
            {
                Enabled = enabled,
                BaseUrl = "https://www.naukri.com",
                MaxPagesPerSearch = 3,
                MaxJobsPerRun = 60,
                MaxDetailsPerRun = 60,
                MinDelayMs = 2500,
                MaxDelayMs = 6000,
                DailyPageCap = 25,
                DailyDetailCap = 60,
                DailyRunCap = 2,
                ChallengeCooldownMinutes = 45,
            };
        }

        private static ValidateOptionsResult Validate(
            Dictionary<string, SourceOptions> sources,
            bool acknowledge = false,
            bool allowHighRisk = false
        )
        {
            var validator = new SourcesOptionsValidator(
                Options.Create(
                    new ComplianceOptions
                    {
                        AcknowledgeSourceRisk = acknowledge,
                        AllowHighRiskSources = allowHighRisk,
                    }
                )
            );
            return validator.Validate(null, new SourcesOptions { Sources = sources });
        }

        [Fact]
        public void ShippedNaukriDefaultsAreValid()
        {
            Assert.True(Validate(new() { ["naukri"] = Naukri() }).Succeeded);
        }

        [Fact]
        public void UnknownSourceFails()
        {
            var result = Validate(new() { ["monster"] = Naukri() });

            Assert.Contains("Sources:monster: unknown source.", result.Failures!);
        }

        [Fact]
        public void AnnotationViolationsAreReportedPerSource()
        {
            var result = Validate(
                new()
                {
                    ["naukri"] = new SourceOptions
                    {
                        BaseUrl = "not a url",
                        MaxPagesPerSearch = 0,
                        DailyRunCap = -1,
                    },
                }
            );

            Assert.False(result.Succeeded);
            Assert.Equal(3, result.Failures!.Count());
            Assert.All(
                result.Failures!,
                failure => Assert.StartsWith("Sources:naukri:", failure, StringComparison.Ordinal)
            );
        }

        [Fact]
        public void UnlimitedRunCapCannotCoexistWithADailyRunCap()
        {
            var source = Naukri();
            source = new SourceOptions
            {
                Enabled = source.Enabled,
                BaseUrl = source.BaseUrl,
                MaxPagesPerSearch = source.MaxPagesPerSearch,
                MaxJobsPerRun = source.MaxJobsPerRun,
                MaxDetailsPerRun = source.MaxDetailsPerRun,
                MinDelayMs = source.MinDelayMs,
                MaxDelayMs = source.MaxDelayMs,
                DailyPageCap = source.DailyPageCap,
                DailyDetailCap = source.DailyDetailCap,
                DailyRunCap = 2,
                UnlimitedRunCap = true,
                ChallengeCooldownMinutes = source.ChallengeCooldownMinutes,
            };

            var result = Validate(new() { ["naukri"] = source });

            Assert.False(result.Succeeded);
            Assert.Contains(
                "UnlimitedRunCap=true conflicts with DailyRunCap=2",
                result.Failures!.Single(),
                StringComparison.Ordinal
            );
        }

        [Fact]
        public void DelayWindowMustBeValidForTheDomain()
        {
            var result = Validate(
                new()
                {
                    ["naukri"] = new SourceOptions
                    {
                        Enabled = true,
                        BaseUrl = "https://www.naukri.com",
                        MaxPagesPerSearch = 3,
                        MaxJobsPerRun = 60,
                        MinDelayMs = 9000,
                        MaxDelayMs = 6000,
                    },
                }
            );

            Assert.Contains("Sources:naukri: maxDelay must be >= minDelay.", result.Failures!);
        }

        [Fact]
        public void EnabledRestrictedSourceNeedsAcknowledgment()
        {
            var sources = new Dictionary<string, SourceOptions> { ["indeed"] = Naukri() };

            Assert.False(Validate(sources).Succeeded);
            Assert.True(Validate(sources, acknowledge: true).Succeeded);
        }

        [Fact]
        public void EnabledHighRiskSourceNeedsBothFlags()
        {
            var sources = new Dictionary<string, SourceOptions> { ["linkedin"] = Naukri() };

            var none = Validate(sources);
            var ackOnly = Validate(sources, acknowledge: true);

            Assert.Equal(2, none.Failures!.Count());
            Assert.Contains(
                ackOnly.Failures!,
                f => f.Contains("AllowHighRiskSources", StringComparison.Ordinal)
            );
            Assert.True(Validate(sources, acknowledge: true, allowHighRisk: true).Succeeded);
        }

        [Fact]
        public void DisabledRiskySourcesNeedNoConsent()
        {
            Assert.True(
                Validate(
                    new()
                    {
                        ["indeed"] = Naukri(enabled: false),
                        ["linkedin"] = Naukri(enabled: false),
                    }
                ).Succeeded
            );
        }

        [Fact]
        public void ValidatorRejectsNullArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new SourcesOptionsValidator(null!));
            Assert.Throws<ArgumentNullException>(() =>
                new SourcesOptionsValidator(Options.Create(new ComplianceOptions())).Validate(
                    null,
                    null!
                )
            );
            Assert.Throws<ArgumentNullException>(() => SourcesOptionsValidator.ToRunLimits(null!));
            Assert.Throws<ArgumentNullException>(() => SourceSettingsMapper.ToCatalog(null!));
        }

        [Fact]
        public void MapperBuildsTheApplicationCatalog()
        {
            var catalog = SourceSettingsMapper.ToCatalog(
                new SourcesOptions
                {
                    Sources = new(StringComparer.OrdinalIgnoreCase) { ["NAUKRI"] = Naukri() },
                }
            );

            var settings = catalog.Find(SourceKey.Naukri);
            Assert.NotNull(settings);
            Assert.True(settings.Enabled);
            Assert.Equal(25, settings.DailyPageCap);
            Assert.Equal(60, settings.DailyDetailCap);
            Assert.Equal(2, settings.DailyRunCap);
            Assert.Equal(TimeSpan.FromMinutes(45), settings.ChallengeCooldown);
            Assert.Null(catalog.Find(SourceKey.Indeed));
        }

        [Fact]
        public void UnlimitedRunCapMapsToNullAndAnOmittedCapStaysZero()
        {
            var capped = Naukri();
            var unlimited = new SourceOptions
            {
                Enabled = capped.Enabled,
                BaseUrl = capped.BaseUrl,
                MaxPagesPerSearch = capped.MaxPagesPerSearch,
                MaxJobsPerRun = capped.MaxJobsPerRun,
                MaxDetailsPerRun = capped.MaxDetailsPerRun,
                MinDelayMs = capped.MinDelayMs,
                MaxDelayMs = capped.MaxDelayMs,
                DailyPageCap = capped.DailyPageCap,
                DailyDetailCap = capped.DailyDetailCap,
                MaxPagesCeiling = 1000,
                MaxJobsCeiling = 20_000,
                UnlimitedRunCap = true,
                ChallengeCooldownMinutes = capped.ChallengeCooldownMinutes,
            };

            var catalog = SourceSettingsMapper.ToCatalog(
                new SourcesOptions
                {
                    Sources = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["naukri"] = capped,
                        ["indeed"] = unlimited,
                    },
                }
            );

            Assert.Equal(2, catalog.Find(SourceKey.Naukri)!.DailyRunCap);
            Assert.Null(catalog.Find(SourceKey.Indeed)!.DailyRunCap);
            Assert.Equal(3, catalog.Find(SourceKey.Naukri)!.MaxPagesCeiling);
            Assert.Equal(1000, catalog.Find(SourceKey.Indeed)!.MaxPagesCeiling);
            Assert.Equal(20_000, catalog.Find(SourceKey.Indeed)!.MaxJobsCeiling);
            Assert.Equal(0, new SourceOptions().DailyRunCap);
        }

        [Fact]
        public void ToRunLimitsUsesTheConfiguredCaps()
        {
            var limits = SourcesOptionsValidator.ToRunLimits(Naukri());

            Assert.Equal(3, limits.MaxPages);
            Assert.Equal(TimeSpan.FromMilliseconds(2500), limits.MinDelay);
            Assert.Equal(TimeSpan.FromMilliseconds(6000), limits.MaxDelay);
        }

        [Fact]
        public void StorageRunAndLoggingDefaultsAreValidAndBoundsAreEnforced()
        {
            static bool IsValid(object options)
            {
                return Validator.TryValidateObject(
                    options,
                    new ValidationContext(options),
                    null,
                    true
                );
            }

            Assert.True(IsValid(new StorageOptions()));
            Assert.True(IsValid(new RunOptions()));
            Assert.True(IsValid(new LoggingOptions()));
            Assert.False(IsValid(new StorageOptions { MinimumFreeDiskBytes = 0 }));
            Assert.False(IsValid(new StorageOptions { DatabasePath = "" }));
            Assert.False(IsValid(new RunOptions { RetryMaxAttempts = 4 }));
            Assert.False(IsValid(new LoggingOptions { RetentionDays = 0 }));
            Assert.False(IsValid(new LoggingOptions { FileSizeLimitBytes = 1024 }));
        }

        [Fact]
        public void JwtDefaultsAreValidAndShortSecretsAreRejected()
        {
            static bool IsValid(object options)
            {
                return Validator.TryValidateObject(
                    options,
                    new ValidationContext(options),
                    null,
                    true
                );
            }

            Assert.False(IsValid(new JwtOptions()));
            Assert.False(IsValid(new JwtOptions { Secret = "too-short" }));
            Assert.True(
                IsValid(
                    new JwtOptions { Secret = "a-demo-secret-of-at-least-32-characters" }
                )
            );
            Assert.False(
                IsValid(
                    new JwtOptions
                    {
                        Secret = "a-demo-secret-of-at-least-32-characters",
                        TokenLifetimeMinutes = 0,
                    }
                )
            );
        }

        [Fact]
        public void JwtValidatorRejectsTheDemoPlaceholder()
        {
            var validator = new JwtOptionsValidator();

            var result = validator.Validate(
                null,
                new JwtOptions { Secret = JwtOptionsValidator.DemoPlaceholder }
            );

            Assert.False(result.Succeeded);
            Assert.Contains(
                result.Failures!,
                failure => failure.Contains("demo placeholder", StringComparison.OrdinalIgnoreCase)
            );
        }

        [Fact]
        public void JwtValidatorCountsBytesNotCharacters()
        {
            var validator = new JwtOptionsValidator();

            Assert.False(
                validator.Validate(null, new JwtOptions { Secret = new string('a', 31) }).Succeeded
            );
            Assert.True(
                validator.Validate(null, new JwtOptions { Secret = new string('a', 32) }).Succeeded
            );
            Assert.True(
                validator.Validate(null, new JwtOptions { Secret = new string('é', 16) }).Succeeded
            );
            Assert.False(
                validator.Validate(null, new JwtOptions { Secret = new string('é', 15) }).Succeeded
            );
        }

        [Fact]
        public void SystemRandomSourceStaysInRange()
        {
            var random = new SystemRandomSource();

            for (var i = 0; i < 1000; i++)
            {
                Assert.InRange(random.NextInt32(5, 8), 5, 7);
            }
        }

        [Fact]
        public async Task TimeProviderDelayWaitsOnTheInjectedClock()
        {
            var clock = new FakeTimeProvider();
            var delay = new TimeProviderDelay(clock);

            var wait = delay.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken
            );
            Assert.False(wait.IsCompleted);
            clock.Advance(TimeSpan.FromSeconds(5));
            await wait;

            Assert.True(wait.IsCompletedSuccessfully);
        }

        [Fact]
        public async Task TimeProviderDelayHonorsCancellationAndRejectsNegative()
        {
            var delay = new TimeProviderDelay(new FakeTimeProvider());
            using var cts = new CancellationTokenSource();

            var wait = delay.WaitAsync(TimeSpan.FromHours(1), cts.Token);
            await cts.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
            // The guard throws synchronously, before any task exists.
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                _ = delay.WaitAsync(TimeSpan.FromSeconds(-1), CancellationToken.None);
            });
            Assert.Throws<ArgumentNullException>(() => new TimeProviderDelay(null!));
        }
    }
}
