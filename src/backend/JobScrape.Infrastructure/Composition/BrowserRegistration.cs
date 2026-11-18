using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.Scraping;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;
using JobScrape.Infrastructure.Browser;
using JobScrape.Infrastructure.Configuration;
using JobScrape.Infrastructure.Sources.Indeed;
using JobScrape.Infrastructure.Sources.LinkedIn;
using JobScrape.Infrastructure.Sources.Naukri;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JobScrape.Infrastructure.Composition
{
    /// <summary>
    /// Registers the browser layer and the source adapters: one Playwright
    /// pool for the process, a session manager per run scope, wall
    /// artifacts on disk, and the Naukri, Indeed, and LinkedIn adapters.
    /// Naukri's adapter is chosen per its <see cref="SourceOptions.ApiOnly"/>
    /// flag: the API fast path by default, the Playwright path on opt-out.
    /// </summary>
    public static class BrowserRegistration
    {
        /// <summary>
        /// Adds browser options, the session pool, the session manager, the
        /// artifact store, and the source registry.
        /// </summary>
        /// <param name="services">The service collection. Must not be null.</param>
        /// <param name="contentRoot">Relative storage paths resolve against this directory. Must not be null.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public static IServiceCollection AddJobScrapeBrowser(
            this IServiceCollection services,
            string contentRoot
        )
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(contentRoot);

            services
                .AddOptions<BrowserOptions>()
                .BindConfiguration(BrowserOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();
            services.AddSingleton<IValidateOptions<BrowserOptions>>(
                new BrowserOptionsValidator(contentRoot)
            );
            services.AddSingleton(provider =>
            {
                var storage = provider.GetRequiredService<IOptions<StorageOptions>>().Value;
                var browser = provider.GetRequiredService<IOptions<BrowserOptions>>().Value;
                return new BrowserSessionSettings(
                    Path.GetFullPath(storage.ProfilesRoot, contentRoot),
                    Path.GetFullPath(storage.ArtifactsRoot, contentRoot),
                    browser.Headless,
                    TimeSpan.FromSeconds(browser.NavigationTimeoutSeconds),
                    browser.AdblockMode,
                    browser.ExtensionDir is null
                        ? null
                        : Path.GetFullPath(browser.ExtensionDir, contentRoot)
                );
            });
            // Singleton: the container disposes it on shutdown, closing every
            // browser and flushing profile cookies. The pool also serves its
            // port, so consumers depend on the abstraction, not the class.
            services.AddSingleton<IPlaywrightFactory, SystemPlaywrightFactory>();
            services.AddSingleton<IFileSystem, PhysicalFileSystem>();
            services.AddSingleton<BrowserSessionPool>();
            services.AddSingleton<IBrowserSessionPool>(sp =>
                sp.GetRequiredService<BrowserSessionPool>()
            );
            services.AddSingleton<IBrowserProfileWorkspace, BrowserProfileWorkspace>();
            // Scoped with the run's unit of work; the API-only decorator
            // below skips the browser for Naukri when the flag is on.
            services.AddScoped<PlaywrightSessionManager>();
            services.AddScoped<ISessionManager>(provider => new ApiOnlySessionManager(
                provider.GetRequiredService<PlaywrightSessionManager>(),
                provider.GetRequiredService<IOptions<SourcesOptions>>()
            ));
            services.AddSingleton<IArtifactStore, FileArtifactStore>();
            // The API fast path: one search client and one adapter as
            // singletons, both disposed at shutdown. Only Naukri may select
            // the API adapter; Indeed and LinkedIn keep the browser path.
            services.AddSingleton<NaukriApiClient>();
            services.AddSingleton<NaukriJobSource>();
            services.AddSingleton<NaukriApiJobSource>();
            services.AddSingleton<IJobSource>(provider =>
                UsesNaukriApi(provider.GetRequiredService<IOptions<SourcesOptions>>().Value)
                    ? provider.GetRequiredService<NaukriApiJobSource>()
                    : provider.GetRequiredService<NaukriJobSource>()
            );
            // Registers one adapter per source; removing one (e.g. LinkedIn)
            // only removes its registration, never core code. Indeed and
            // LinkedIn stay disabled by default and gated on consent.
            services.AddSingleton<IJobSource, IndeedJobSource>();
            services.AddSingleton<IJobSource, LinkedInJobSource>();
            services.AddSingleton<JobSourceRegistry>();
            return services;
        }

        /// <summary>
        /// Whether the Naukri source is configured to fetch through its API.
        /// The check is scoped to <see cref="SourceKey.Naukri"/>: the shared
        /// options class defaults <see cref="SourceOptions.ApiOnly"/> to true,
        /// so a generic read would misroute Indeed and LinkedIn.
        /// </summary>
        /// <param name="sources">The bound per-source options. Must not be null.</param>
        /// <returns>True when Naukri uses the API path; a missing section keeps the default.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="sources"/> is null.</exception>
        internal static bool UsesNaukriApi(SourcesOptions sources)
        {
            ArgumentNullException.ThrowIfNull(sources);
            return sources.Sources.GetValueOrDefault(SourceKey.Naukri.Value)?.ApiOnly ?? true;
        }
    }

    /// <summary>
    /// Skips the browser session for Naukri when its API path is selected:
    /// the API adapter never drives a page, and the policy bans login, so
    /// opening a persistent context would only waste a launch. Every other
    /// source, and a flag-off Naukri, delegates unchanged.
    /// </summary>
    internal sealed class ApiOnlySessionManager : ISessionManager
    {
        // Both injected and never mutated.
        private readonly ISessionManager _inner;
        private readonly IOptions<SourcesOptions> _sources;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="inner">The browser session manager. Must not be null.</param>
        /// <param name="sources">The bound per-source options. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public ApiOnlySessionManager(ISessionManager inner, IOptions<SourcesOptions> sources)
        {
            ArgumentNullException.ThrowIfNull(inner);
            ArgumentNullException.ThrowIfNull(sources);
            _inner = inner;
            _sources = sources;
        }

        /// <summary>
        /// Reports Naukri ready without touching the browser when the API
        /// path is selected; delegates every other request.
        /// </summary>
        /// <param name="source">The source to verify against.</param>
        /// <param name="profileId">The browser profile to launch.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The session assessment.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
        public Task<ChallengeAssessment> EnsureSessionAsync(
            SourceKey source,
            BrowserProfileId profileId,
            CancellationToken cancellationToken
        )
        {
            ArgumentNullException.ThrowIfNull(source);
            return source == SourceKey.Naukri && BrowserRegistration.UsesNaukriApi(_sources.Value)
                ? Task.FromResult(ChallengeAssessment.Clear)
                : _inner.EnsureSessionAsync(source, profileId, cancellationToken);
        }
    }
}
