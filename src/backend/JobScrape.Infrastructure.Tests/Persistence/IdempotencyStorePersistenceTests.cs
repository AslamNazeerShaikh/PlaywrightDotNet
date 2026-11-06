using JobScrape.Application.Abstractions;
using JobScrape.Infrastructure.Persistence;
using JobScrape.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

        private sealed class BusyInterceptor : SaveChangesInterceptor
        {
            private int _thrown;

            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
                DbContextEventData eventData,
                InterceptionResult<int> result,
                CancellationToken cancellationToken = default
            )
            {
                if (_thrown == 0 && eventData.Context is not null)
                {
                    var deletes = eventData
                        .Context.ChangeTracker.Entries()
                        .Any(entry =>
                            entry.State == EntityState.Deleted
                            && entry.Entity.GetType().Name == "IdempotencyRecord"
                        );
                    if (deletes)
                    {
                        _thrown = 1;
                        var sqlite = new SqliteException("database is locked", 6);
                        throw new DbUpdateException("wrapped busy", sqlite);
                    }
                }

                return base.SavingChangesAsync(eventData, result, cancellationToken);
            }
        }
    }
}
