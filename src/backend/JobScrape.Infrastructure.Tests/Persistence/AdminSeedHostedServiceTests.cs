using JobScrape.Application.Abstractions;
using JobScrape.Domain;
using JobScrape.Domain.Identity;
using JobScrape.Infrastructure.Persistence;
using JobScrape.Infrastructure.Runtime;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace JobScrape.Infrastructure.Tests.Persistence
{
    [Trait("Category", "Integration")]
    public sealed class AdminSeedHostedServiceTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 6, 8, 0, 0, TimeSpan.Zero);

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact]
        public async Task ConcurrentSeedLeavesOneAdminAndOneRaceWarning()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);

            var logger = new CapturingLogger();
            var gateState = new GateState();

            var interceptor1 = new UserSaveGateInterceptor(gateState);
            var interceptor2 = new UserSaveGateInterceptor(gateState);

            await using var provider1 = BuildProvider(
                database.ConnectionString,
                interceptor1,
                logger
            );
            await using var provider2 = BuildProvider(
                database.ConnectionString,
                interceptor2,
                logger
            );

            var hosted1 = new AdminSeedHostedService(
                provider1.GetRequiredService<IServiceScopeFactory>(),
                TimeProvider.System,
                logger,
                "admin@example.com",
                "Seed-Secret1!!"
            );
            var hosted2 = new AdminSeedHostedService(
                provider2.GetRequiredService<IServiceScopeFactory>(),
                TimeProvider.System,
                logger,
                "admin@example.com",
                "Seed-Secret1!!"
            );

            var first = hosted1.StartAsync(Ct);
            var second = hosted2.StartAsync(Ct);

            await CompleteWithinAsync(gateState.Arrived.Task, "seed race gate");
            gateState.Release.SetResult();

            await CompleteWithinAsync(Task.WhenAll(first, second), "seed race completion");

            await using var scope = provider1.CreateAsyncScope();
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var roles = scope.ServiceProvider.GetRequiredService<IRoleRepository>();

            var allUsers = await users.ListAsync(Ct);
            var allRoles = await roles.ListAsync(Ct);

            Assert.Equal(3, allRoles.Count);
            var adminRole = Assert.Single(allRoles, role => role.Name == RoleName.Admin);
            Assert.Equal(1, allUsers.Count(user => user.Roles.Contains(adminRole.Id)));

            Assert.Equal(allUsers.Count, allUsers.Select(user => user.Id).Distinct().Count());
            Assert.Equal(allRoles.Count, allRoles.Select(role => role.Id).Distinct().Count());

            // Both the role save and the user save race exactly once:
            // one loser per stage, one warning each.
            Assert.Equal(
                2,
                logger.Entries.Count(entry =>
                    entry.Level == LogLevel.Warning
                    && entry.Message.Contains(
                        "concurrency race",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
            );
        }

        [Fact]
        public async Task FreshBootRepairsAdminGrants()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);

            var missingKey = PermissionKey.From("profiles.read");

            await using (var seedScope = database.Scope())
            {
                var roles = seedScope.Get<IRoleRepository>();
                var unitOfWork = seedScope.Get<IUnitOfWork>();
                var role = Role.Create(RoleId.New(), RoleName.Admin);
                foreach (var key in PermissionCatalog.Keys)
                {
                    if (key != missingKey)
                    {
                        role.Grant(key);
                    }
                }

                await roles.AddAsync(role, Ct);
                await unitOfWork.SaveChangesAsync(Ct);
            }

            var services = new ServiceCollection();
            services.AddJobScrapePersistence(database.ConnectionString);
            await using var provider = services.BuildServiceProvider(validateScopes: true);

            var logger = new CapturingLogger();
            var hosted = new AdminSeedHostedService(
                provider.GetRequiredService<IServiceScopeFactory>(),
                TimeProvider.System,
                logger
            );

            await hosted.StartAsync(Ct);

            await using var verifyScope = database.Scope();
            var verifyRoles = verifyScope.Get<IRoleRepository>();
            var adminRole = await verifyRoles.GetByNameAsync(RoleName.Admin, Ct);
            Assert.NotNull(adminRole);
            Assert.Contains(missingKey, adminRole.PermissionKeys);
        }

        private static async Task CompleteWithinAsync(Task task, string label)
        {
            var done = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5), Ct));
            Assert.True(ReferenceEquals(done, task), $"{label} did not complete in time.");
            await task;
        }

        private static ServiceProvider BuildProvider(
            string connectionString,
            UserSaveGateInterceptor interceptor,
            CapturingLogger logger
        )
        {
            var services = new ServiceCollection();
            services.AddJobScrapePersistence(connectionString);

            var contextDescriptor = services.First(descriptor =>
                descriptor.ServiceType == typeof(JobScrapeDbContext)
            );
            services.Remove(contextDescriptor);

            services.AddSingleton(interceptor);
            services.AddSingleton<ILogger<AdminSeedHostedService>>(logger);

            services.AddDbContext<JobScrapeDbContext>(
                (provider, options) =>
                    options
                        .UseSqlite(connectionString)
                        .AddInterceptors(
                            provider.GetRequiredService<SqlitePragmaInterceptor>(),
                            interceptor
                        )
            );

            return services.BuildServiceProvider(validateScopes: true);
        }

        internal sealed class GateState
        {
            public int Arrivals;
            public TaskCompletionSource Arrived { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource Release { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        internal sealed class UserSaveGateInterceptor : SaveChangesInterceptor
        {
            private readonly GateState _state;

            public UserSaveGateInterceptor(GateState state)
            {
                _state = state;
            }

            public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
                DbContextEventData eventData,
                InterceptionResult<int> result,
                CancellationToken cancellationToken = default
            )
            {
                // Gate role and user saves alike so both stages genuinely
                // race; quota definitions are seeded before either gate and
                // stay uncontended by design.
                if (
                    eventData.Context is not null
                    && eventData
                        .Context.ChangeTracker.Entries()
                        .Any(entry =>
                            entry.State == EntityState.Added
                            && (entry.Entity is User || entry.Entity is Domain.Identity.Role)
                        )
                )
                {
                    if (Interlocked.Increment(ref _state.Arrivals) == 2)
                    {
                        _state.Arrived.TrySetResult();
                    }

                    await _state.Release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                }

                return await base.SavingChangesAsync(eventData, result, cancellationToken);
            }
        }

        internal sealed class CapturingLogger : ILogger<AdminSeedHostedService>
        {
            private readonly Lock _gate = new();

            public List<LogEntry> Entries { get; } = [];

            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter
            )
            {
                var properties = state is IEnumerable<KeyValuePair<string, object?>> pairs
                    ? pairs.ToDictionary(pair => pair.Key, pair => pair.Value)
                    : [];
                var entry = new LogEntry(
                    logLevel,
                    formatter(state, exception),
                    properties,
                    exception
                );
                lock (_gate)
                {
                    Entries.Add(entry);
                }
            }
        }

        internal sealed record LogEntry(
            LogLevel Level,
            string Message,
            Dictionary<string, object?> Properties,
            Exception? Exception
        );
    }
}
