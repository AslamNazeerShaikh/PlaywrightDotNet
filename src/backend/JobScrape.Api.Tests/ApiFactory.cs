using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace JobScrape.Api.Tests
{
    /// <summary>
    /// The API under test with its log files in a private temp directory, so
    /// test runs never write into the project tree.
    /// </summary>
    public sealed class ApiFactory : WebApplicationFactory<Program>
    {
        public string LogDirectory { get; } =
            Path.Combine(Path.GetTempPath(), $"jobscrape-api-logs-{Guid.NewGuid():N}");

        public bool DeleteLogsOnDispose { get; init; } = true;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("JobScrape:Logging:Directory", LogDirectory);
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            if (DeleteLogsOnDispose && Directory.Exists(LogDirectory))
            {
                Directory.Delete(LogDirectory, recursive: true);
            }
        }
    }
}
