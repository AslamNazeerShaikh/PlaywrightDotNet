using JobScrape.Application.Abstractions;
using JobScrape.Domain;
using JobScrape.Domain.Access;
using JobScrape.Domain.Identity;
using JobScrape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JobScrape.Infrastructure.Runtime
{
    /// <summary>
    /// Seeds the default admin on first start. When the users table is
    /// empty, creates the admin from <c>ADMIN_EMAIL</c> and
    /// <c>ADMIN_SETUP_SECRET</c>, assigns (creating) the admin role with
    /// every catalog permission, and leaves the account awaiting first
    /// login. A non-empty users table is a no-op. Secrets are never logged.
    /// </summary>
    public sealed partial class AdminSeedHostedService : IHostedService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly TimeProvider _clock;
        private readonly ILogger<AdminSeedHostedService> _logger;
        private readonly string? _adminEmail;
        private readonly string? _adminSetupSecret;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdminSeedHostedService"/> class.
        /// </summary>
        /// <param name="scopes">Creates the seeding scope. Must not be null.</param>
        /// <param name="clock">The time source. Must not be null.</param>
        /// <param name="logger">The logger. Must not be null.</param>
        /// <param name="adminEmail">
        /// Optional override for <c>ADMIN_EMAIL</c>. When null the process
        /// environment is consulted, preserving production behavior.
        /// </param>
        /// <param name="adminSetupSecret">
        /// Optional override for <c>ADMIN_SETUP_SECRET</c>. When null the process
        /// environment is consulted, preserving production behavior.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public AdminSeedHostedService(
            IServiceScopeFactory scopes,
            TimeProvider clock,
            ILogger<AdminSeedHostedService> logger,
            string? adminEmail = null,
            string? adminSetupSecret = null
        )
        {
            ArgumentNullException.ThrowIfNull(scopes);
            ArgumentNullException.ThrowIfNull(clock);
            ArgumentNullException.ThrowIfNull(logger);
            _scopes = scopes;
            _clock = clock;
            _logger = logger;
            _adminEmail = adminEmail;
            _adminSetupSecret = adminSetupSecret;
        }

        /// <summary>
        /// Seeds the admin when no users exist yet, and the usage quota
        /// starting budgets when no definitions exist yet.
        /// </summary>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>A task that completes when seeding is done or skipped.</returns>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await using var scope = _scopes.CreateAsyncScope();
            await SeedUsageQuotasAsync(scope.ServiceProvider, cancellationToken);
            await EnsureRolesAsync(scope.ServiceProvider, cancellationToken);

            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            if ((await users.ListAsync(cancellationToken)).Count != 0)
            {
                return;
            }

            var email = _adminEmail ?? Environment.GetEnvironmentVariable("ADMIN_EMAIL");
            var secret =
                _adminSetupSecret ?? Environment.GetEnvironmentVariable("ADMIN_SETUP_SECRET");
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(secret))
            {
                LogSeedSkipped(_logger);
                return;
            }

            PasswordHash hash;
            try
            {
                hash = PasswordHash.Create(secret);
            }
            catch (DomainException ex)
            {
                LogBadSeedSecret(_logger, ex.Message);
                return;
            }

            var roles = scope.ServiceProvider.GetRequiredService<IRoleRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var admin = await roles.GetByNameAsync(RoleName.Admin, cancellationToken);
            if (admin is null)
            {
                // EnsureRolesAsync should have created it; fall back defensively.
                admin = Role.Create(RoleId.New(), RoleName.Admin);
                foreach (var key in PermissionCatalog.Keys)
                {
                    admin.Grant(key);
                }

                await roles.AddAsync(admin, cancellationToken);
            }

            User user;
            try
            {
                user = User.CreateSeedAdmin(email, hash, _clock.GetUtcNow());
            }
            catch (DomainException ex)
            {
                LogBadSeedEmail(_logger, ex.Message);
                return;
            }

            user.AssignRole(admin.Id);

            await users.AddAsync(user, cancellationToken);
            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
                when (unitOfWork.IsUniqueViolation(ex))
            {
                // Another instance seeded concurrently; nothing to do.
                LogSeedRace(_logger);
                return;
            }

            LogSeeded(_logger);
        }

        /// <summary>
        /// Ensures the three closed roles exist with their documented grant
        /// sets. Existing rows are reused; missing catalog keys on an
        /// existing Admin are repaired, but revoked keys are never removed.
        /// Only missing rows are inserted. Concurrent instances that race
        /// on the unique role name are tolerated by catching the resulting
        /// unique violation and re-reading.
        /// </summary>
        /// <param name="provider">The seeding scope's services.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>A task that completes when the roles are ensured.</returns>
        private static async Task EnsureRolesAsync(
            IServiceProvider provider,
            CancellationToken cancellationToken
        )
        {
            var roles = provider.GetRequiredService<IRoleRepository>();
            var unitOfWork = provider.GetRequiredService<IUnitOfWork>();
            var db = provider.GetRequiredService<JobScrapeDbContext>();

            var desired = new (RoleName Name, IReadOnlyList<PermissionKey> Grants)[]
            {
                (RoleName.Admin, PermissionCatalog.Keys.ToList()),
                (
                    RoleName.Operator,
                    new[]
                    {
                        PermissionKey.From("profiles.read"),
                        PermissionKey.From("profiles.write"),
                        PermissionKey.From("runs.read"),
                        PermissionKey.From("runs.write"),
                    }
                ),
                (
                    RoleName.Viewer,
                    new[] { PermissionKey.From("profiles.read"), PermissionKey.From("runs.read") }
                ),
            };

            // Read the existing role names once, then stage only the missing
            // roles. AddAsync only stages in the change tracker; the single
            // SaveChangesAsync below is where a concurrent instance's insert
            // surfaces as a SQLite unique-name conflict.
            var existingRoles = await roles.ListAsync(cancellationToken);
            var existingNames = existingRoles.Select(role => role.Name).ToHashSet();
            var staged = new List<Role>();
            var repairedGrants = false;
            foreach (var (name, grants) in desired)
            {
                if (existingNames.Contains(name))
                {
                    // Reconcile an existing Admin role: grant any catalog keys
                    // that are missing, but never revoke keys an admin has
                    // intentionally removed. Save only when something changed.
                    if (name == RoleName.Admin)
                    {
                        var existing =
                            existingRoles.FirstOrDefault(role => role.Name == name)
                            ?? await roles.GetByNameAsync(name, cancellationToken);
                        if (existing is not null)
                        {
                            foreach (var key in grants)
                            {
                                if (!existing.PermissionKeys.Contains(key))
                                {
                                    existing.Grant(key);
                                    repairedGrants = true;
                                }
                            }
                        }
                    }

                    continue;
                }

                var role = Role.Create(RoleId.New(), name);
                foreach (var key in grants)
                {
                    role.Grant(key);
                }

                await roles.AddAsync(role, cancellationToken);
                staged.Add(role);
            }

            if (staged.Count == 0 && !repairedGrants)
            {
                return;
            }

            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (unitOfWork.IsUniqueViolation(ex))
            {
                // A concurrent instance won one or more inserts. Detach the
                // roles this instance staged (never reuse their RoleIds) and
                // re-read from the database; the caller re-queries by name.
                foreach (var entry in db.ChangeTracker.Entries<Role>().ToList())
                {
                    if (entry.State == EntityState.Added)
                    {
                        entry.State = EntityState.Detached;
                    }
                }

                var logger = provider.GetRequiredService<ILogger<AdminSeedHostedService>>();
                LogSeedRace(logger);
            }
        }

        /// <summary>
        /// Seeds the usage quota starting budgets when no definitions
        /// exist yet. A table with rows is a no-op: admins own the values
        /// from there and a restart never overwrites them.
        /// </summary>
        /// <param name="provider">The seeding scope's services.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>A task that completes when seeding is done or skipped.</returns>
        private static async Task SeedUsageQuotasAsync(
            IServiceProvider provider,
            CancellationToken cancellationToken
        )
        {
            var quotas = provider.GetRequiredService<IUsageQuotaRepository>();
            if ((await quotas.ListAsync(cancellationToken)).Count != 0)
            {
                return;
            }

            foreach (var quota in UsageQuotaDefaults.All())
            {
                await quotas.AddAsync(quota, cancellationToken);
            }

            try
            {
                await provider
                    .GetRequiredService<IUnitOfWork>()
                    .SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
                when (provider.GetRequiredService<IUnitOfWork>().IsUniqueViolation(ex))
            {
                // A concurrent instance seeded first; definitions now exist.
                return;
            }
        }

        /// <summary>
        /// Stops seeding. Nothing to release.
        /// </summary>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>A completed task.</returns>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "No users exist and ADMIN_EMAIL or ADMIN_SETUP_SECRET is missing; skipping admin seed"
        )]
        private static partial void LogSeedSkipped(ILogger logger);

        [LoggerMessage(
            Level = LogLevel.Error,
            Message = "Admin seed secret does not meet the password criteria: {Reason}"
        )]
        private static partial void LogBadSeedSecret(ILogger logger, string reason);

        [LoggerMessage(Level = LogLevel.Error, Message = "Admin seed email is invalid: {Reason}")]
        private static partial void LogBadSeedEmail(ILogger logger, string reason);

        [LoggerMessage(Level = LogLevel.Information, Message = "Seeded the default admin account")]
        private static partial void LogSeeded(ILogger logger);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Admin seed lost a concurrency race; another instance seeded first"
        )]
        private static partial void LogSeedRace(ILogger logger);
    }
}
