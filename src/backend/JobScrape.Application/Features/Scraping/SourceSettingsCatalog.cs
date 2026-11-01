using JobScrape.Domain.Common;

namespace JobScrape.Application.Features.Scraping
{
    /// <summary>
    /// The configured <see cref="SourceSettings"/>, one per source. A source
    /// without settings cannot run: safety caps never fall back to defaults.
    /// </summary>
    public sealed class SourceSettingsCatalog
    {
        // Immutable after construction.
        private readonly Dictionary<SourceKey, SourceSettings> _settings;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="settings">The settings; at most one per source. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a source appears twice.</exception>
        public SourceSettingsCatalog(IEnumerable<SourceSettings> settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            _settings = [];
            foreach (var entry in settings)
            {
                if (!_settings.TryAdd(entry.Source, entry))
                {
                    throw new ArgumentException(
                        $"Source '{entry.Source}' is configured twice.",
                        nameof(settings)
                    );
                }
            }
        }

        /// <summary>
        /// Returns the settings for <paramref name="source"/>, or null when unconfigured.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <returns>The settings, or null.</returns>
        public SourceSettings? Find(SourceKey source)
        {
            ArgumentNullException.ThrowIfNull(source);
            return _settings.GetValueOrDefault(source);
        }
    }
}
