using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JobScrape.Application.Abstractions;
using JobScrape.Domain.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace JobScrape.Api.Tests
{
    /// <summary>
    /// The admin-identity error branches over HTTP: wrong current password
    /// on change, unknown user and role reads, and duplicate user creation.
    /// Happy paths live in <see cref="AuthEndpointTests"/>.
    /// </summary>
    public sealed class IdentityAdminEndpointTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact]
        public async Task ChangePasswordWithWrongCurrentIs401()
        {
            await using var factory = new ApiFactory();
            using var admin = await AuthTestHelper.CreateAdminClientAsync(factory, Ct);

            using var response = await admin.PostAsJsonAsync(
                "api/v1/auth/change-password",
                new { currentPassword = "Wrong-Password1!!", newPassword = "New-Password3##" },
                Ct
            );

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("INVALID_CREDENTIALS", body.GetProperty("code").GetString());
        }

        [Fact]
        public async Task SetupPasswordWithWeakNewPasswordIs400()
        {
            await using var factory = new ApiFactory();
            using var admin = await AuthTestHelper.CreateAdminClientAsync(factory, Ct);
            var email = $"weak-{Guid.NewGuid():N}@example.com";
            const string temporary = "Temporary1!!";
            using var created = await admin.PostAsJsonAsync(
                "api/v1/users",
                new { email, temporaryPassword = temporary },
                Ct
            );
            created.EnsureSuccessStatusCode();
            using var anonymous = factory.CreateClient();

            using var response = await anonymous.PostAsJsonAsync(
                "api/v1/auth/setup-password",
                new
                {
                    email,
                    currentSecret = temporary,
                    newPassword = "short",
                },
                Ct
            );

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task RevokedAdminTokenIsDenied()
        {
            await using var factory = new ApiFactory();
            using var admin = await AuthTestHelper.CreateAdminClientAsync(factory, Ct);
            var email = $"second-{Guid.NewGuid():N}@example.com";
            const string temporary = "Temporary1!!";
            using var created = await admin.PostAsJsonAsync(
                "api/v1/users",
                new { email, temporaryPassword = temporary },
                Ct
            );
            created.EnsureSuccessStatusCode();
            var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>(Ct);
            var secondId = createdBody.GetProperty("id").GetGuid();
            using var anonymous = factory.CreateClient();
            using var setup = await anonymous.PostAsJsonAsync(
                "api/v1/auth/setup-password",
                new
                {
                    email,
                    currentSecret = temporary,
                    newPassword = "Second-Password2@@",
                },
                Ct
            );
            setup.EnsureSuccessStatusCode();
            var setupBody = await setup.Content.ReadFromJsonAsync<JsonElement>(Ct);
            var secondToken = setupBody.GetProperty("token").GetString();
            using var second = factory.CreateClient();
            second.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", secondToken);
            using var assigned = await admin.PostAsJsonAsync(
                $"api/v1/users/{secondId:D}/roles",
                new { role = "Admin" },
                Ct
            );
            assigned.EnsureSuccessStatusCode();
            using var secondFresh = await AuthTestHelper.LoginAsync(
                factory,
                email,
                "Second-Password2@@",
                Ct
            );
            using var users = await admin.GetAsync("api/v1/users", Ct);
            users.EnsureSuccessStatusCode();
            var list = await users.Content.ReadFromJsonAsync<JsonElement>(Ct);
            var firstId = list.GetProperty("users")
                .EnumerateArray()
                .Select(user => user.GetProperty("id").GetGuid())
                .First(id => id != secondId);
            using var revoked = await secondFresh.SendAsync(
                new HttpRequestMessage(HttpMethod.Delete, $"api/v1/users/{firstId:D}/roles/Admin"),
                Ct
            );
            revoked.EnsureSuccessStatusCode();

            using var response = await admin.GetAsync("api/v1/users", Ct);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task SetupOracleDoesNotDistinguishUnknownEmail()
        {
            await using var factory = new ApiFactory();
            using var anonymous = factory.CreateClient();
            var pendingEmail = $"pending-{Guid.NewGuid():N}@example.com";
            const string temporary = "Temporary1!!";
            using var created = await (
                await AuthTestHelper.CreateAdminClientAsync(factory, Ct)
            ).PostAsJsonAsync(
                "api/v1/users",
                new { email = pendingEmail, temporaryPassword = temporary },
                Ct
            );
            created.EnsureSuccessStatusCode();

            using var unknown = await anonymous.PostAsJsonAsync(
                "api/v1/auth/setup-password",
                new
                {
                    email = $"ghost-{Guid.NewGuid():N}@example.com",
                    currentSecret = "whatever",
                    newPassword = "New-Password3##",
                },
                Ct
            );
            using var wrongSecret = await anonymous.PostAsJsonAsync(
                "api/v1/auth/setup-password",
                new
                {
                    email = pendingEmail,
                    currentSecret = "wrong-secret",
                    newPassword = "New-Password3##",
                },
                Ct
            );

            var unknownBody = await unknown.Content.ReadFromJsonAsync<JsonElement>(Ct);
            var wrongBody = await wrongSecret.Content.ReadFromJsonAsync<JsonElement>(Ct);

            Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
            Assert.Equal(unknown.StatusCode, wrongSecret.StatusCode);
            Assert.Equal("INVALID_CREDENTIALS", unknownBody.GetProperty("code").GetString());
            Assert.Equal(
                unknownBody.GetProperty("code").GetString(),
                wrongBody.GetProperty("code").GetString()
            );
            Assert.Equal(
                unknownBody.GetProperty("title").GetString(),
                wrongBody.GetProperty("title").GetString()
            );
            Assert.True(unknownBody.TryGetProperty("traceId", out _));
            Assert.True(wrongBody.TryGetProperty("traceId", out _));
        }

        [Fact]
        public async Task SetupPasswordWithSameSecretIs400()
        {
            await using var factory = new ApiFactory();
            using var admin = await AuthTestHelper.CreateAdminClientAsync(factory, Ct);
            var email = $"same-{Guid.NewGuid():N}@example.com";
            const string temporary = "Temporary1!!";
            using var created = await admin.PostAsJsonAsync(
                "api/v1/users",
                new { email, temporaryPassword = temporary },
                Ct
            );
            created.EnsureSuccessStatusCode();
            using var anonymous = factory.CreateClient();

            using var response = await anonymous.PostAsJsonAsync(
                "api/v1/auth/setup-password",
                new
                {
                    email,
                    currentSecret = temporary,
                    newPassword = temporary,
                },
                Ct
            );

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("VALIDATION_FAILED", body.GetProperty("code").GetString());
        }

        [Fact]
        public async Task ChangePasswordWithSameNewPasswordIs400()
        {
            await using var factory = new ApiFactory();
            using var admin = await AuthTestHelper.CreateAdminClientAsync(factory, Ct);

            using var response = await admin.PostAsJsonAsync(
                "api/v1/auth/change-password",
                new { currentPassword = "Final-Password2@@", newPassword = "Final-Password2@@" },
                Ct
            );

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("VALIDATION_FAILED", body.GetProperty("code").GetString());
        }

        [Fact]
        public async Task SetupPasswordOnLockedAccountWithCorrectSecretIs403()
        {
            await using var factory = new ApiFactory();
            var email = $"locked-{Guid.NewGuid():N}@example.com";
            const string temporary = "Temporary1!!";
            using (var scope = factory.Services.CreateScope())
            {
                var provider = scope.ServiceProvider;
                var clock = provider.GetRequiredService<TimeProvider>();
                var users = provider.GetRequiredService<IUserRepository>();
                var user = User.Create(email, PasswordHash.Create(temporary), clock.GetUtcNow());
                user.RecordFailedLogin(clock.GetUtcNow(), 1, TimeSpan.FromMinutes(15));
                await users.AddAsync(user, Ct);
                await provider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
            }
            using var anonymous = factory.CreateClient();

            using var response = await anonymous.PostAsJsonAsync(
                "api/v1/auth/setup-password",
                new
                {
                    email,
                    currentSecret = temporary,
                    newPassword = "New-Password3##",
                },
                Ct
            );

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("ACCOUNT_LOCKED", body.GetProperty("code").GetString());
        }

        [Fact]
        public async Task AssignRoleToSelfIs403()
        {
            await using var factory = new ApiFactory();
            using var admin = await AuthTestHelper.CreateAdminClientAsync(factory, Ct);
            var email = $"self-{Guid.NewGuid():N}@example.com";
            const string temporary = "Temporary1!!";
            using var created = await admin.PostAsJsonAsync(
                "api/v1/users",
                new { email, temporaryPassword = temporary },
                Ct
            );
            created.EnsureSuccessStatusCode();
            var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>(Ct);
            var userId = createdBody.GetProperty("id").GetGuid();
            using var anonymous = factory.CreateClient();
            using var setup = await anonymous.PostAsJsonAsync(
                "api/v1/auth/setup-password",
                new
                {
                    email,
                    currentSecret = temporary,
                    newPassword = "Self-Password2@@",
                },
                Ct
            );
            setup.EnsureSuccessStatusCode();
            using var assigned = await admin.PostAsJsonAsync(
                $"api/v1/users/{userId:D}/roles",
                new { role = "Admin" },
                Ct
            );
            assigned.EnsureSuccessStatusCode();
            using var self = await AuthTestHelper.LoginAsync(
                factory,
                email,
                "Self-Password2@@",
                Ct
            );

            using var response = await self.PostAsJsonAsync(
                $"api/v1/users/{userId:D}/roles",
                new { role = "Admin" },
                Ct
            );

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("ACCESS_DENIED", body.GetProperty("code").GetString());
        }

        [Fact]
        public async Task RevokeRoleFromSelfIs403()
        {
            await using var factory = new ApiFactory();
            using var admin = await AuthTestHelper.CreateAdminClientAsync(factory, Ct);
            var email = $"selfrevoke-{Guid.NewGuid():N}@example.com";
            const string temporary = "Temporary1!!";
            using var created = await admin.PostAsJsonAsync(
                "api/v1/users",
                new { email, temporaryPassword = temporary },
                Ct
            );
            created.EnsureSuccessStatusCode();
            var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>(Ct);
            var userId = createdBody.GetProperty("id").GetGuid();
            using var anonymous = factory.CreateClient();
            using var setup = await anonymous.PostAsJsonAsync(
                "api/v1/auth/setup-password",
                new
                {
                    email,
                    currentSecret = temporary,
                    newPassword = "Self-Password2@@",
                },
                Ct
            );
            setup.EnsureSuccessStatusCode();
            using var assigned = await admin.PostAsJsonAsync(
                $"api/v1/users/{userId:D}/roles",
                new { role = "Admin" },
                Ct
            );
            assigned.EnsureSuccessStatusCode();
            using var self = await AuthTestHelper.LoginAsync(
                factory,
                email,
                "Self-Password2@@",
                Ct
            );

            using var response = await self.DeleteAsync($"api/v1/users/{userId:D}/roles/Admin", Ct);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("ACCESS_DENIED", body.GetProperty("code").GetString());
        }

        [Fact]
        public async Task NonAdminWithUsersWriteCanAssignOperatorButNotAdmin()
        {
            await using var factory = new ApiFactory();
            using var admin = await AuthTestHelper.CreateAdminClientAsync(factory, Ct);

            foreach (var roleName in new[] { "Operator", "Viewer" })
            {
                using var seed = await admin.PostAsJsonAsync(
                    "api/v1/roles",
                    new { name = roleName },
                    Ct
                );
                Assert.True(
                    seed.StatusCode == HttpStatusCode.Created
                        || seed.StatusCode == HttpStatusCode.Conflict
                );
            }

            using var grant = await admin.PostAsJsonAsync(
                "api/v1/roles/Operator/permissions",
                new { key = "users.write" },
                Ct
            );
            Assert.True(
                grant.StatusCode == HttpStatusCode.OK || grant.StatusCode == HttpStatusCode.Conflict
            );

            var callerEmail = $"caller-{Guid.NewGuid():N}@example.com";
            const string temporary = "Temporary1!!";
            using var createdCaller = await admin.PostAsJsonAsync(
                "api/v1/users",
                new { email = callerEmail, temporaryPassword = temporary },
                Ct
            );
            createdCaller.EnsureSuccessStatusCode();
            var createdCallerBody = await createdCaller.Content.ReadFromJsonAsync<JsonElement>(Ct);
            var callerId = createdCallerBody.GetProperty("id").GetGuid();

            using var assignedCallerRole = await admin.PostAsJsonAsync(
                $"api/v1/users/{callerId:D}/roles",
                new { role = "Operator" },
                Ct
            );
            assignedCallerRole.EnsureSuccessStatusCode();

            using var anonymous = factory.CreateClient();
            using var setup = await anonymous.PostAsJsonAsync(
                "api/v1/auth/setup-password",
                new
                {
                    email = callerEmail,
                    currentSecret = temporary,
                    newPassword = "Caller-Password2@@",
                },
                Ct
            );
            setup.EnsureSuccessStatusCode();

            using var caller = await AuthTestHelper.LoginAsync(
                factory,
                callerEmail,
                "Caller-Password2@@",
                Ct
            );

            var targetEmail = $"target-{Guid.NewGuid():N}@example.com";
            using var createdTarget = await caller.PostAsJsonAsync(
                "api/v1/users",
                new { email = targetEmail, temporaryPassword = temporary },
                Ct
            );
            Assert.Equal(HttpStatusCode.Created, createdTarget.StatusCode);
            var createdTargetBody = await createdTarget.Content.ReadFromJsonAsync<JsonElement>(Ct);
            var targetId = createdTargetBody.GetProperty("id").GetGuid();

            using var adminAssign = await caller.PostAsJsonAsync(
                $"api/v1/users/{targetId:D}/roles",
                new { role = "Admin" },
                Ct
            );
            Assert.Equal(HttpStatusCode.Forbidden, adminAssign.StatusCode);
            var adminAssignBody = await adminAssign.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("ACCESS_DENIED", adminAssignBody.GetProperty("code").GetString());

            using var operatorAssign = await caller.PostAsJsonAsync(
                $"api/v1/users/{targetId:D}/roles",
                new { role = "Operator" },
                Ct
            );
            Assert.Equal(HttpStatusCode.OK, operatorAssign.StatusCode);
        }

        [Fact]
        public async Task OldTokenIsUnauthorizedAfterRoleGrant()
        {
            await using var factory = new ApiFactory();
            using var admin = await AuthTestHelper.CreateAdminClientAsync(factory, Ct);
            var email = $"stale-{Guid.NewGuid():N}@example.com";
            const string temporary = "Temporary1!!";
            using var created = await admin.PostAsJsonAsync(
                "api/v1/users",
                new { email, temporaryPassword = temporary },
                Ct
            );
            created.EnsureSuccessStatusCode();
            var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>(Ct);
            var userId = createdBody.GetProperty("id").GetGuid();
            using var anonymous = factory.CreateClient();
            using var setup = await anonymous.PostAsJsonAsync(
                "api/v1/auth/setup-password",
                new
                {
                    email,
                    currentSecret = temporary,
                    newPassword = "Stale-Password2@@",
                },
                Ct
            );
            setup.EnsureSuccessStatusCode();
            var setupBody = await setup.Content.ReadFromJsonAsync<JsonElement>(Ct);
            var oldToken = setupBody.GetProperty("token").GetString()!;
            using var oldClient = factory.CreateClient();
            oldClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", oldToken);

            using var granted = await admin.PostAsJsonAsync(
                $"api/v1/users/{userId:D}/roles",
                new { role = "Admin" },
                Ct
            );
            granted.EnsureSuccessStatusCode();

            using var response = await oldClient.GetAsync("api/v1/auth/me", Ct);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("UNAUTHENTICATED", body.GetProperty("code").GetString());
        }

        [Fact]
        public async Task GetUnknownUserIs404WithCode()
        {
            await using var factory = new ApiFactory();
            using var admin = await AuthTestHelper.CreateAdminClientAsync(factory, Ct);

            using var response = await admin.GetAsync($"api/v1/users/{Guid.NewGuid():D}", Ct);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("USER_NOT_FOUND", body.GetProperty("code").GetString());
        }

        [Fact]
        public async Task GetUnknownRoleIs404WithCode()
        {
            await using var factory = new ApiFactory();
            using var admin = await AuthTestHelper.CreateAdminClientAsync(factory, Ct);

            using var response = await admin.GetAsync("api/v1/roles/no-such-role", Ct);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("ROLE_NOT_FOUND", body.GetProperty("code").GetString());
        }

        [Fact]
        public async Task CreateDuplicateUserIs409WithCode()
        {
            await using var factory = new ApiFactory();
            using var admin = await AuthTestHelper.CreateAdminClientAsync(factory, Ct);
            var email = $"dup-{Guid.NewGuid():N}@example.com";
            using var first = await admin.PostAsJsonAsync(
                "api/v1/users",
                new { email, temporaryPassword = "Temporary1!!" },
                Ct
            );
            first.EnsureSuccessStatusCode();

            using var response = await admin.PostAsJsonAsync(
                "api/v1/users",
                new { email, temporaryPassword = "Temporary1!!" },
                Ct
            );

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("EMAIL_TAKEN", body.GetProperty("code").GetString());
        }
    }
}
