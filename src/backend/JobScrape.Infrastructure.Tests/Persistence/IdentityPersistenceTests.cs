using JobScrape.Application.Abstractions;
using JobScrape.Domain;
using JobScrape.Domain.Identity;
using JobScrape.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JobScrape.Infrastructure.Tests.Persistence
{
    [Trait("Category", "Integration")]
    public sealed class IdentityPersistenceTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 6, 8, 0, 0, TimeSpan.Zero);

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private static User SeededUser(string email = "op@example.com")
        {
            var user = User.Create(email, PasswordHash.Create("Seed-Secret1!!"), Now);
            for (var attempt = 0; attempt < 5; attempt++)
            {
                user.RecordFailedLogin(Now, 5, TimeSpan.FromMinutes(15));
            }

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

        [Fact]
        public async Task UserRoundTripsEveryField()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            var admin = AdminRole();
            var user = SeededUser();
            user.AssignRole(admin.Id);
            await using (var scope = database.Scope())
            {
                await scope.Get<IRoleRepository>().AddAsync(admin, Ct);
                await scope.Get<IUserRepository>().AddAsync(user, Ct);
                await scope.Get<IUnitOfWork>().SaveChangesAsync(Ct);
            }

            await using var reload = database.Scope();
            var loaded = await reload.Get<IUserRepository>().GetByEmailAsync("OP@EXAMPLE.COM", Ct);

            Assert.NotNull(loaded);
            Assert.Equal(user.Id, loaded.Id);
            Assert.Equal("op@example.com", loaded.Email);
            Assert.True(loaded.PasswordHash.Verify("Seed-Secret1!!"));
            Assert.True(loaded.MustChangePassword);
            Assert.Equal(5, loaded.FailedAttempts);
            Assert.Equal(Now.AddMinutes(15), loaded.LockedUntilUtc);
            Assert.Equal(Now, loaded.CreatedAt);
            Assert.Equal([admin.Id], loaded.Roles);
            Assert.Null(await reload.Get<IUserRepository>().GetAsync(UserId.New(), Ct));
        }

        [Fact]
        public async Task RoleAssignmentPersistsAcrossRoleReload()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            var user = SeededUser();
            var role = Role.Create(RoleId.New(), RoleName.Viewer);
            role.Grant(PermissionKey.From("runs.read"));
            user.AssignRole(role.Id);
            await using (var scope = database.Scope())
            {
                await scope.Get<IRoleRepository>().AddAsync(role, Ct);
                await scope.Get<IUserRepository>().AddAsync(user, Ct);
                await scope.Get<IUnitOfWork>().SaveChangesAsync(Ct);
            }

            await using (var scope = database.Scope())
            {
                var loadedRole = await scope.Get<IRoleRepository>().GetByNameAsync(RoleName.Viewer, Ct);
                Assert.NotNull(loadedRole);
                Assert.Equal([PermissionKey.From("runs.read")], loadedRole.PermissionKeys);
                loadedRole.Grant(PermissionKey.From("profiles.read"));
                loadedRole.Revoke(PermissionKey.From("runs.read"));
                await scope.Get<IUnitOfWork>().SaveChangesAsync(Ct);
            }

            await using var reload = database.Scope();
            var reloaded = await reload.Get<IRoleRepository>().GetByNameAsync(RoleName.Viewer, Ct);

            Assert.NotNull(reloaded);
            Assert.Equal([PermissionKey.From("profiles.read")], reloaded.PermissionKeys);
            Assert.Null(await reload.Get<IRoleRepository>().GetByNameAsync(RoleName.Admin, Ct));
            Assert.Null(await reload.Get<IUserRepository>().GetByEmailAsync("missing@example.com", Ct));
        }

        [Fact]
        public async Task EmailsAreUnique()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            await using var scope = database.Scope();
            var users = scope.Get<IUserRepository>();
            await users.AddAsync(SeededUser("same@example.com"), Ct);
            await users.AddAsync(SeededUser("SAME@example.com"), Ct);

            await Assert.ThrowsAsync<DbUpdateException>(() =>
                scope.Get<IUnitOfWork>().SaveChangesAsync(Ct)
            );
        }

        [Fact]
        public async Task RoleNamesAreUnique()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            await using var scope = database.Scope();
            await scope.Get<IRoleRepository>().AddAsync(AdminRole(), Ct);
            await scope.Get<IRoleRepository>().AddAsync(AdminRole(), Ct);

            await Assert.ThrowsAsync<DbUpdateException>(() =>
                scope.Get<IUnitOfWork>().SaveChangesAsync(Ct)
            );
        }

        [Fact]
        public async Task CorruptPasswordHashFailsLoudlyOnLoad()
        {
            await using var database = await TestDatabase.CreateAsync(Ct);
            await using (var scope = database.Scope())
            {
                await scope.Get<IUserRepository>().AddAsync(SeededUser(), Ct);
                await scope.Get<IUnitOfWork>().SaveChangesAsync(Ct);
            }

            await using (var connection = new Microsoft.Data.Sqlite.SqliteConnection(database.ConnectionString))
            {
                await connection.OpenAsync(Ct);
                await using var command = connection.CreateCommand();
                command.CommandText = "UPDATE users SET password_hash = 'bogus';";
                await command.ExecuteNonQueryAsync(Ct);
            }

            await using var reload = database.Scope();
            await Assert.ThrowsAsync<System.Text.Json.JsonException>(() =>
                reload.Db.Users.ToListAsync(Ct)
            );
        }
    }
}
