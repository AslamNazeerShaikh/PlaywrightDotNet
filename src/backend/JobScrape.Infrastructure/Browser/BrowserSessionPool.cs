using System.Collections.Concurrent;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace JobScrape.Infrastructure.Browser
{
    /// <summary>
    /// Owns Playwright and one persistent browser context per browser
    /// profile: the profile's user-data directory keeps its cookies between
    /// runs. A run opens its profile's context, adapters drive its page, and
    /// the run's host releases it when the run ends. Thread-safe.
    /// </summary>
    public sealed partial class BrowserSessionPool : IAsyncDisposable
    {
        // Settings injected and never mutated. _gate serializes launches
        // (and Playwright's creation); lookups read the map lock-free.
        private readonly BrowserSessionSettings _settings;
        private readonly ILogger<BrowserSessionPool>? _logger;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly ConcurrentDictionary<BrowserProfileId, IBrowserContext> _contexts = [];
        private IPlaywright? _playwright;

        /// <summary>
        /// Initializes a new instance of the <see cref="BrowserSessionPool"/> class.
        /// </summary>
        /// <param name="settings">The resolved browser settings.</param>
        /// <param name="logger">Sink for the missing-extension-dir warning. Null skips logging.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> is null.</exception>
        public BrowserSessionPool(
            BrowserSessionSettings settings,
            ILogger<BrowserSessionPool>? logger = null
        )
        {
            ArgumentNullException.ThrowIfNull(settings);
            _settings = settings;
            _logger = logger;
        }

        /// <summary>
        /// Gets the profile's page, launching its persistent context first
        /// when needed.
        /// </summary>
        /// <param name="profile">The browser profile. Must not be null.</param>
        /// <param name="cancellationToken">Token to observe while waiting for the pool.</param>
        /// <returns>The profile's page.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="profile"/> is null.</exception>
        /// <exception cref="PlaywrightException">Thrown when the browser cannot launch (for example, it is not installed).</exception>
        public async Task<IPage> OpenAsync(
            BrowserProfile profile,
            CancellationToken cancellationToken
        )
        {
            ArgumentNullException.ThrowIfNull(profile);

            await _gate.WaitAsync(cancellationToken);
            try
            {
                if (!_contexts.TryGetValue(profile.Id, out var context))
                {
                    _playwright ??= await Playwright.CreateAsync();
                    // The name is a safe path segment (BrowserProfile.IsValidName),
                    // so the directory stays inside the profiles root.
                    var directory = Path.Combine(
                        _settings.ProfilesRoot,
                        profile.UserDataDirectory.Replace('/', Path.DirectorySeparatorChar)
                    );
                    Directory.CreateDirectory(directory);
                    context = await Engine(_playwright, profile.Engine)
                        .LaunchPersistentContextAsync(
                            directory,
                            new BrowserTypeLaunchPersistentContextOptions
                            {
                                Headless = _settings.Headless,
                                Channel = Channel(profile.Channel),
                                // Real window size and the browser's own user agent:
                                // no spoofing (docs/07-anti-bot-and-compliance.md).
                                ViewportSize = ViewportSize.NoViewport,
                                Args = ExtensionArgs(profile.Engine),
                            }
                        );
                    context.SetDefaultNavigationTimeout(
                        (float)_settings.NavigationTimeout.TotalMilliseconds
                    );
                    _contexts[profile.Id] = context;
                }

                return context.Pages.Count > 0 ? context.Pages[0] : await context.NewPageAsync();
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>
        /// Gets the open page of a profile, if its context is open.
        /// </summary>
        /// <param name="profileId">The browser profile id.</param>
        /// <returns>The page, or null when the profile has no open context.</returns>
        public IPage? Find(BrowserProfileId profileId)
        {
            return _contexts.TryGetValue(profileId, out var context) && context.Pages.Count > 0
                ? context.Pages[0]
                : null;
        }

        /// <summary>
        /// Closes a profile's context, flushing its cookies to disk. A no-op
        /// when the profile has none open.
        /// </summary>
        /// <param name="profileId">The browser profile id.</param>
        /// <returns>A task that completes when the context is closed.</returns>
        public async Task ReleaseAsync(BrowserProfileId profileId)
        {
            if (_contexts.TryRemove(profileId, out var context))
            {
                await context.CloseAsync();
            }
        }

        /// <summary>
        /// Closes every context and stops Playwright.
        /// </summary>
        /// <returns>A task that completes when everything is closed.</returns>
        public async ValueTask DisposeAsync()
        {
            await _gate.WaitAsync(CancellationToken.None);
            try
            {
                foreach (var context in _contexts.Values)
                {
                    await context.CloseAsync();
                }

                _contexts.Clear();
                _playwright?.Dispose();
                _playwright = null;
            }
            finally
            {
                _gate.Release();
            }

            _gate.Dispose();
        }

        /// <summary>
        /// Maps the domain engine to its Playwright browser type.
        /// </summary>
        /// <param name="playwright">The Playwright instance.</param>
        /// <param name="engine">The engine.</param>
        /// <returns>The browser type.</returns>
        private static IBrowserType Engine(IPlaywright playwright, BrowserEngine engine)
        {
            return engine switch
            {
                BrowserEngine.Firefox => playwright.Firefox,
                BrowserEngine.WebKit => playwright.Webkit,
                _ => playwright.Chromium,
            };
        }

        /// <summary>
        /// Maps the domain channel to Playwright's channel name; bundled
        /// browsers have none.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <returns>The channel name, or null for the bundled browser.</returns>
        internal static string? Channel(BrowserChannel channel)
        {
            return channel switch
            {
                BrowserChannel.Chrome => "chrome",
                BrowserChannel.Edge => "msedge",
                _ => null,
            };
        }

        /// <summary>
        /// Gets the extension flags for a launch, or null to leave the
        /// launch unchanged. Only headed Chromium with a configured mode
        /// and an existing directory gets flags; a missing directory logs
        /// and launches without the blocker.
        /// </summary>
        /// <param name="engine">The engine about to launch.</param>
        /// <returns>The flags, or null when the blocker stays off.</returns>
        public string[]? ExtensionArgs(BrowserEngine engine)
        {
            if (
                engine != BrowserEngine.Chromium
                || _settings.Headless
                || _settings.AdblockMode is null
                || _settings.ExtensionDir is null
            )
            {
                return null;
            }

            if (!Directory.Exists(_settings.ExtensionDir))
            {
                if (_logger is not null)
                {
                    LogExtensionDirMissing(_logger, _settings.ExtensionDir);
                }

                return null;
            }

            return
            [
                $"--disable-extensions-except={_settings.ExtensionDir}",
                $"--load-extension={_settings.ExtensionDir}",
            ];
        }

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Adblock extension directory {ExtensionDir} is missing; launching without the blocker. Run vendor/download-ubol.sh to fetch it."
        )]
        private static partial void LogExtensionDirMissing(
            ILogger logger,
            string? extensionDir
        );
    }
}
