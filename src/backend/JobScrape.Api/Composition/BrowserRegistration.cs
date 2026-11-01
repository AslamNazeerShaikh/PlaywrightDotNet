using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.Scraping;
using JobScrape.Infrastructure.Browser;
using JobScrape.Infrastructure.Configuration;
using JobScrape.Infrastructure.Sources.Naukri;
using Microsoft.Extensions.Options;

namespace JobScrape.Api.Composition
{
    /// <summary>
    /// Registers the browser layer and the source adapters: one Playwright
    /// pool for the process, a session manager per run scope, wall
    /// artifacts on disk, and the Naukri adapter.
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
            services.AddSingleton(provider =>
            {
                var storage = provider.GetRequiredService<IOptions<StorageOptions>>().Value;
                var browser = provider.GetRequiredService<IOptions<BrowserOptions>>().Value;
                return new BrowserSessionSettings(
                    Path.GetFullPath(storage.ProfilesRoot, contentRoot),
                    Path.GetFullPath(storage.ArtifactsRoot, contentRoot),
                    browser.Headless,
                    TimeSpan.FromSeconds(browser.NavigationTimeoutSeconds)
                );
            });
            // Singleton: the container disposes it on shutdown, closing every
            // browser and flushing profile cookies.
            services.AddSingleton<BrowserSessionPool>();
            services.AddScoped<ISessionManager, PlaywrightSessionManager>();
            services.AddSingleton<IArtifactStore, FileArtifactStore>();
            services.AddSingleton<IJobSource, NaukriJobSource>();
            services.AddSingleton<JobSourceRegistry>();
            return services;
        }
    }
}
