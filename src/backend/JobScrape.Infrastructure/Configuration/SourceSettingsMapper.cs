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
                options.Sources.Select(entry => new SourceSettings(
                    SourceKey.Parse(entry.Key),
                    entry.Value.Enabled,
                    entry.Value.DailyPageCap,
                    entry.Value.DailyDetailCap,
                    entry.Value.DailyRunCap,
                    TimeSpan.FromMinutes(entry.Value.ChallengeCooldownMinutes)
                ))
            );
        }
    }
}
