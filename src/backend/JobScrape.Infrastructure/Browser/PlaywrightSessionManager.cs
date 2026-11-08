using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.Scraping;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;

namespace JobScrape.Infrastructure.Browser
{
    /// <summary>
    /// Opens a profile's persistent browser context and delegates warm-up
    /// to the source's adapter (<see cref="ISourceWarmUpVerifier"/>): the
    /// adapter navigates its home page the way a person would, or declines
    /// to verify. A clean home page verifies the session; a wall is
    /// reported for the run handler to act on. Never types credentials:
    /// login stays manual.
    /// </summary>
    public sealed class PlaywrightSessionManager : ISessionManager
    {
        // All injected and never mutated; scoped with the run's unit of work,
        // so a verified profile is saved with the run.
        private readonly IBrowserProfileRepository _profiles;
        private readonly IBrowserSessionPool _pool;
        private readonly JobSourceRegistry _sources;
        private readonly TimeProvider _clock;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaywrightSessionManager"/> class.
        /// </summary>
        /// <param name="profiles">Loads the browser profile (tracked by the run's unit of work).</param>
        /// <param name="pool">Owns the browser contexts.</param>
        /// <param name="sources">Resolves the source adapter that owns warm-up.</param>
        /// <param name="clock">The time source.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public PlaywrightSessionManager(
            IBrowserProfileRepository profiles,
            IBrowserSessionPool pool,
            JobSourceRegistry sources,
            TimeProvider clock
        )
        {
            ArgumentNullException.ThrowIfNull(profiles);
            ArgumentNullException.ThrowIfNull(pool);
            ArgumentNullException.ThrowIfNull(sources);
            ArgumentNullException.ThrowIfNull(clock);
            _profiles = profiles;
            _pool = pool;
            _sources = sources;
            _clock = clock;
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

            // The source adapter owns its warm-up; an adapter without one
            // (or an unregistered source) reports ready without verifying.
            var assessment = _sources.Find(source) is ISourceWarmUpVerifier verifier
                ? await verifier.VerifyAsync(page, cancellationToken)
                : null;
            if (assessment is null)
            {
                return ChallengeAssessment.Clear;
            }

            if (assessment.Kind == ChallengeKind.None)
            {
                profile.MarkVerified(_clock.GetUtcNow());
            }

            return assessment;
        }
    }
}
