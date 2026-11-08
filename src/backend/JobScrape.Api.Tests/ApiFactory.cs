using JobScrape.Infrastructure.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

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

        public string ProfilesRoot => Path.Combine(_root, "data", "profiles");

        public bool DeleteLogsOnDispose { get; init; } = true;

        /// <summary>
        /// When true, the demo placeholder is injected in place of the
        /// shipped development secret, so startup validation must refuse
        /// the host.
        /// </summary>
        public bool UseShippedJwtSecret { get; init; }

        /// <summary>
        /// Replaces services after the app registered its own (fakes for the
        /// browser-facing ports, for example).
        /// </summary>
        public Action<IServiceCollection>? TestServices { get; init; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            // The shipped appsettings now carries a working development
            // secret; tests run with a disposable signing key, or with the
            // demo placeholder when exercising startup rejection.
            if (!UseShippedJwtSecret)
            {
                builder.UseSetting("JobScrape:Jwt:Secret", AuthTestHelper.TestJwtSecret);
            }
            else
            {
                builder.UseSetting("JobScrape:Jwt:Secret", JwtOptionsValidator.DemoPlaceholder);
            }
            builder.UseSetting("JobScrape:Logging:Directory", LogDirectory);
            builder.UseSetting("JobScrape:Storage:DatabasePath", DatabasePath);
            builder.UseSetting("JobScrape:Storage:ArtifactsRoot", Path.Combine(_root, "data"));
            builder.UseSetting(
                "JobScrape:Storage:ProfilesRoot",
                Path.Combine(_root, "data", "profiles")
            );
            if (TestServices is not null)
            {
                builder.ConfigureTestServices(TestServices);
            }
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
