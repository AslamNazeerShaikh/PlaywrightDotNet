using JobScrape.Application.Features.Scraping;
using JobScrape.Application.Results;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;

namespace JobScrape.Application.Features.Runs
{
    /// <summary>
    /// Starts a run in the background under the configured compliance
    /// consent. Every gate (profile, source, consent, circuit, session,
    /// quota) is <see cref="RunScrapeHandler"/>'s; this handler only turns
    /// the request into a run and reports the recorded run.
    /// </summary>
    public sealed class StartRunHandler
    {
        // All injected and never mutated.
        private readonly IRunLauncher _launcher;
        private readonly IRunStore _store;
        private readonly ComplianceConsent _consent;

        /// <summary>
        /// Initializes a new instance of the <see cref="StartRunHandler"/> class.
        /// </summary>
        /// <param name="launcher">Executes runs in the background.</param>
        /// <param name="store">Reads the recorded run back.</param>
        /// <param name="consent">The operator's configured compliance consent.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public StartRunHandler(IRunLauncher launcher, IRunStore store, ComplianceConsent consent)
        {
            ArgumentNullException.ThrowIfNull(launcher);
            ArgumentNullException.ThrowIfNull(store);
            ArgumentNullException.ThrowIfNull(consent);
            _launcher = launcher;
            _store = store;
            _consent = consent;
        }

        /// <summary>
        /// Starts the run and returns it as recorded.
        /// </summary>
        /// <param name="request">The request. Must not be null.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The started run; or the gate's error.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
        public async Task<Result<RunResponse>> HandleAsync(
            StartRunRequest request,
            CancellationToken cancellationToken
        )
        {
            ArgumentNullException.ThrowIfNull(request);

            // Id factories reject empty ids, so they are field errors here.
            var errors = new Dictionary<string, string[]>();
            if (request.ProfileId == Guid.Empty)
            {
                errors["profileId"] = ["Choose a search profile."];
            }

            if (request.BrowserProfileId == Guid.Empty)
            {
                errors["browserProfileId"] = ["Choose a session for the profile's source."];
            }

            if (errors.Count > 0)
            {
                return Result.Fail<RunResponse>(
                    Errors.ValidationFailed("The request contains invalid fields.", errors)
                );
            }

            var started = await _launcher.StartAsync(
                new RunScrapeRequest(
                    SearchProfileId.From(request.ProfileId),
                    BrowserProfileId.From(request.BrowserProfileId),
                    _consent,
                    request.Enrich
                ),
                cancellationToken
            );
            if (started.IsFailure)
            {
                return Result.Fail<RunResponse>(started.Error);
            }

            // The launcher returns after the run's first save, so it exists.
            var run = await _store.GetAsync(started.Value, cancellationToken);
            return run is null
                ? Result.Fail<RunResponse>(Errors.RunNotFound(started.Value))
                : Result.Ok(run);
        }
    }
}
