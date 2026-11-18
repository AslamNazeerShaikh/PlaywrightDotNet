using JobScrape.Application.Features.Scraping;
using JobScrape.Domain.Common;

namespace JobScrape.Infrastructure.Configuration
{
    /// <summary>
    /// Turns validated <see cref="SourcesOptions"/> into the plain
    /// <see cref="SourceSettingsCatalog"/> the application layer consumes,
    /// so no handler reads configuration.
    /// </summary>
    public static class SourceSettingsMapper
    {
        /// <summary>
        /// Maps every configured source.
        /// </summary>
        /// <param name="options">Options already accepted by <see cref="SourcesOptionsValidator"/>.</param>
        /// <returns>The catalog.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
        public static SourceSettingsCatalog ToCatalog(SourcesOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            return new SourceSettingsCatalog(
                options.Sources.Select(entry =>
                {
                    var source = SourceKey.Parse(entry.Key);
                    return new SourceSettings(
                        source,
                        entry.Value.Enabled,
                        entry.Value.DailyPageCap,
                        entry.Value.DailyDetailCap,
                        entry.Value.UnlimitedRunCap ? null : entry.Value.DailyRunCap,
                        entry.Value.MaxPagesCeiling ?? entry.Value.MaxPagesPerSearch,
                        entry.Value.MaxJobsCeiling ?? entry.Value.MaxJobsPerRun,
                        TimeSpan.FromMinutes(entry.Value.ChallengeCooldownMinutes),
                        SourcesOptionsValidator.ToRunLimits(entry.Value),
                        IsApiOnly(source, entry.Value)
                    );
                })
            );
        }

        /// <summary>
        /// Whether the source's runs fetch through its API: only Naukri has
        /// an API fast path, and only when its own flag is on. The shared
        /// options class defaults <see cref="SourceOptions.ApiOnly"/> to true,
        /// so a generic read would misroute Indeed and LinkedIn.
        /// </summary>
        /// <param name="source">The source key parsed from the section name.</param>
        /// <param name="options">That source's options. Must not be null.</param>
        /// <returns>True when the source needs no browser session.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
        internal static bool IsApiOnly(SourceKey source, SourceOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            return source == SourceKey.Naukri && options.ApiOnly;
        }
    }
}
