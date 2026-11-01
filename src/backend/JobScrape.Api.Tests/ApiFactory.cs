using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace JobScrape.Api.Tests
{
    /// <summary>
    /// The API under test with a private SQLite file and log directory, so
    /// test runs never share state or write into the project tree. Runs in
    /// Development, so migrations apply on startup like a local dev run.
    /// </summary>
    public sealed class ApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(),
            $"jobscrape-api-{Guid.NewGuid():N}"
        );

        public string LogDirectory => Path.Combine(_root, "logs");

        public string DatabasePath => Path.Combine(_root, "data", "jobscrape.db");

        public bool DeleteLogsOnDispose { get; init; } = true;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("JobScrape:Logging:Directory", LogDirectory);
            builder.UseSetting("JobScrape:Storage:DatabasePath", DatabasePath);
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            SqliteConnection.ClearAllPools();
            var data = Path.Combine(_root, "data");
            if (Directory.Exists(data))
            {
                Directory.Delete(data, recursive: true);
            }

            if (DeleteLogsOnDispose && Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }
}
