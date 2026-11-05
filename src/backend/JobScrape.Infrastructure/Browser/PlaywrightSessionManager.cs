using JobScrape.Application.Abstractions;
using JobScrape.Domain.Abstractions;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;
using JobScrape.Infrastructure.Sources.Naukri;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace JobScrape.Infrastructure.Browser
{
    /// <summary>
    /// Opens a profile's persistent browser context and warms it up the way
    /// a person would: the source's home page, a pause, one scroll
    /// (docs/08-site-playbooks.md §3.2). A clean home page verifies the
    /// session; a wall is reported for the run handler to act on. Never
    /// types credentials: login stays manual.
    /// </summary>
    public sealed partial class PlaywrightSessionManager : ISessionManager
    {
        // All injected and never mutated; scoped with the run's unit of work,
        // so a verified profile is saved with the run.
        private readonly IBrowserProfileRepository _profiles;
        private readonly IBrowserSessionPool _pool;
        private readonly IDelay _delay;
        private readonly IRandomSource _random;
        private readonly TimeProvider _clock;
        private readonly ILogger<PlaywrightSessionManager> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaywrightSessionManager"/> class.
        /// </summary>
        /// <param name="profiles">Loads the browser profile (tracked by the run's unit of work).</param>
        /// <param name="pool">Owns the browser contexts.</param>
        /// <param name="delay">Waits between warm-up steps.</param>
        /// <param name="random">Jitters the warm-up pause.</param>
        /// <param name="clock">The time source.</param>
        /// <param name="logger">The logger.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public PlaywrightSessionManager(
            IBrowserProfileRepository profiles,
            IBrowserSessionPool pool,
            IDelay delay,
            IRandomSource random,
            TimeProvider clock,
            ILogger<PlaywrightSessionManager> logger
        )
        {
            ArgumentNullException.ThrowIfNull(profiles);
            ArgumentNullException.ThrowIfNull(pool);
            ArgumentNullException.ThrowIfNull(delay);
            ArgumentNullException.ThrowIfNull(random);
            ArgumentNullException.ThrowIfNull(clock);
            ArgumentNullException.ThrowIfNull(logger);
            _profiles = profiles;
            _pool = pool;
            _delay = delay;
            _random = random;
            _clock = clock;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<ChallengeAssessment> EnsureSessionAsync(
            SourceKey source,
            BrowserProfileId profileId,
            CancellationToken cancellationToken
        )
        {
            ArgumentNullException.ThrowIfNull(source);

            // The run handler loaded and gated this profile in the same scope.
            var profile =
                await _profiles.GetAsync(profileId, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Browser profile '{profileId}' disappeared during the run."
                );
            var page = await _pool.OpenAsync(profile, cancellationToken);
            if (source != SourceKey.Naukri)
            {
                // No adapter exists for other sources, so the handler never
                // reaches here for them; there is no home page to verify.
                return ChallengeAssessment.Clear;
            }

            ChallengeAssessment assessment;
            try
            {
                var response = await page.GotoAsync(
                    "https://www.naukri.com/",
                    new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded }
                );
                await _delay.WaitAsync(
                    TimeSpan.FromMilliseconds(_random.NextInt32(1500, 3000)),
                    cancellationToken
                );
                await page.Mouse.WheelAsync(0, 600);
                // The home page has no job cards by design, so only the
                // URL, status, title, and wall-text signals apply.
                assessment = NaukriChallengeClassifier.Classify(
                    new Uri(page.Url),
                    response?.Status,
                    await page.TitleAsync(),
                    await PageText.BodyAsync(page),
                    hasJobCards: true
                );
            }
            catch (PlaywrightException ex)
            {
                LogWarmUpFailed(_logger, ex, source.Value);
                return new ChallengeAssessment(
                    ChallengeKind.TransientError,
                    0.5,
                    "Home page did not load."
                );
            }
            catch (TimeoutException ex)
            {
                LogWarmUpFailed(_logger, ex, source.Value);
                return new ChallengeAssessment(
                    ChallengeKind.TransientError,
                    0.5,
                    "Home page timed out."
                );
            }

            if (assessment.Kind == ChallengeKind.None)
            {
                profile.MarkVerified(_clock.GetUtcNow());
            }

            return assessment;
        }

        [LoggerMessage(Level = LogLevel.Warning, Message = "Session warm-up for {Source} failed")]
        private static partial void LogWarmUpFailed(
            ILogger logger,
            Exception exception,
            string source
        );
    }
}
