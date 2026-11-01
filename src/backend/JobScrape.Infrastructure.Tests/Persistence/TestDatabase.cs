using JobScrape.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JobScrape.Infrastructure.Tests.Persistence
{
    /// <summary>
    /// A migrated SQLite file unique to one test, wired through the real
    /// persistence registration. Each <see cref="Scope"/> is a fresh unit of
    /// work, so a reload proves the data went through the database.
    /// </summary>
    internal sealed class TestDatabase : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;

        private TestDatabase(string path)
        {
            Path = path;
            ConnectionString = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
            _provider = new ServiceCollection()
                .AddJobScrapePersistence(ConnectionString)
                .BuildServiceProvider(validateScopes: true);
        }

        public string Path { get; }

        public string ConnectionString { get; }

        public static async Task<TestDatabase> CreateAsync(CancellationToken cancellationToken)
        {
            var database = new TestDatabase(
                System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    $"jobscrape-test-{Guid.NewGuid():N}.db"
                )
            );
            await using var scope = database.Scope();
            await scope.Db.Database.MigrateAsync(cancellationToken);
            return database;
        }

        public TestScope Scope()
        {
            return new TestScope(_provider.CreateAsyncScope());
        }

        public async ValueTask DisposeAsync()
        {
            await _provider.DisposeAsync();
            SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-wal", "-shm" })
            {
                File.Delete(Path + suffix);
            }
        }

        internal sealed class TestScope(AsyncServiceScope scope) : IAsyncDisposable
        {
            public JobScrapeDbContext Db => Get<JobScrapeDbContext>();

            public T Get<T>()
                where T : notnull
            {
                return scope.ServiceProvider.GetRequiredService<T>();
            }

            public ValueTask DisposeAsync()
            {
                return scope.DisposeAsync();
            }
        }
    }
}
