using JobScrape.Domain.Common;

namespace JobScrape.Application.Features.Scraping
{
    /// <summary>
    /// Resolves the adapter for a source. Built once from the registered
    /// adapters; removing an adapter (e.g. LinkedIn) only removes its
    /// registration, never core code.
    /// </summary>
    public sealed class JobSourceRegistry
    {
        // Immutable after construction.
        private readonly Dictionary<SourceKey, IJobSource> _sources;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="sources">The adapters; at most one per source. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="sources"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when two adapters claim the same source.</exception>
        public JobSourceRegistry(IEnumerable<IJobSource> sources)
        {
            ArgumentNullException.ThrowIfNull(sources);
            _sources = [];
            foreach (var source in sources)
            {
                if (!_sources.TryAdd(source.Key, source))
                {
                    throw new ArgumentException(
                        $"Two adapters are registered for source '{source.Key}'.",
                        nameof(sources)
                    );
                }
            }
        }

        /// <summary>
        /// Returns the adapter for <paramref name="source"/>, or null when none is registered.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <returns>The adapter, or null.</returns>
        public IJobSource? Find(SourceKey source)
        {
            ArgumentNullException.ThrowIfNull(source);
            return _sources.GetValueOrDefault(source);
        }
    }
}
