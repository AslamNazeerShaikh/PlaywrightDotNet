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

        /// <summary>
        /// The only supported adblock mode: uBlock Origin Lite at maximum
        /// filtering.
        /// </summary>
        public const string UbolMax = "ubol-max";

        /// <summary>
        /// Gets the optional adblock mode. Null (the default) launches
        /// without blocking; the only accepted value is <see cref="UbolMax"/>.
        /// </summary>
        [RegularExpression(
            "^ubol-max$",
            ErrorMessage = "Browser:AdblockMode must be null or 'ubol-max'."
        )]
        public string? AdblockMode { get; init; }

        /// <summary>
        /// Gets the unpacked extension directory, resolved against the
        /// content root. Required when <see cref="AdblockMode"/> is set.
        /// </summary>
        public string? ExtensionDir { get; init; }
    }
}
