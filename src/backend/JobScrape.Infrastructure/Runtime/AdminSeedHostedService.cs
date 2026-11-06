using JobScrape.Application.Abstractions;
using JobScrape.Domain;
using JobScrape.Domain.Identity;
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

        /// <summary>
        /// Initializes a new instance of the <see cref="AdminSeedHostedService"/> class.
        /// </summary>
        /// <param name="scopes">Creates the seeding scope. Must not be null.</param>
        /// <param name="clock">The time source. Must not be null.</param>
        /// <param name="logger">The logger. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public AdminSeedHostedService(
            IServiceScopeFactory scopes,
            TimeProvider clock,
            ILogger<AdminSeedHostedService> logger
        )
        {
            ArgumentNullException.ThrowIfNull(scopes);
            ArgumentNullException.ThrowIfNull(clock);
            ArgumentNullException.ThrowIfNull(logger);
            _scopes = scopes;
            _clock = clock;
            _logger = logger;
        }

        /// <summary>
        /// Seeds the admin when no users exist yet.
        /// </summary>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>A task that completes when seeding is done or skipped.</returns>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await using var scope = _scopes.CreateAsyncScope();
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            if ((await users.ListAsync(cancellationToken)).Count != 0)
            {
                return;
            }

            var email = Environment.GetEnvironmentVariable("ADMIN_EMAIL");
            var secret = Environment.GetEnvironmentVariable("ADMIN_SETUP_SECRET");
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
            var existingAdmin = await roles.GetByNameAsync(RoleName.Admin, cancellationToken);
            var admin = existingAdmin ?? Role.Create(RoleId.New(), RoleName.Admin);
            foreach (var key in PermissionCatalog.Keys)
            {
                if (!admin.PermissionKeys.Contains(key))
                {
                    admin.Grant(key);
                }
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
            if (existingAdmin is null)
            {
                await roles.AddAsync(admin, cancellationToken);
            }

            await users.AddAsync(user, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            LogSeeded(_logger);
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
    }
}
