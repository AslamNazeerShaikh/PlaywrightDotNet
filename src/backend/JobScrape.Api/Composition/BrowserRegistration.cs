using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.Scraping;
using JobScrape.Infrastructure.Browser;
using JobScrape.Infrastructure.Configuration;
using JobScrape.Infrastructure.Sources.Indeed;
using JobScrape.Infrastructure.Sources.LinkedIn;
using JobScrape.Infrastructure.Sources.Naukri;
using Microsoft.Extensions.Options;

namespace JobScrape.Api.Composition
{
    /// <summary>
    /// Registers the browser layer and the source adapters: one Playwright
    /// pool for the process, a session manager per run scope, wall
    /// artifacts on disk, and the Naukri, Indeed, and LinkedIn adapters.
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
            services.AddSingleton<IBrowserSessionPool>(sp => sp.GetRequiredService<BrowserSessionPool>());
            services.AddSingleton<IBrowserProfileWorkspace, BrowserProfileWorkspace>();
            services.AddScoped<ISessionManager, PlaywrightSessionManager>();
            services.AddSingleton<IArtifactStore, FileArtifactStore>();
            // Registers one adapter per source; removing one (e.g. LinkedIn)
            // only removes its registration, never core code. Indeed and
            // LinkedIn stay disabled by default and gated on consent.
            services.AddSingleton<IJobSource, NaukriJobSource>();
            services.AddSingleton<IJobSource, IndeedJobSource>();
            services.AddSingleton<IJobSource, LinkedInJobSource>();
            services.AddSingleton<JobSourceRegistry>();
            return services;
        }
    }
}
