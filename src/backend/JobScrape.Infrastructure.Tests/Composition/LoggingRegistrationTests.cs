using JobScrape.Infrastructure.Composition;
using JobScrape.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace JobScrape.Infrastructure.Tests.Composition
{
    public sealed class LoggingRegistrationTests
    {
        private static readonly Action<ILogger, Exception?> ProbeMarker = LoggerMessage.Define(
            LogLevel.Information,
            new EventId(1, "Probe"),
            "probe-marker"
        );

        [Fact]
        public void WritesBothSinksFromOneEvent()
        {
            var directory = Path.Combine(Path.GetTempPath(), $"jobscrape-logs-{Guid.NewGuid():N}");
            try
            {
                var services = new ServiceCollection()
                    .AddLogging()
                    .AddSingleton(Options.Create(new LoggingOptions { Directory = directory }))
                    .AddJobScrapeLogging(
                        new ConfigurationBuilder().Build(),
                        contentRoot: directory,
                        logFilePrefix: "probe"
                    );

                using (var provider = services.BuildServiceProvider())
                {
                    var logger = provider
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger("Probe");
                    ProbeMarker(logger, null);
                }

                var logFile = Assert.Single(Directory.GetFiles(directory, "probe-*.log"));
                Assert.Matches(@"^probe-\d{10}\.log$", Path.GetFileName(logFile));
                var line = Assert.Single(File.ReadAllLines(logFile));
                Assert.Matches(
                    @"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} [+-]\d{2}:\d{2} \[INF\] \[Probe\] probe-marker$",
                    line
                );

                var jsonlFile = Assert.Single(Directory.GetFiles(directory, "probe-*.jsonl"));
                Assert.Matches(@"^probe-\d{8}\.jsonl$", Path.GetFileName(jsonlFile));
                var jsonLine = Assert.Single(File.ReadAllLines(jsonlFile));
                Assert.Contains("\"@mt\":\"probe-marker\"", jsonLine, StringComparison.Ordinal);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
        }
    }
}
