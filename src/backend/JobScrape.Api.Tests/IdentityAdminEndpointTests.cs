using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
            var unknownBody = await unknown.Content.ReadFromJsonAsync<JsonElement>(Ct);

            Assert.Equal(HttpStatusCode.Conflict, unknown.StatusCode);
            Assert.Equal("PASSWORD_SETUP_COMPLETE", unknownBody.GetProperty("code").GetString());
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
