using System.ComponentModel.DataAnnotations;

namespace JobScrape.Infrastructure.Configuration
{
    /// <summary>
    /// Browser launch settings, bound from <c>JobScrape:Browser</c>.
    /// </summary>
    public sealed class BrowserOptions
    {
        /// <summary>
        /// The configuration section name.
        /// </summary>
        public const string SectionName = "JobScrape:Browser";

        /// <summary>
        /// Gets a value indicating whether browsers launch headless. Off by
        /// default: Naukri blocks headless first contact (docs/08-site-playbooks.md §3.1),
        /// so a run opens a visible window.
        /// </summary>
        public bool Headless { get; init; }

        /// <summary>
        /// Gets the navigation timeout in seconds.
        /// </summary>
        [Range(5, 120)]
        public int NavigationTimeoutSeconds { get; init; } = 45;
    }
}
