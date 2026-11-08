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
        private readonly string _fallbackRoot = Path.Combine(
            Path.GetTempPath(),
            $"jobscrape-api-{Guid.NewGuid():N}"
        );

        /// <summary>
        /// Gets the storage root backing this factory. Tests that model a
        /// host restart set the same root on a second factory; the SQLite
        /// file (with its quota definitions and token versions) survives.
        /// </summary>
        public string? StorageRoot { get; init; }

        /// <summary>
        /// Gets the effective storage root.
        /// </summary>
        public string Root => StorageRoot ?? _fallbackRoot;

        /// <summary>
        /// Gets whether disposing this factory also deletes its storage
        /// root. Tests that share one root across restarts set this false
        /// and delete the root themselves.
        /// </summary>
        public bool DeleteStorageOnDispose { get; init; } = true;

        public string LogDirectory => Path.Combine(Root, "logs");

        public string DatabasePath => Path.Combine(Root, "data", "jobscrape.db");

        public string ProfilesRoot => Path.Combine(Root, "data", "profiles");

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
            builder.UseSetting("JobScrape:Storage:ArtifactsRoot", Path.Combine(Root, "data"));
            builder.UseSetting("JobScrape:Storage:ProfilesRoot", ProfilesRoot);
            if (TestServices is not null)
            {
                builder.ConfigureTestServices(TestServices);
            }
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            SqliteConnection.ClearAllPools();
            if (!DeleteStorageOnDispose)
            {
                return;
            }

            var data = Path.Combine(Root, "data");
            if (Directory.Exists(data))
            {
                Directory.Delete(data, recursive: true);
            }

            if (DeleteLogsOnDispose && Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
