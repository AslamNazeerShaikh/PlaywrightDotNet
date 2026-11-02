using System.Globalization;
using System.Text;
using System.Text.Json;
using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.Scraping;
using JobScrape.Domain.Access;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace JobScrape.Infrastructure.Browser
{
    /// <summary>
    /// Captures what a wall looked like, for a person to inspect: a
    /// screenshot, the page HTML (capped), and the assessment, under
    /// <c>{artifacts}/runs/{runId}/{time}-{kind}/</c>. Capture is best effort:
    /// a failed screenshot never fails the run.
    /// </summary>
    public sealed partial class FileArtifactStore : IArtifactStore
    {
        /// <summary>
        /// Most HTML bytes kept per capture.
        /// </summary>
        public const int MaxHtmlChars = 2_000_000;

        // All injected and never mutated.
        private readonly BrowserSessionPool _pool;
        private readonly BrowserSessionSettings _settings;
        private readonly TimeProvider _clock;
        private readonly ILogger<FileArtifactStore> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="FileArtifactStore"/> class.
        /// </summary>
        /// <param name="pool">Finds the run's page.</param>
        /// <param name="settings">Provides the artifacts root.</param>
        /// <param name="clock">Stamps the capture directory.</param>
        /// <param name="logger">The logger.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public FileArtifactStore(
            BrowserSessionPool pool,
            BrowserSessionSettings settings,
            TimeProvider clock,
            ILogger<FileArtifactStore> logger
        )
        {
            ArgumentNullException.ThrowIfNull(pool);
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(clock);
            ArgumentNullException.ThrowIfNull(logger);
            _pool = pool;
            _settings = settings;
            _clock = clock;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<string> CaptureAsync(
            SourceRunContext context,
            ChallengeAssessment assessment,
            CancellationToken cancellationToken
        )
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(assessment);

            var relative = Path.Combine(
                "runs",
                context.RunId.Value.ToString(),
                $"{_clock.GetUtcNow().ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture)}-{assessment.Kind}"
            );
            var directory = Path.Combine(_settings.ArtifactsRoot, relative);
            Directory.CreateDirectory(directory);

            var page = _pool.Find(context.BrowserProfileId);
            await File.WriteAllTextAsync(
                Path.Combine(directory, "assessment.json"),
                JsonSerializer.Serialize(
                    new
                    {
                        kind = assessment.Kind.ToString(),
                        confidence = assessment.Confidence,
                        evidence = assessment.Evidence,
                        url = page?.Url,
                    }
                ),
                Encoding.UTF8,
                cancellationToken
            );

            if (page is not null)
            {
                try
                {
                    await page.ScreenshotAsync(
                        new PageScreenshotOptions
                        {
                            Path = Path.Combine(directory, "screenshot.png"),
                            FullPage = true,
                        }
                    );
                    var html = await page.ContentAsync();
                    await File.WriteAllTextAsync(
                        Path.Combine(directory, "page.html"),
                        html.Length > MaxHtmlChars ? html[..MaxHtmlChars] : html,
                        Encoding.UTF8,
                        cancellationToken
                    );
                }
                catch (PlaywrightException ex)
                {
                    LogCaptureFailed(_logger, ex, context.RunId.Value);
                }
            }

            return relative.Replace(Path.DirectorySeparatorChar, '/');
        }

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Artifact capture for run {RunId} was incomplete"
        )]
        private static partial void LogCaptureFailed(
            ILogger logger,
            Exception exception,
            Guid runId
        );
    }
}
