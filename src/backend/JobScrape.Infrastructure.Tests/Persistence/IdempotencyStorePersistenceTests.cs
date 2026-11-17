using JobScrape.Application.Abstractions;
using JobScrape.Infrastructure.Persistence;
using JobScrape.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace JobScrape.Infrastructure.Tests.Persistence
{
    [Trait("Category", "Integration")]
    public sealed class IdempotencyStorePersistenceTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact]
        public async Task TryReserveAsyncSecondCallReturnsFalseOnPrimaryKeyViolation()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            await using var scope = database.Scope();
            var store = scope.Get<IIdempotencyStore>();
            var clock = scope.Get<TimeProvider>();

            var entry = new IdempotencyEntry(
                "owner",
                "POST /api/v1/users",
                "key",
                "hash",
                0,
                "application/json",
                [],
                clock.GetUtcNow()
            );

            Assert.True(await store.TryReserveAsync(entry, Ct));
            // Same triple again must be treated as a conflict (PK violation
            // surfaces as SQLite extended code 1555), not throw.
            Assert.False(await store.TryReserveAsync(entry, Ct));
        }

        [Fact]
        public async Task RemoveAsyncCompletesEvenWhenTokenCanceled()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            await using var scope = database.Scope();
            var store = scope.Get<IIdempotencyStore>();
            var clock = scope.Get<TimeProvider>();

            var entry = new IdempotencyEntry(
                "owner",
                "POST /api/v1/users",
                "key2",
                "hash",
                0,
                "application/json",
                [],
                clock.GetUtcNow()
            );
            Assert.True(await store.TryReserveAsync(entry, Ct));

            using var cts = new CancellationTokenSource();
            cts.Cancel();
            await store.RemoveAsync("owner", "POST /api/v1/users", "key2", cts.Token);

            var reloaded = await store.GetAsync("owner", "POST /api/v1/users", "key2", Ct);
            Assert.Null(reloaded);
        }

        [Fact]
        public async Task GetAsyncObservesCommittedUpdateAcrossScopes()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);

            await using (var scope = database.Scope())
            {
                var store = scope.Get<IIdempotencyStore>();
                var clock = scope.Get<TimeProvider>();
                var entry = new IdempotencyEntry(
                    "owner",
                    "POST /api/v1/users",
                    "key3",
                    "hash",
                    0,
                    "application/json",
                    [],
                    clock.GetUtcNow()
                );
                Assert.True(await store.TryReserveAsync(entry, Ct));

                // Complete the reservation in another scope (simulating the winner).
                await using (var other = database.Scope())
                {
                    var otherStore = other.Get<IIdempotencyStore>();
                    var otherClock = other.Get<TimeProvider>();
                    await otherStore.SaveAsync(
                        new IdempotencyEntry(
                            "owner",
                            "POST /api/v1/users",
                            "key3",
                            "hash",
                            201,
                            "application/json",
                            [1, 2, 3],
                            otherClock.GetUtcNow()
                        ),
                        Ct
                    );
                }

                // The waiter in the original scope must observe completion
                // despite the tracked pending instance.
                var reloaded = await store.GetAsync("owner", "POST /api/v1/users", "key3", Ct);
                Assert.NotNull(reloaded);
                Assert.Equal(201, reloaded!.StatusCode);
                Assert.Equal(new byte[] { 1, 2, 3 }, reloaded.Body);
            }
        }

        [Fact]
        public async Task RemoveAsyncRetriesOnceAfterWrappedBusy()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);

            var services = new ServiceCollection();
            services.AddJobScrapePersistence(database.ConnectionString);

            foreach (
                var descriptor in services
                    .Where(descriptor =>
                        descriptor.ServiceType == typeof(JobScrapeDbContext)
                        || descriptor.ServiceType == typeof(DbContextOptions<JobScrapeDbContext>)
                    )
                    .ToList()
            )
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<JobScrapeDbContext>(options =>
                options
                    .UseSqlite(database.ConnectionString)
                    .AddInterceptors(new SqlitePragmaInterceptor(), new BusyInterceptor())
            );
            services.TryAddSingleton(TimeProvider.System);
            var provider = services.BuildServiceProvider();

            try
            {
                await using var scope = provider.CreateAsyncScope();
                var store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
                var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();

                var entry = new IdempotencyEntry(
                    "owner",
                    "POST /api/v1/users",
                    "wrapped-busy-key",
                    "hash",
                    0,
                    "application/json",
                    [],
                    clock.GetUtcNow()
                );
                Assert.True(await store.TryReserveAsync(entry, Ct));

                await store.RemoveAsync("owner", "POST /api/v1/users", "wrapped-busy-key", Ct);

                var reloaded = await store.GetAsync(
                    "owner",
                    "POST /api/v1/users",
                    "wrapped-busy-key",
                    Ct
                );
                Assert.Null(reloaded);
            }
            finally
            {
                await provider.DisposeAsync();
            }
        }

        [Fact]
        public async Task ExpiredEntryReadsAsAbsentAndIsReplacedOnReserve()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            await using var scope = database.Scope();
            var clock = new FakeTimeProvider(
                new DateTimeOffset(2026, 11, 8, 0, 0, 0, TimeSpan.Zero)
            );
            var store = new IdempotencyStore(scope.Db, clock);
            const string endpoint = "POST /api/v1/users";
            const string key = "ttl-key";

            var entry = new IdempotencyEntry(
                "owner",
                endpoint,
                key,
                "hash",
                201,
                "application/json",
                [1, 2, 3],
                clock.GetUtcNow()
            );
            Assert.True(await store.TryReserveAsync(entry, Ct));
            await store.SaveAsync(entry, Ct);
            Assert.NotNull(await store.GetAsync("owner", endpoint, key, Ct));

            // Past the 24-hour retention the completed row reads as absent.
            clock.Advance(TimeSpan.FromHours(25));
            Assert.Null(await store.GetAsync("owner", endpoint, key, Ct));

            // A retry after expiry wins a fresh reservation, replacing the
            // stale row rather than reporting a conflict.
            var fresh = new IdempotencyEntry(
                "owner",
                endpoint,
                key,
                "hash-2",
                0,
                "application/json",
                [],
                clock.GetUtcNow()
            );
            Assert.True(await store.TryReserveAsync(fresh, Ct));

            var reloaded = await store.GetAsync("owner", endpoint, key, Ct);
            Assert.NotNull(reloaded);
            Assert.Equal(0, reloaded!.StatusCode);
            Assert.Equal("hash-2", reloaded.RequestHash);
            Assert.Equal(1, await scope.Db.IdempotencyRecords.CountAsync(Ct));
        }

        [Fact]
        public async Task SaveAsyncKeepsTheFirstCompletedResponseOnReplay()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            await using var scope = database.Scope();
            var store = scope.Get<IIdempotencyStore>();
            var clock = scope.Get<TimeProvider>();
            const string endpoint = "POST /api/v1/users";
            const string key = "replay-key";
            var entry = new IdempotencyEntry(
                "owner",
                endpoint,
                key,
                "hash",
                201,
                "application/json",
                [1],
                clock.GetUtcNow()
            );
            Assert.True(await store.TryReserveAsync(entry, Ct));
            await store.SaveAsync(entry, Ct);

            // A second writer must not overwrite the completed response.
            await store.SaveAsync(entry with { StatusCode = 500, Body = [9] }, Ct);

            var reloaded = await store.GetAsync("owner", endpoint, key, Ct);
            Assert.NotNull(reloaded);
            Assert.Equal(201, reloaded!.StatusCode);
            Assert.Equal(new byte[] { 1 }, reloaded.Body);
        }

        [Fact]
        public async Task SaveAsyncReplacesAnExpiredCompletedResponse()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            await using var scope = database.Scope();
            var clock = new FakeTimeProvider(
                new DateTimeOffset(2026, 11, 8, 0, 0, 0, TimeSpan.Zero)
            );
            var store = new IdempotencyStore(scope.Db, clock);
            const string endpoint = "POST /api/v1/users";
            const string key = "stale-key";
            var stale = new IdempotencyEntry(
                "owner",
                endpoint,
                key,
                "hash",
                201,
                "application/json",
                [1],
                clock.GetUtcNow()
            );
            Assert.True(await store.TryReserveAsync(stale, Ct));
            await store.SaveAsync(stale, Ct);

            clock.Advance(TimeSpan.FromHours(25));
            var fresh = new IdempotencyEntry(
                "owner",
                endpoint,
                key,
                "hash-2",
                200,
                "application/json",
                [2],
                clock.GetUtcNow()
            );
            await store.SaveAsync(fresh, Ct);

            var reloaded = await store.GetAsync("owner", endpoint, key, Ct);
            Assert.NotNull(reloaded);
            Assert.Equal(200, reloaded!.StatusCode);
            Assert.Equal("hash-2", reloaded.RequestHash);
            Assert.Equal(new byte[] { 2 }, reloaded.Body);
            Assert.Equal(1, await scope.Db.IdempotencyRecords.CountAsync(Ct));
        }

        [Fact]
        public async Task RemoveAsyncOfAnAbsentKeyIsANoOp()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            await using var scope = database.Scope();
            var store = scope.Get<IIdempotencyStore>();

            var failure = await Record.ExceptionAsync(() =>
                store.RemoveAsync("owner", "POST /api/v1/users", "never-written", Ct)
            );

            Assert.Null(failure);
        }

        [Fact]
        public async Task RemoveAsyncSwallowsUnexpectedFailuresAfterLogging()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            await using var scope = database.Scope();
            var store = new IdempotencyStore(scope.Db, scope.Get<TimeProvider>());
            await scope.Db.DisposeAsync();

            var failure = await Record.ExceptionAsync(() =>
                store.RemoveAsync("owner", "POST /api/v1/users", "disposed", Ct)
            );

            Assert.Null(failure);
        }

        [Fact]
        public async Task RemoveAsyncGivesUpAfterTheRetryAlsoFails()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            await using var provider = ProviderWith(database, new BusyInterceptor(failures: 2));
            await using var scope = provider.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
            var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();

            var entry = new IdempotencyEntry(
                "owner",
                "POST /api/v1/users",
                "twice-busy-key",
                "hash",
                0,
                "application/json",
                [],
                clock.GetUtcNow()
            );
            Assert.True(await store.TryReserveAsync(entry, Ct));

            var failure = await Record.ExceptionAsync(() =>
                store.RemoveAsync("owner", "POST /api/v1/users", "twice-busy-key", Ct)
            );

            Assert.Null(failure);
            var reloaded = await store.GetAsync(
                "owner",
                "POST /api/v1/users",
                "twice-busy-key",
                Ct
            );
            Assert.NotNull(reloaded);
        }

        [Fact]
        public async Task SaveAsyncSwallowsALostUniqueInsertRace()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            var unique = new UniqueViolationInterceptor();
            await using var provider = ProviderWith(database, unique);
            await using var scope = provider.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
            var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();
            var entry = new IdempotencyEntry(
                "owner",
                "POST /api/v1/users",
                "race-key",
                "hash",
                201,
                "application/json",
                [1],
                clock.GetUtcNow()
            );
            Assert.True(await store.TryReserveAsync(entry, Ct));

            unique.Armed = true;
            var failure = await Record.ExceptionAsync(() => store.SaveAsync(entry, Ct));

            Assert.Null(failure);
            var reloaded = await store.GetAsync("owner", "POST /api/v1/users", "race-key", Ct);
            Assert.NotNull(reloaded);
            Assert.Equal(0, reloaded!.StatusCode);
        }

        private static ServiceProvider ProviderWith(
            TestDatabase database,
            params SaveChangesInterceptor[] interceptors
        )
        {
            var services = new ServiceCollection();
            services.AddJobScrapePersistence(database.ConnectionString);
            foreach (
                var descriptor in services
                    .Where(candidate =>
                        candidate.ServiceType == typeof(JobScrapeDbContext)
                        || candidate.ServiceType == typeof(DbContextOptions<JobScrapeDbContext>)
                    )
                    .ToList()
            )
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<JobScrapeDbContext>(options =>
                options.UseSqlite(database.ConnectionString).AddInterceptors(interceptors)
            );
            services.TryAddSingleton(TimeProvider.System);
            return services.BuildServiceProvider();
        }

        private sealed class UniqueViolationInterceptor : SaveChangesInterceptor
        {
            public bool Armed { get; set; }

            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
                DbContextEventData eventData,
                InterceptionResult<int> result,
                CancellationToken cancellationToken = default
            )
            {
                if (
                    Armed
                    && eventData.Context is not null
                    && eventData
                        .Context.ChangeTracker.Entries()
                        .Any(entry =>
                            entry.State != EntityState.Unchanged
                            && entry.Entity.GetType().Name == "IdempotencyRecord"
                        )
                )
                {
                    throw new DbUpdateException(
                        "lost insert race",
                        new SqliteException("UNIQUE constraint failed", 2067, 2067)
                    );
                }

                return base.SavingChangesAsync(eventData, result, cancellationToken);
            }
        }

        private sealed class BusyInterceptor : SaveChangesInterceptor
        {
            private readonly int _failures;
            private int _thrown;

            public BusyInterceptor(int failures = 1)
            {
                _failures = failures;
            }

            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
                DbContextEventData eventData,
                InterceptionResult<int> result,
                CancellationToken cancellationToken = default
            )
            {
                if (_thrown < _failures && eventData.Context is not null)
                {
                    var deletes = eventData
                        .Context.ChangeTracker.Entries()
                        .Any(entry =>
                            entry.State == EntityState.Deleted
                            && entry.Entity.GetType().Name == "IdempotencyRecord"
                        );
                    if (deletes)
                    {
                        _thrown++;
                        var sqlite = new SqliteException("database is locked", 6);
                        throw new DbUpdateException("wrapped busy", sqlite);
                    }
                }

                return base.SavingChangesAsync(eventData, result, cancellationToken);
            }
        }
    }
}
