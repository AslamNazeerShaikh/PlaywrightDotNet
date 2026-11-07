using System.Threading;
using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.Identity;
using JobScrape.Application.Results;
using JobScrape.Domain.Identity;
using Microsoft.Extensions.Logging;
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
            private int _listCalls;
            private int _emailCalls;

            public int Saves { get; private set; }

            /// <summary>
            /// When set, the first two <see cref="GetByEmailAsync"/> calls
            /// await this gate before returning, so two handlers can both
            /// pass the pre-check in flight without sleeping.
            /// </summary>
            public TaskCompletionSource? EmailGate { get; set; }

            /// <summary>
            /// When set, the first <see cref="ListAsync"/> call awaits this gate
            /// before returning, so two handlers can be held in flight without
            /// sleeping.
            /// </summary>
            public TaskCompletionSource? ListGate { get; set; }

            public void Seed(User user)
            {
                _users.Add(user);
            }

            /// <summary>
            /// When true, <see cref="AddAsync"/> stages into a pending list
            /// invisible to reads, modelling uncommitted rows. Committed
            /// rows are the only ones queries return.
            /// </summary>
            public bool StageAdds { get; set; }

            private readonly List<User> _staged = [];

            public async Task<User?> GetByEmailAsync(
                string email,
                CancellationToken cancellationToken
            )
            {
                if (EmailGate is not null && Interlocked.Increment(ref _emailCalls) <= 2)
                {
                    await EmailGate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                }

                var normalized = email.Trim().ToLowerInvariant();
                return _users.FirstOrDefault(user =>
                    string.Equals(user.Email, normalized, StringComparison.OrdinalIgnoreCase)
                );
            }

            public Task<User?> GetAsync(UserId id, CancellationToken cancellationToken)
            {
                return Task.FromResult(_users.FirstOrDefault(user => user.Id == id));
            }

            public Task AddAsync(User user, CancellationToken cancellationToken)
            {
                (StageAdds ? _staged : _users).Add(user);
                return Task.CompletedTask;
            }

            public async Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken)
            {
                if (ListGate is not null && Interlocked.Increment(ref _listCalls) == 1)
                {
                    await ListGate.Task.ConfigureAwait(false);
                }

                return _users.ToList();
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

            /// When set, invoked with the 1-based save count. A returned
            /// exception is thrown instead of succeeding.
            public Func<int, Exception?>? SaveFailure { get; set; }

            /// Decides unique-violation classification for the handler.
            public Func<Exception, bool>? Unique { get; set; }

            public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
            {
                Saves++;
                var failure = SaveFailure?.Invoke(Saves);
                if (failure is not null)
                {
                    throw failure;
                }

                return Task.FromResult(0);
            }

            public bool IsUniqueViolation(Exception exception)
            {
                return Unique?.Invoke(exception) ?? false;
            }
        }

        private static LoginHandler Login(
            FakeUsers users,
            FakeRoles roles,
            FakeTokens tokens,
            FakeUnitOfWork unitOfWork,
            FakeTimeProvider clock,
            int maxAttempts = 5,
            Microsoft.Extensions.Logging.ILogger<LoginHandler>? logger = null
        )
        {
            return new LoginHandler(
                users,
                roles,
                tokens,
                unitOfWork,
                new LoginPolicy(maxAttempts, TimeSpan.FromMinutes(15)),
                clock,
                logger
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
        public async Task LoginBeforeFirstLoginFailsInvalidCredentials()
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
            Assert.Equal(ErrorKind.Unauthorized, result.Error.Kind);
            Assert.Equal("INVALID_CREDENTIALS", result.Error.Code);
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
        public async Task SetupPasswordCompletedAccountConflictsUnknownUnauthorized()
        {
            var ct = TestContext.Current.CancellationToken;

            var completedUsers = new FakeUsers();
            completedUsers.Seed(ActiveUser());
            var completedUnitOfWork = new FakeUnitOfWork();
            var completedHandler = new SetupPasswordHandler(
                completedUsers,
                completedUnitOfWork,
                new FakeTimeProvider(Now)
            );
            var completedResult = await completedHandler.HandleAsync(
                "op@example.com",
                FinalPassword,
                TempPassword,
                ct
            );

            var unknownUnitOfWork = new FakeUnitOfWork();
            var unknownHandler = new SetupPasswordHandler(
                new FakeUsers(),
                unknownUnitOfWork,
                new FakeTimeProvider(Now)
            );
            var unknownResult = await unknownHandler.HandleAsync(
                "ghost@example.com",
                "whatever",
                FinalPassword,
                ct
            );

            Assert.True(completedResult.IsFailure);
            Assert.Equal(ErrorKind.Conflict, completedResult.Error.Kind);
            Assert.Equal("PASSWORD_SETUP_COMPLETE", completedResult.Error.Code);
            Assert.Equal(0, completedUnitOfWork.Saves);

            Assert.True(unknownResult.IsFailure);
            Assert.Equal(ErrorKind.Unauthorized, unknownResult.Error.Kind);
            Assert.Equal("INVALID_CREDENTIALS", unknownResult.Error.Code);
            Assert.Equal(0, unknownUnitOfWork.Saves);
        }

        [Fact]
        public async Task SetupPasswordWithUnknownEmailFailsInvalidCredentials()
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
            Assert.Equal(ErrorKind.Unauthorized, result.Error.Kind);
            Assert.Equal("INVALID_CREDENTIALS", result.Error.Code);
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
            var roles = new FakeRoles();
            var viewer = Role.Create(RoleId.New(), RoleName.Viewer);
            roles.Seed(viewer);
            var unitOfWork = new FakeUnitOfWork();
            var handler = new CreateUserHandler(
                users,
                roles,
                unitOfWork,
                new FakeTimeProvider(Now)
            );

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
            Assert.Contains(viewer.Id, saved.Roles);
            Assert.Equal(1, unitOfWork.Saves);
        }

        [Fact]
        public async Task CreateUserWithoutViewerRoleFailsRoleNotFound()
        {
            var users = new FakeUsers();
            var roles = new FakeRoles();
            var unitOfWork = new FakeUnitOfWork();
            var handler = new CreateUserHandler(
                users,
                roles,
                unitOfWork,
                new FakeTimeProvider(Now)
            );

            var result = await handler.HandleAsync(
                "new@example.com",
                TempPassword,
                TestContext.Current.CancellationToken
            );

            Assert.Equal("ROLE_NOT_FOUND", result.Error.Code);
            Assert.Equal(ErrorKind.NotFound, result.Error.Kind);
            Assert.Equal(0, unitOfWork.Saves);
        }

        [Fact]
        public async Task CreateUserRethrowsNonUniqueSaveFailure()
        {
            var users = new FakeUsers();
            var roles = new FakeRoles();
            roles.Seed(Role.Create(RoleId.New(), RoleName.Viewer));
            var unitOfWork = new FakeUnitOfWork
            {
                SaveFailure = _ => new InvalidOperationException("disk full"),
                Unique = _ => false,
            };
            var handler = new CreateUserHandler(
                users,
                roles,
                unitOfWork,
                new FakeTimeProvider(Now)
            );

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handler.HandleAsync(
                    "new@example.com",
                    TempPassword,
                    TestContext.Current.CancellationToken
                )
            );
        }

        [Fact]
        public async Task CreateUserRethrowsUniqueViolationWithNoWinner()
        {
            // Staged adds stay invisible to reads, so the loser's own
            // uncommitted row cannot pose as the winner.
            var users = new FakeUsers { StageAdds = true };
            var roles = new FakeRoles();
            roles.Seed(Role.Create(RoleId.New(), RoleName.Viewer));
            var unitOfWork = new FakeUnitOfWork
            {
                SaveFailure = _ => new InvalidOperationException(
                    "UNIQUE constraint failed: users.email"
                ),
                Unique = _ => true,
            };
            var handler = new CreateUserHandler(
                users,
                roles,
                unitOfWork,
                new FakeTimeProvider(Now)
            );

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handler.HandleAsync(
                    "new@example.com",
                    TempPassword,
                    TestContext.Current.CancellationToken
                )
            );
        }

        [Fact]
        public async Task CreateUserRaceYieldsOneEmailTaken()
        {
            var users = new FakeUsers();
            var roles = new FakeRoles();
            roles.Seed(Role.Create(RoleId.New(), RoleName.Viewer));
            var unitOfWork = new FakeUnitOfWork();
            // The loser's save fails with a unique violation; the winner is
            // the user the first handler staged, found by re-query.
            unitOfWork.SaveFailure = saves =>
                saves >= 2
                    ? new InvalidOperationException("UNIQUE constraint failed: users.email")
                    : null;
            unitOfWork.Unique = _ => true;
            var clock = new FakeTimeProvider(Now);
            var firstHandler = new CreateUserHandler(users, roles, unitOfWork, clock);
            var secondHandler = new CreateUserHandler(users, roles, unitOfWork, clock);

            // Both handlers must pass the pre-check before either stages
            // its insert; otherwise the second fails before reaching save
            // and the conflict path stays untested.
            var emailGate = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
            users.EmailGate = emailGate;

            var firstTask = firstHandler.HandleAsync(
                "dupe@example.com",
                TempPassword,
                TestContext.Current.CancellationToken
            );
            var secondTask = secondHandler.HandleAsync(
                "dupe@example.com",
                TempPassword,
                TestContext.Current.CancellationToken
            );
            emailGate.SetResult();
            await Task.WhenAll(firstTask, secondTask);

            var results = new[] { firstTask.Result, secondTask.Result };
            Assert.Equal(1, results.Count(result => result.IsSuccess));
            Assert.Equal(
                1,
                results.Count(result => result.IsFailure && result.Error.Code == "EMAIL_TAKEN")
            );
        }

        [Fact]
        public async Task CreateUserWithTakenEmailFailsConflict()
        {
            var users = new FakeUsers();
            users.Seed(PendingUser());
            var roles = new FakeRoles();
            roles.Seed(Role.Create(RoleId.New(), RoleName.Viewer));
            var handler = new CreateUserHandler(
                users,
                roles,
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
                new FakeRoles(),
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
            var handler = new AssignRoleHandler(
                users,
                roles,
                unitOfWork,
                new CallerContext("admin", new List<string> { "Admin" }, Array.Empty<string>())
            );

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
            var handler = new AssignRoleHandler(
                users,
                roles,
                new FakeUnitOfWork(),
                new CallerContext("admin", new List<string> { "Admin" }, Array.Empty<string>())
            );

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
                new FakeUnitOfWork(),
                new CallerContext("admin", new List<string> { "Admin" }, Array.Empty<string>())
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
            var handler = new AssignRoleHandler(
                users,
                new FakeRoles(),
                new FakeUnitOfWork(),
                new CallerContext("admin", new List<string> { "Admin" }, Array.Empty<string>())
            );

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
            var handler = new RevokeRoleHandler(
                users,
                roles,
                new FakeUnitOfWork(),
                new CallerContext("admin", new List<string> { "Admin" }, Array.Empty<string>())
            );

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
            var handler = new RevokeRoleHandler(
                users,
                roles,
                new FakeUnitOfWork(),
                new CallerContext("admin", new List<string> { "Admin" }, Array.Empty<string>())
            );

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
        public async Task AssignRoleToSelfFailsAccessDenied()
        {
            var users = new FakeUsers();
            var user = ActiveUser();
            users.Seed(user);
            var roles = new FakeRoles();
            var admin = AdminRole();
            roles.Seed(admin);
            user.AssignRole(admin.Id);
            var caller = new CallerContext(
                user.Id.Value.ToString(),
                Array.Empty<string>(),
                Array.Empty<string>()
            );
            var handler = new AssignRoleHandler(users, roles, new FakeUnitOfWork(), caller);

            var result = await handler.HandleAsync(
                user.Id.Value,
                RoleName.Admin,
                TestContext.Current.CancellationToken
            );

            Assert.True(result.IsFailure);
            Assert.Equal(ErrorKind.Forbidden, result.Error.Kind);
            Assert.Equal("ACCESS_DENIED", result.Error.Code);
        }

        [Fact]
        public async Task RevokeRoleFromSelfFailsAccessDenied()
        {
            var users = new FakeUsers();
            var user = ActiveUser();
            users.Seed(user);
            var roles = new FakeRoles();
            var admin = AdminRole();
            roles.Seed(admin);
            user.AssignRole(admin.Id);
            var caller = new CallerContext(
                user.Id.Value.ToString(),
                Array.Empty<string>(),
                Array.Empty<string>()
            );
            var handler = new RevokeRoleHandler(users, roles, new FakeUnitOfWork(), caller);

            var result = await handler.HandleAsync(
                user.Id.Value,
                RoleName.Admin,
                TestContext.Current.CancellationToken
            );

            Assert.True(result.IsFailure);
            Assert.Equal(ErrorKind.Forbidden, result.Error.Kind);
            Assert.Equal("ACCESS_DENIED", result.Error.Code);
        }

        [Fact]
        public async Task SetupPasswordWithSameSecretFailsValidation()
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
                SeedSecret,
                TestContext.Current.CancellationToken
            );

            Assert.True(result.IsFailure);
            Assert.Equal(ErrorKind.Validation, result.Error.Kind);
            Assert.Equal("VALIDATION_FAILED", result.Error.Code);
            var fieldMap = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string[]>>(
                result.Error.Metadata!["errors"]
            );
            Assert.Contains("newPassword", fieldMap.Keys);
        }

        [Fact]
        public async Task ChangePasswordWithSameSecretFailsValidation()
        {
            var users = new FakeUsers();
            users.Seed(ActiveUser());
            var handler = new ChangePasswordHandler(users, new FakeUnitOfWork());

            var result = await handler.HandleAsync(
                "op@example.com",
                FinalPassword,
                FinalPassword,
                TestContext.Current.CancellationToken
            );

            Assert.True(result.IsFailure);
            Assert.Equal(ErrorKind.Validation, result.Error.Kind);
            Assert.Equal("VALIDATION_FAILED", result.Error.Code);
            var fieldMap = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string[]>>(
                result.Error.Metadata!["errors"]
            );
            Assert.Contains("newPassword", fieldMap.Keys);
        }

        [Fact]
        public async Task SetupPasswordOnLockedAccountWithWrongSecretIsUnauthorized()
        {
            var users = new FakeUsers();
            var user = PendingUser();
            user.RecordFailedLogin(Now, 1, TimeSpan.FromMinutes(15));
            users.Seed(user);
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

            Assert.True(result.IsFailure);
            Assert.Equal(ErrorKind.Unauthorized, result.Error.Kind);
            Assert.Equal("INVALID_CREDENTIALS", result.Error.Code);
        }

        [Fact]
        public async Task SetupPasswordOnLockedAccountWithCorrectSecretIsForbidden()
        {
            var users = new FakeUsers();
            var user = PendingUser();
            user.RecordFailedLogin(Now, 1, TimeSpan.FromMinutes(15));
            users.Seed(user);
            var handler = new SetupPasswordHandler(
                users,
                new FakeUnitOfWork(),
                new FakeTimeProvider(Now)
            );

            var result = await handler.HandleAsync(
                "op@example.com",
                SeedSecret,
                FinalPassword,
                TestContext.Current.CancellationToken
            );

            Assert.True(result.IsFailure);
            Assert.Equal(ErrorKind.Forbidden, result.Error.Kind);
            Assert.Equal("ACCOUNT_LOCKED", result.Error.Code);
        }

        [Fact]
        public async Task ConcurrentRevokeLeavesOneAdmin()
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
            var unitOfWork = new FakeUnitOfWork();
            var caller = new CallerContext(
                "admin",
                new List<string> { "Admin" },
                Array.Empty<string>()
            );
            var firstHandler = new RevokeRoleHandler(users, roles, unitOfWork, caller);
            var secondHandler = new RevokeRoleHandler(users, roles, unitOfWork, caller);
            var ct = TestContext.Current.CancellationToken;

            // Hold the first handler inside the guarded region until the second
            // has reached the semaphore, so both calls are genuinely in flight.
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            users.ListGate = gate;

            var firstTask = firstHandler.HandleAsync(first.Id.Value, RoleName.Admin, ct);
            var secondTask = secondHandler.HandleAsync(second.Id.Value, RoleName.Admin, ct);

            gate.SetResult();
            await Task.WhenAll(firstTask, secondTask);

            var results = new[] { firstTask.Result, secondTask.Result };
            Assert.Equal(1, results.Count(result => result.IsSuccess));
            Assert.Equal(
                1,
                results.Count(result =>
                    result.IsFailure && result.Error.Code == "LAST_ADMIN_REQUIRED"
                )
            );
        }

        [Fact]
        public async Task LoginPrunesUnknownRoleWithOneWarning()
        {
            var users = new FakeUsers();
            var user = ActiveUser();
            var unknownRoleId = RoleId.New();
            user.AssignRole(unknownRoleId);
            users.Seed(user);
            var roles = new FakeRoles();
            var tokens = new FakeTokens();
            var unitOfWork = new FakeUnitOfWork();
            var logger = new CapturingLogger();
            var handler = Login(
                users,
                roles,
                tokens,
                unitOfWork,
                new FakeTimeProvider(Now),
                logger: logger
            );

            var result = await handler.HandleAsync(
                "op@example.com",
                FinalPassword,
                TestContext.Current.CancellationToken
            );

            Assert.True(result.IsSuccess);
            Assert.DoesNotContain(unknownRoleId, user.Roles);
            Assert.Single(
                logger.Entries,
                entry =>
                    entry.Level == LogLevel.Warning
                    && entry.Message.Contains("Dropped unknown role", StringComparison.Ordinal)
            );
            Assert.Equal(1, unitOfWork.Saves);
        }

        [Fact]
        public async Task LoginSecondRunAfterPruneIsSilent()
        {
            var users = new FakeUsers();
            var user = ActiveUser();
            var unknownRoleId = RoleId.New();
            user.AssignRole(unknownRoleId);
            users.Seed(user);
            var roles = new FakeRoles();
            var tokens = new FakeTokens();
            var unitOfWork = new FakeUnitOfWork();
            var logger = new CapturingLogger();
            var handler = Login(
                users,
                roles,
                tokens,
                unitOfWork,
                new FakeTimeProvider(Now),
                logger: logger
            );
            var ct = TestContext.Current.CancellationToken;

            var first = await handler.HandleAsync("op@example.com", FinalPassword, ct);
            var second = await handler.HandleAsync("op@example.com", FinalPassword, ct);

            Assert.True(first.IsSuccess);
            Assert.True(second.IsSuccess);
            Assert.DoesNotContain(unknownRoleId, user.Roles);
            Assert.Single(logger.Entries);
        }

        [Fact]
        public async Task LoginDropsCatalogUnknownPermissionAndRetainsValidKeys()
        {
            var users = new FakeUsers();
            var user = ActiveUser();
            users.Seed(user);
            var roles = new FakeRoles();
            var role = Role.Create(RoleId.New(), RoleName.Viewer);
            role.Grant(PermissionKey.From("runs.read"));
            var badRole = Role.Create(RoleId.New(), RoleName.Operator);
            // `nope.zone` is syntactically valid but absent from the catalog,
            // so it models a key removed from the catalog after granting.
            var unknownKey = PermissionKey.From("nope.zone");
            badRole.Grant(unknownKey);
            badRole.Grant(PermissionKey.From("profiles.read"));
            roles.Seed(role);
            roles.Seed(badRole);
            user.AssignRole(role.Id);
            user.AssignRole(badRole.Id);
            var tokens = new FakeTokens();
            var unitOfWork = new FakeUnitOfWork();
            var logger = new CapturingLogger();
            var handler = Login(
                users,
                roles,
                tokens,
                unitOfWork,
                new FakeTimeProvider(Now),
                logger: logger
            );

            var result = await handler.HandleAsync(
                "op@example.com",
                FinalPassword,
                TestContext.Current.CancellationToken
            );

            Assert.True(result.IsSuccess);
            Assert.Contains(PermissionKey.From("runs.read"), tokens.SeenPermissions!);
            Assert.Contains(PermissionKey.From("profiles.read"), tokens.SeenPermissions!);
            Assert.DoesNotContain(unknownKey, tokens.SeenPermissions!);
            Assert.Single(
                logger.Entries,
                entry =>
                    entry.Level == LogLevel.Warning
                    && entry.Message.Contains(
                        "Dropped unknown permission",
                        StringComparison.Ordinal
                    )
            );
            Assert.Equal(1, unitOfWork.Saves);
        }

        internal sealed class CapturingLogger : ILogger<LoginHandler>
        {
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
                Entries.Add(
                    new LogEntry(logLevel, formatter(state, exception), properties, exception)
                );
            }
        }

        internal sealed record LogEntry(
            LogLevel Level,
            string Message,
            Dictionary<string, object?> Properties,
            Exception? Exception
        );

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
            Assert.Throws<ArgumentNullException>(() =>
                new CreateUserHandler(users, null!, unitOfWork, clock)
            );
            Assert.Throws<ArgumentNullException>(() =>
                new AssignRoleHandler(users, roles, null!, CallerContext.LocalOperator)
            );
            Assert.Throws<ArgumentNullException>(() =>
                new RevokeRoleHandler(users, roles, null!, CallerContext.LocalOperator)
            );
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
