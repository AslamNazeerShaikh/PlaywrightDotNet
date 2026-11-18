using JobScrape.Application.Abstractions;
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
        private readonly ISearchProfileRepository _profiles;
        private readonly SourceSettingsCatalog _sources;

        /// <summary>
        /// Initializes a new instance of the <see cref="StartRunHandler"/> class.
        /// </summary>
        /// <param name="launcher">Executes runs in the background.</param>
        /// <param name="store">Reads the recorded run back.</param>
        /// <param name="consent">The operator's configured compliance consent.</param>
        /// <param name="profiles">Resolves the run's source when no session is named.</param>
        /// <param name="sources">The per-source settings, including the transport.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public StartRunHandler(
            IRunLauncher launcher,
            IRunStore store,
            ComplianceConsent consent,
            ISearchProfileRepository profiles,
            SourceSettingsCatalog sources
        )
        {
            ArgumentNullException.ThrowIfNull(launcher);
            ArgumentNullException.ThrowIfNull(store);
            ArgumentNullException.ThrowIfNull(consent);
            ArgumentNullException.ThrowIfNull(profiles);
            ArgumentNullException.ThrowIfNull(sources);
            _launcher = launcher;
            _store = store;
            _consent = consent;
            _profiles = profiles;
            _sources = sources;
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

            // An API-only source never opens a browser, so its runs name no
            // session. Every other source still requires one.
            var apiOnly =
                request.BrowserProfileId == Guid.Empty
                && request.ProfileId != Guid.Empty
                && await IsApiOnlyAsync(request.ProfileId, cancellationToken);
            if (request.BrowserProfileId == Guid.Empty && !apiOnly)
            {
                errors["browserProfileId"] = ["Choose a session for the profile's source."];
            }

            if (errors.Count > 0)
            {
                return Result.Fail<RunResponse>(RunIds.InvalidFields(errors));
            }

            BrowserProfileId browserProfileId =
                request.BrowserProfileId == Guid.Empty
                    ? default
                    : BrowserProfileId.From(request.BrowserProfileId);
            var started = await _launcher.StartAsync(
                new RunScrapeRequest(
                    SearchProfileId.From(request.ProfileId),
                    browserProfileId,
                    _consent,
                    request.Enrich,
                    request.OnlyNew
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

        /// <summary>
        /// Whether the request's profile runs against an API-only source. An
        /// unknown profile or source answers false, so the session stays
        /// required and the run path reports the missing profile.
        /// </summary>
        /// <param name="profileId">The search profile id; never empty here.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>True when the run needs no browser session.</returns>
        private async Task<bool> IsApiOnlyAsync(Guid profileId, CancellationToken cancellationToken)
        {
            var profile = await _profiles.GetAsync(
                SearchProfileId.From(profileId),
                cancellationToken
            );
            return profile is not null && (_sources.Find(profile.Source)?.ApiOnly ?? false);
        }
    }
}
