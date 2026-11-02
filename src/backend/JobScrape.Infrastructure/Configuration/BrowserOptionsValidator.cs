using Microsoft.Extensions.Options;

namespace JobScrape.Infrastructure.Configuration
{
    /// <summary>
    /// The adblock cross-field rule DataAnnotations cannot express: a set
    /// mode needs an existing unpacked extension directory. Any violation
    /// fails host startup (docs/02-architecture.md §11.2).
    /// </summary>
    public sealed class BrowserOptionsValidator : IValidateOptions<BrowserOptions>
    {
        // Injected and never mutated.
        private readonly string _contentRoot;

        /// <summary>
        /// Initializes a new instance of the <see cref="BrowserOptionsValidator"/> class.
        /// </summary>
        /// <param name="contentRoot">Relative extension paths resolve against this directory. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="contentRoot"/> is null.</exception>
        public BrowserOptionsValidator(string contentRoot)
        {
            ArgumentNullException.ThrowIfNull(contentRoot);
            _contentRoot = contentRoot;
        }

        /// <summary>
        /// Validates the adblock settings.
        /// </summary>
        /// <param name="name">The options instance name (unused).</param>
        /// <param name="options">The bound options.</param>
        /// <returns>Success, or a failure naming the vendor script.</returns>
        public ValidateOptionsResult Validate(string? name, BrowserOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            if (options.AdblockMode is null)
            {
                return ValidateOptionsResult.Success;
            }

            if (options.ExtensionDir is null)
            {
                return ValidateOptionsResult.Fail(
                    "Browser:AdblockMode is set but Browser:ExtensionDir is not. Run vendor/download-ubol.sh to fetch the unpacked extension, then set Browser:ExtensionDir."
                );
            }

            var directory = Path.GetFullPath(options.ExtensionDir, _contentRoot);
            return Directory.Exists(directory)
                ? ValidateOptionsResult.Success
                : ValidateOptionsResult.Fail(
                    $"Browser:ExtensionDir '{directory}' does not exist. Run vendor/download-ubol.sh to fetch the unpacked extension."
                );
        }
    }
}
