using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.Identity;
using JobScrape.Application.Results;
using JobScrape.Domain.Identity;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace JobScrape.Application.Tests
{
    public sealed class IdentityHandlerTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 6, 8, 0, 0, TimeSpan.Zero);
        private const string SeedSecret = "Seed-Secret1!!";
        private const string FinalPassword = "Final-Password2@@";
        private const string TempPassword = "Temp-Password3##";

        private static User PendingUser(string email = "op@example.com")
        {
            return User.Create(email, PasswordHash.Create(SeedSecret), Now);
        }

        private static User ActiveUser(string email = "op@example.com")
        {
            var user = PendingUser(email);
            user.CompleteFirstLogin(PasswordHash.Create(FinalPassword), Now);
            return user;
        }

        private static Role AdminRole()
        {
            var role = Role.Create(RoleId.New(), RoleName.Admin);
            foreach (var key in PermissionCatalog.Keys)
            {
                role.Grant(key);
            }

            return role;
        }

        private sealed class FakeUsers : IUserRepository
        {
            private readonly List<User> _users = [];

            public int Saves { get; private set; }

            public void Seed(User user)
            {
                _users.Add(user);
            }

            public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
            {
                var normalized = email.Trim().ToLowerInvariant();
                return Task.FromResult(
                    _users.FirstOrDefault(user =>
                        string.Equals(user.Email, normalized, StringComparison.OrdinalIgnoreCase)
                    )
                );
            }

            public Task<User?> GetAsync(UserId id, CancellationToken cancellationToken)
            {
                return Task.FromResult(_users.FirstOrDefault(user => user.Id == id));
            }

            public Task AddAsync(User user, CancellationToken cancellationToken)
            {
                _users.Add(user);
                return Task.CompletedTask;
            }

            public Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult<IReadOnlyList<User>>(_users.ToList());
            }
        }

        private sealed class FakeRoles : IRoleRepository
        {
            private readonly List<Role> _roles = [];

            public void Seed(Role role)
            {
                _roles.Add(role);
            }

            public Task<Role?> GetAsync(RoleId id, CancellationToken cancellationToken)
            {
                return Task.FromResult(_roles.FirstOrDefault(role => role.Id == id));
            }

            public Task<Role?> GetByNameAsync(RoleName name, CancellationToken cancellationToken)
            {
                return Task.FromResult(_roles.FirstOrDefault(role => role.Name == name));
            }

            public Task AddAsync(Role role, CancellationToken cancellationToken)
            {
                _roles.Add(role);
                return Task.CompletedTask;
            }

            public Task<IReadOnlyList<Role>> ListAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult<IReadOnlyList<Role>>(_roles.ToList());
            }
        }

        private sealed class FakeTokens : ITokenService
        {
            public User? SeenUser { get; private set; }

            public IReadOnlyList<PermissionKey>? SeenPermissions { get; private set; }

            public string CreateToken(
                User user,
                IReadOnlyList<Role> roles,
                IReadOnlyList<PermissionKey> permissions,
                DateTimeOffset now
            )
            {
                SeenUser = user;
                SeenPermissions = permissions;
                return "opaque-token";
            }
        }

        private sealed class FakeUnitOfWork : IUnitOfWork
        {
            public int Saves { get; private set; }

            public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
            {
                Saves++;
                return Task.FromResult(0);
            }
        }

        private static LoginHandler Login(
            FakeUsers users,
            FakeRoles roles,
            FakeTokens tokens,
            FakeUnitOfWork unitOfWork,
            FakeTimeProvider clock,
            int maxAttempts = 5
        )
        {
            return new LoginHandler(
                users,
                roles,
                tokens,
                unitOfWork,
                new LoginPolicy(maxAttempts, TimeSpan.FromMinutes(15)),
                clock
            );
        }

        [Fact]
        public async Task LoginSucceedsWithTokenAndClearsFailures()
        {
            var users = new FakeUsers();
            var user = ActiveUser();
            user.RecordFailedLogin(Now, 5, TimeSpan.FromMinutes(15));
            users.Seed(user);
            var roles = new FakeRoles();
            var admin = AdminRole();
            roles.Seed(admin);
            user.AssignRole(admin.Id);
            var tokens = new FakeTokens();
            var unitOfWork = new FakeUnitOfWork();
            var handler = Login(users, roles, tokens, unitOfWork, new FakeTimeProvider(Now));

            var result = await handler.HandleAsync(
                "OP@example.com",
                FinalPassword,
                TestContext.Current.CancellationToken
            );

            Assert.True(result.IsSuccess);
            Assert.Equal("opaque-token", result.Value.Token);
            Assert.False(result.Value.MustChangePassword);
            Assert.Same(user, tokens.SeenUser);
            Assert.Equal(PermissionCatalog.Keys, tokens.SeenPermissions);
            Assert.Equal(0, user.FailedAttempts);
            Assert.Equal(1, unitOfWork.Saves);
        }

        [Fact]
        public async Task LoginWithUnknownEmailFailsInvalidCredentials()
        {
            var handler = Login(
                new FakeUsers(),
                new FakeRoles(),
                new FakeTokens(),
                new FakeUnitOfWork(),
                new FakeTimeProvider(Now)
            );

            var result = await handler.HandleAsync(
                "nobody@example.com",
                "whatever",
                TestContext.Current.CancellationToken
            );

            Assert.True(result.IsFailure);
            Assert.Equal(ErrorKind.Unauthorized, result.Error.Kind);
            Assert.Equal("INVALID_CREDENTIALS", result.Error.Code);
        }

        [Fact]
        public async Task LoginBeforeFirstLoginFailsResetRequired()
        {
            var users = new FakeUsers();
            users.Seed(PendingUser());
            var handler = Login(
                users,
                new FakeRoles(),
                new FakeTokens(),
                new FakeUnitOfWork(),
                new FakeTimeProvider(Now)
            );

            var result = await handler.HandleAsync(
                "op@example.com",
                SeedSecret,
                TestContext.Current.CancellationToken
            );

            Assert.True(result.IsFailure);
            Assert.Equal(ErrorKind.Forbidden, result.Error.Kind);
            Assert.Equal("PASSWORD_RESET_REQUIRED", result.Error.Code);
        }

        [Fact]
        public async Task LoginLocksOutAfterMaxAttempts()
        {
            var users = new FakeUsers();
            users.Seed(ActiveUser());
            var unitOfWork = new FakeUnitOfWork();
            var clock = new FakeTimeProvider(Now);
            var handler = Login(
                users,
                new FakeRoles(),
                new FakeTokens(),
                unitOfWork,
                clock,
                maxAttempts: 2
            );
            var ct = TestContext.Current.CancellationToken;

            var first = await handler.HandleAsync("op@example.com", "wrong", ct);
            var second = await handler.HandleAsync("op@example.com", "wrong", ct);
            var third = await handler.HandleAsync("op@example.com", FinalPassword, ct);

            Assert.Equal("INVALID_CREDENTIALS", first.Error.Code);
            Assert.Equal("INVALID_CREDENTIALS", second.Error.Code);
            Assert.True(third.IsFailure);
            Assert.Equal("ACCOUNT_LOCKED", third.Error.Code);
            Assert.Equal(ErrorKind.Forbidden, third.Error.Kind);
            Assert.Equal(3, unitOfWork.Saves);
        }

        [Fact]
        public async Task SetupPasswordCompletesFirstLogin()
        {
            var users = new FakeUsers();
            var user = PendingUser();
            users.Seed(user);
            var unitOfWork = new FakeUnitOfWork();
            var handler = new SetupPasswordHandler(users, unitOfWork, new FakeTimeProvider(Now));

            var result = await handler.HandleAsync(
                "op@example.com",
                SeedSecret,
                FinalPassword,
                TestContext.Current.CancellationToken
            );

            Assert.True(result.IsSuccess);
            Assert.False(user.MustChangePassword);
            Assert.True(user.PasswordHash.Verify(FinalPassword));
            Assert.Equal(1, unitOfWork.Saves);
        }

        [Fact]
        public async Task SetupPasswordWithWrongSecretFailsInvalidCredentials()
        {
            var users = new FakeUsers();
            users.Seed(PendingUser());
            var handler = new SetupPasswordHandler(
                users,
                new FakeUnitOfWork(),
                new FakeTimeProvider(Now)
            );

            var result = await handler.HandleAsync(
                "op@example.com",
                "wrong",
                FinalPassword,
                TestContext.Current.CancellationToken
            );

            Assert.Equal("INVALID_CREDENTIALS", result.Error.Code);
        }

        [Fact]
        public async Task SetupPasswordAfterCompletionFailsConflict()
        {
            var users = new FakeUsers();
            users.Seed(ActiveUser());
            var handler = new SetupPasswordHandler(
                users,
                new FakeUnitOfWork(),
                new FakeTimeProvider(Now)
            );

            var result = await handler.HandleAsync(
                "op@example.com",
                FinalPassword,
                TempPassword,
                TestContext.Current.CancellationToken
            );

            Assert.True(result.IsFailure);
            Assert.Equal(ErrorKind.Conflict, result.Error.Kind);
            Assert.Equal("PASSWORD_SETUP_COMPLETE", result.Error.Code);
        }

        [Fact]
        public async Task SetupPasswordWithUnknownEmailMatchesCompletedCode()
        {
            var handler = new SetupPasswordHandler(
                new FakeUsers(),
                new FakeUnitOfWork(),
                new FakeTimeProvider(Now)
            );

            var result = await handler.HandleAsync(
                "ghost@example.com",
                "whatever",
                FinalPassword,
                TestContext.Current.CancellationToken
            );

            Assert.True(result.IsFailure);
            Assert.Equal(ErrorKind.Conflict, result.Error.Kind);
            Assert.Equal("PASSWORD_SETUP_COMPLETE", result.Error.Code);
        }

        [Fact]
        public async Task SetupPasswordWithWeakPasswordFailsValidation()
        {
            var users = new FakeUsers();
            users.Seed(PendingUser());
            var handler = new SetupPasswordHandler(
                users,
                new FakeUnitOfWork(),
                new FakeTimeProvider(Now)
            );

            var result = await handler.HandleAsync(
                "op@example.com",
                SeedSecret,
                "short",
                TestContext.Current.CancellationToken
            );

            Assert.Equal("VALIDATION_FAILED", result.Error.Code);
        }

        [Fact]
        public async Task CreateUserStagesPendingUser()
        {
            var users = new FakeUsers();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new CreateUserHandler(users, unitOfWork, new FakeTimeProvider(Now));

            var result = await handler.HandleAsync(
                "new@example.com",
                TempPassword,
                TestContext.Current.CancellationToken
            );

            Assert.True(result.IsSuccess);
            var saved = await users.GetAsync(
                UserId.From(result.Value),
                TestContext.Current.CancellationToken
            );
            Assert.NotNull(saved);
            Assert.True(saved.MustChangePassword);
            Assert.True(saved.PasswordHash.Verify(TempPassword));
            Assert.Equal(1, unitOfWork.Saves);
        }

        [Fact]
        public async Task CreateUserWithTakenEmailFailsConflict()
        {
            var users = new FakeUsers();
            users.Seed(PendingUser());
            var handler = new CreateUserHandler(
                users,
                new FakeUnitOfWork(),
                new FakeTimeProvider(Now)
            );

            var result = await handler.HandleAsync(
                "OP@example.com",
                TempPassword,
                TestContext.Current.CancellationToken
            );

            Assert.Equal("EMAIL_TAKEN", result.Error.Code);
            Assert.Equal(ErrorKind.Conflict, result.Error.Kind);
        }

        [Fact]
        public async Task CreateUserWithBadEmailFailsValidation()
        {
            var handler = new CreateUserHandler(
                new FakeUsers(),
                new FakeUnitOfWork(),
                new FakeTimeProvider(Now)
            );

            var result = await handler.HandleAsync(
                "not-an-email",
                TempPassword,
                TestContext.Current.CancellationToken
            );

            Assert.Equal("VALIDATION_FAILED", result.Error.Code);
        }

        [Fact]
        public async Task AssignRoleSucceeds()
        {
            var users = new FakeUsers();
            var user = ActiveUser();
            users.Seed(user);
            var roles = new FakeRoles();
            var admin = AdminRole();
            roles.Seed(admin);
            var unitOfWork = new FakeUnitOfWork();
            var handler = new AssignRoleHandler(users, roles, unitOfWork);

            var result = await handler.HandleAsync(
                user.Id.Value,
                RoleName.Admin,
                TestContext.Current.CancellationToken
            );

            Assert.True(result.IsSuccess);
            Assert.Contains(admin.Id, user.Roles);
            Assert.Equal(1, unitOfWork.Saves);
        }

        [Fact]
        public async Task AssignRoleDuplicateFailsConflict()
        {
            var users = new FakeUsers();
            var user = ActiveUser();
            users.Seed(user);
            var roles = new FakeRoles();
            var admin = AdminRole();
            roles.Seed(admin);
            user.AssignRole(admin.Id);
            var handler = new AssignRoleHandler(users, roles, new FakeUnitOfWork());

            var result = await handler.HandleAsync(
                user.Id.Value,
                RoleName.Admin,
                TestContext.Current.CancellationToken
            );

            Assert.Equal("ROLE_ALREADY_ASSIGNED", result.Error.Code);
        }

        [Fact]
        public async Task AssignRoleToMissingUserFailsNotFound()
        {
            var handler = new AssignRoleHandler(
                new FakeUsers(),
                new FakeRoles(),
                new FakeUnitOfWork()
            );

            var result = await handler.HandleAsync(
                Guid.NewGuid(),
                RoleName.Admin,
                TestContext.Current.CancellationToken
            );

            Assert.Equal("USER_NOT_FOUND", result.Error.Code);
            Assert.Equal(ErrorKind.NotFound, result.Error.Kind);
        }

        [Fact]
        public async Task AssignMissingRoleFailsNotFound()
        {
            var users = new FakeUsers();
            var user = ActiveUser();
            users.Seed(user);
            var handler = new AssignRoleHandler(users, new FakeRoles(), new FakeUnitOfWork());

            var result = await handler.HandleAsync(
                user.Id.Value,
                RoleName.Admin,
                TestContext.Current.CancellationToken
            );

            Assert.Equal("ROLE_NOT_FOUND", result.Error.Code);
        }

        [Fact]
        public async Task RevokeLastAdminIsRefused()
        {
            var users = new FakeUsers();
            var user = ActiveUser();
            users.Seed(user);
            var roles = new FakeRoles();
            var admin = AdminRole();
            roles.Seed(admin);
            user.AssignRole(admin.Id);
            var handler = new RevokeRoleHandler(users, roles, new FakeUnitOfWork());

            var result = await handler.HandleAsync(
                user.Id.Value,
                RoleName.Admin,
                TestContext.Current.CancellationToken
            );

            Assert.True(result.IsFailure);
            Assert.Equal(ErrorKind.Conflict, result.Error.Kind);
            Assert.Equal("LAST_ADMIN_REQUIRED", result.Error.Code);
            Assert.Contains(admin.Id, user.Roles);
        }

        [Fact]
        public async Task RevokeAdminSucceedsWhenAnotherAdminExists()
        {
            var users = new FakeUsers();
            var first = ActiveUser("first@example.com");
            var second = ActiveUser("second@example.com");
            users.Seed(first);
            users.Seed(second);
            var roles = new FakeRoles();
            var admin = AdminRole();
            roles.Seed(admin);
            first.AssignRole(admin.Id);
            second.AssignRole(admin.Id);
            var handler = new RevokeRoleHandler(users, roles, new FakeUnitOfWork());

            var result = await handler.HandleAsync(
                first.Id.Value,
                RoleName.Admin,
                TestContext.Current.CancellationToken
            );

            Assert.True(result.IsSuccess);
            Assert.DoesNotContain(admin.Id, first.Roles);
        }

        [Fact]
        public async Task CreateRoleSucceedsAndDuplicateFails()
        {
            var roles = new FakeRoles();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new CreateRoleHandler(roles, unitOfWork);
            var ct = TestContext.Current.CancellationToken;

            var created = await handler.HandleAsync(RoleName.Operator, ct);
            var duplicate = await handler.HandleAsync(RoleName.Operator, ct);

            Assert.True(created.IsSuccess);
            Assert.NotNull(await roles.GetByNameAsync(RoleName.Operator, ct));
            Assert.Equal("ROLE_NAME_TAKEN", duplicate.Error.Code);
            Assert.Equal(1, unitOfWork.Saves);
        }

        [Fact]
        public async Task GrantPermissionSucceedsAndDuplicateFails()
        {
            var roles = new FakeRoles();
            roles.Seed(Role.Create(RoleId.New(), RoleName.Viewer));
            var handler = new GrantPermissionHandler(roles, new FakeUsers(), new FakeUnitOfWork());
            var ct = TestContext.Current.CancellationToken;

            var granted = await handler.HandleAsync(RoleName.Viewer, "runs.read", ct);
            var duplicate = await handler.HandleAsync(RoleName.Viewer, "runs.read", ct);

            Assert.True(granted.IsSuccess);
            Assert.Equal("PERMISSION_ALREADY_GRANTED", duplicate.Error.Code);
        }

        [Fact]
        public async Task GrantUnknownPermissionFails()
        {
            var roles = new FakeRoles();
            roles.Seed(Role.Create(RoleId.New(), RoleName.Viewer));
            var handler = new GrantPermissionHandler(roles, new FakeUsers(), new FakeUnitOfWork());

            var result = await handler.HandleAsync(
                RoleName.Viewer,
                "nope.zone",
                TestContext.Current.CancellationToken
            );

            Assert.Equal("UNKNOWN_PERMISSION", result.Error.Code);
            Assert.Equal(ErrorKind.Validation, result.Error.Kind);
        }

        [Fact]
        public async Task GrantMalformedPermissionFailsValidation()
        {
            var roles = new FakeRoles();
            roles.Seed(Role.Create(RoleId.New(), RoleName.Viewer));
            var handler = new GrantPermissionHandler(roles, new FakeUsers(), new FakeUnitOfWork());

            var result = await handler.HandleAsync(
                RoleName.Viewer,
                "bogus",
                TestContext.Current.CancellationToken
            );

            Assert.Equal("VALIDATION_FAILED", result.Error.Code);
        }

        [Fact]
        public async Task RevokePermissionRemovesGrantAndToleratesAbsence()
        {
            var roles = new FakeRoles();
            var role = Role.Create(RoleId.New(), RoleName.Viewer);
            role.Grant(PermissionKey.From("runs.read"));
            roles.Seed(role);
            var handler = new RevokePermissionHandler(roles, new FakeUsers(), new FakeUnitOfWork());
            var ct = TestContext.Current.CancellationToken;

            var revoked = await handler.HandleAsync(RoleName.Viewer, "runs.read", ct);
            var absent = await handler.HandleAsync(RoleName.Viewer, "runs.write", ct);

            Assert.True(revoked.IsSuccess);
            Assert.True(absent.IsSuccess);
            Assert.Empty(role.PermissionKeys);
        }

        [Fact]
        public async Task RevokePermissionFromAdminIsRefused()
        {
            var roles = new FakeRoles();
            var admin = Role.Create(RoleId.New(), RoleName.Admin);
            admin.Grant(PermissionKey.From("runs.read"));
            roles.Seed(admin);
            var handler = new RevokePermissionHandler(roles, new FakeUsers(), new FakeUnitOfWork());

            var result = await handler.HandleAsync(
                RoleName.Admin,
                "runs.read",
                TestContext.Current.CancellationToken
            );

            Assert.True(result.IsFailure);
            Assert.Equal(ErrorKind.Forbidden, result.Error.Kind);
            Assert.Equal("ROLE_PROTECTED", result.Error.Code);
            Assert.Contains(PermissionKey.From("runs.read"), admin.PermissionKeys);
        }

        [Fact]
        public void HandlersRejectNullPorts()
        {
            var users = new FakeUsers();
            var roles = new FakeRoles();
            var unitOfWork = new FakeUnitOfWork();
            var clock = new FakeTimeProvider(Now);
            var policy = new LoginPolicy(5, TimeSpan.FromMinutes(15));

            Assert.Throws<ArgumentNullException>(() =>
                new LoginHandler(null!, roles, new FakeTokens(), unitOfWork, policy, clock)
            );
            Assert.Throws<ArgumentNullException>(() =>
                new SetupPasswordHandler(null!, unitOfWork, clock)
            );
            Assert.Throws<ArgumentNullException>(() => new CreateUserHandler(users, null!, clock));
            Assert.Throws<ArgumentNullException>(() => new AssignRoleHandler(users, roles, null!));
            Assert.Throws<ArgumentNullException>(() => new RevokeRoleHandler(users, roles, null!));
            Assert.Throws<ArgumentNullException>(() => new CreateRoleHandler(null!, unitOfWork));
            Assert.Throws<ArgumentNullException>(() =>
                new GrantPermissionHandler(roles, new FakeUsers(), null!)
            );
            Assert.Throws<ArgumentNullException>(() =>
                new GrantPermissionHandler(roles, null!, new FakeUnitOfWork())
            );
            Assert.Throws<ArgumentNullException>(() =>
                new RevokePermissionHandler(roles, new FakeUsers(), null!)
            );
            Assert.Throws<ArgumentNullException>(() =>
                new RevokePermissionHandler(roles, null!, new FakeUnitOfWork())
            );
            Assert.Throws<ArgumentNullException>(() =>
                new RevokePermissionHandler(null!, new FakeUsers(), unitOfWork)
            );
        }
    }
}
