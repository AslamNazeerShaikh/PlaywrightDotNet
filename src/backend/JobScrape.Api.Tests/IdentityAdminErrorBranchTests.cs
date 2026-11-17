using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace JobScrape.Api.Tests
{
    /// <summary>
    /// Error branches on the identity admin endpoints: null bodies, unknown
    /// role names, defensive model-state mapping, and the Admin-only gate on
    /// permission grants. Happy paths live in
    /// <see cref="IdentityAdminEndpointTests"/>.
    /// </summary>
    public sealed class IdentityAdminErrorBranchTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact]
        public async Task GetSeededRoleIs200WithItsName()
        {
            await using var factory = new ApiFactory();
            using var admin = await AuthTestHelper.CreateAdminClientAsync(factory, Ct);

            using var response = await admin.GetAsync("api/v1/roles/Admin", Ct);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("Admin", body.GetProperty("name").GetString());
        }

        [Fact]
        public async Task CreateRoleWithNullBodyIs400WithBodyError()
        {
            await using var factory = new ApiFactory();
            using var admin = await AuthTestHelper.CreateAdminClientAsync(factory, Ct);

            using var response = await admin.PostAsync(
                "api/v1/roles",
                new StringContent("null", Encoding.UTF8, "application/json"),
                Ct
            );

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("VALIDATION_FAILED", body.GetProperty("code").GetString());
            Assert.True(body.GetProperty("errors").TryGetProperty("body", out _));
        }

        [Fact]
        public async Task CreateRoleWithUnknownNameIs400WithNameError()
        {
            await using var factory = new ApiFactory();
            using var admin = await AuthTestHelper.CreateAdminClientAsync(factory, Ct);

            using var response = await admin.PostAsJsonAsync(
                "api/v1/roles",
                new { name = "Wizard" },
                Ct
            );

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("VALIDATION_FAILED", body.GetProperty("code").GetString());
            Assert.True(body.GetProperty("errors").TryGetProperty("name", out var errors));
            Assert.Contains(errors.EnumerateArray(), error =>
                error.GetString()!.Contains("unknown", StringComparison.Ordinal)
            );
        }

        [Fact]
        public async Task GrantPermissionWithNullBodyIs400WithBodyError()
        {
            await using var factory = new ApiFactory();
            using var admin = await AuthTestHelper.CreateAdminClientAsync(factory, Ct);

            using var response = await admin.PostAsync(
                "api/v1/roles/Operator/permissions",
                new StringContent("null", Encoding.UTF8, "application/json"),
                Ct
            );

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("VALIDATION_FAILED", body.GetProperty("code").GetString());
            Assert.True(body.GetProperty("errors").TryGetProperty("body", out _));
        }

        [Fact]
        public async Task GrantPermissionToUnknownRoleIs404()
        {
            await using var factory = new ApiFactory();
            using var admin = await AuthTestHelper.CreateAdminClientAsync(factory, Ct);

            using var response = await admin.PostAsJsonAsync(
                "api/v1/roles/Wizard/permissions",
                new { key = "runs.read" },
                Ct
            );

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("ROLE_NOT_FOUND", body.GetProperty("code").GetString());
        }

        [Fact]
        public async Task RevokePermissionFromUnknownRoleIs404()
        {
            await using var factory = new ApiFactory();
            using var admin = await AuthTestHelper.CreateAdminClientAsync(factory, Ct);

            using var response = await admin.DeleteAsync(
                "api/v1/roles/Wizard/permissions/runs.read",
                Ct
            );

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("ROLE_NOT_FOUND", body.GetProperty("code").GetString());
        }

        [Fact]
        public async Task RevokeGrantedPermissionIs200()
        {
            await using var factory = new ApiFactory();
            using var admin = await AuthTestHelper.CreateAdminClientAsync(factory, Ct);
            using var grant = await admin.PostAsJsonAsync(
                "api/v1/roles/Operator/permissions",
                new { key = "roles.write" },
                Ct
            );
            Assert.Equal(HttpStatusCode.OK, grant.StatusCode);

            using var response = await admin.DeleteAsync(
                "api/v1/roles/Operator/permissions/roles.write",
                Ct
            );

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task NonAdminCannotGrantOrRevokeAdminPermissions()
        {
            await using var factory = new ApiFactory();
            using var operatorClient = await OperatorWithRolesWriteAsync(factory);

            using var grant = await operatorClient.PostAsJsonAsync(
                "api/v1/roles/Admin/permissions",
                new { key = "users.write" },
                Ct
            );
            using var revoke = await operatorClient.DeleteAsync(
                "api/v1/roles/Admin/permissions/users.write",
                Ct
            );

            Assert.Equal(HttpStatusCode.Forbidden, grant.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, revoke.StatusCode);
            var grantBody = await grant.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("ACCESS_DENIED", grantBody.GetProperty("code").GetString());
        }

        [Fact]
        public async Task CreateUserWithNullBodyIs400WithBodyError()
        {
            await using var factory = new ApiFactory();
            using var admin = await AuthTestHelper.CreateAdminClientAsync(factory, Ct);

            using var response = await admin.PostAsync(
                "api/v1/users",
                new StringContent("null", Encoding.UTF8, "application/json"),
                Ct
            );

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("VALIDATION_FAILED", body.GetProperty("code").GetString());
            Assert.True(body.GetProperty("errors").TryGetProperty("body", out _));
        }

        [Fact]
        public async Task CreateUserWithInvalidEmailIs400WithFieldErrors()
        {
            await using var factory = new ApiFactory();
            using var admin = await AuthTestHelper.CreateAdminClientAsync(factory, Ct);

            using var response = await admin.PostAsJsonAsync(
                "api/v1/users",
                new { email = "not-an-email", temporaryPassword = "Temporary1!!" },
                Ct
            );

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("VALIDATION_FAILED", body.GetProperty("code").GetString());
            Assert.Equal(
                "The request contains invalid fields.",
                body.GetProperty("detail").GetString()
            );
            Assert.True(body.GetProperty("errors").TryGetProperty("Email", out _));
        }

        [Fact]
        public async Task AssignRoleWithNullBodyIs400WithBodyError()
        {
            await using var factory = new ApiFactory();
            using var admin = await AuthTestHelper.CreateAdminClientAsync(factory, Ct);

            using var response = await admin.PostAsync(
                $"api/v1/users/{Guid.NewGuid():D}/roles",
                new StringContent("null", Encoding.UTF8, "application/json"),
                Ct
            );

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("VALIDATION_FAILED", body.GetProperty("code").GetString());
            Assert.True(body.GetProperty("errors").TryGetProperty("body", out _));
        }

        [Fact]
        public async Task AssignUnknownRoleIs400WithRoleError()
        {
            await using var factory = new ApiFactory();
            using var admin = await AuthTestHelper.CreateAdminClientAsync(factory, Ct);

            using var response = await admin.PostAsJsonAsync(
                $"api/v1/users/{Guid.NewGuid():D}/roles",
                new { role = "Wizard" },
                Ct
            );

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("VALIDATION_FAILED", body.GetProperty("code").GetString());
            Assert.True(body.GetProperty("errors").TryGetProperty("role", out var errors));
            Assert.Contains(errors.EnumerateArray(), error =>
                error.GetString()!.Contains("unknown", StringComparison.Ordinal)
            );
        }

        [Fact]
        public async Task RevokeUnknownRoleIs400WithRoleError()
        {
            await using var factory = new ApiFactory();
            using var admin = await AuthTestHelper.CreateAdminClientAsync(factory, Ct);

            using var response = await admin.DeleteAsync(
                $"api/v1/users/{Guid.NewGuid():D}/roles/Wizard",
                Ct
            );

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("VALIDATION_FAILED", body.GetProperty("code").GetString());
            Assert.True(body.GetProperty("errors").TryGetProperty("role", out _));
        }

        [Fact]
        public async Task LoginWithNullBodyIs400WithBodyError()
        {
            await using var factory = new ApiFactory();
            using var anonymous = factory.CreateClient();

            using var response = await anonymous.PostAsync(
                "api/v1/auth/login",
                new StringContent("null", Encoding.UTF8, "application/json"),
                Ct
            );

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("VALIDATION_FAILED", body.GetProperty("code").GetString());
            Assert.True(body.GetProperty("errors").TryGetProperty("body", out _));
        }

        [Fact]
        public async Task SetupPasswordWithNullBodyIs400WithBodyError()
        {
            await using var factory = new ApiFactory();
            using var anonymous = factory.CreateClient();

            using var response = await anonymous.PostAsync(
                "api/v1/auth/setup-password",
                new StringContent("null", Encoding.UTF8, "application/json"),
                Ct
            );

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("VALIDATION_FAILED", body.GetProperty("code").GetString());
            Assert.True(body.GetProperty("errors").TryGetProperty("body", out _));
        }

        [Fact]
        public async Task ChangePasswordWithNullBodyIs400WithBodyError()
        {
            await using var factory = new ApiFactory();
            using var admin = await AuthTestHelper.CreateAdminClientAsync(factory, Ct);

            using var response = await admin.PostAsync(
                "api/v1/auth/change-password",
                new StringContent("null", Encoding.UTF8, "application/json"),
                Ct
            );

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("VALIDATION_FAILED", body.GetProperty("code").GetString());
            Assert.True(body.GetProperty("errors").TryGetProperty("body", out _));
        }

        [Fact]
        public async Task ChangePasswordWithMissingFieldsIs400WithFieldErrors()
        {
            await using var factory = new ApiFactory();
            using var admin = await AuthTestHelper.CreateAdminClientAsync(factory, Ct);

            using var response = await admin.PostAsJsonAsync(
                "api/v1/auth/change-password",
                new { },
                Ct
            );

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("VALIDATION_FAILED", body.GetProperty("code").GetString());
            Assert.Equal(
                "The request contains invalid fields.",
                body.GetProperty("detail").GetString()
            );
        }

        /// <summary>
        /// Creates an Operator account carrying <c>roles.write</c> and signs it
        /// in, so the Admin-only permission gate is reachable without Admin.
        /// </summary>
        /// <param name="factory">The app under test.</param>
        /// <returns>An authenticated Operator client.</returns>
        private static async Task<HttpClient> OperatorWithRolesWriteAsync(ApiFactory factory)
        {
            using var admin = await AuthTestHelper.CreateAdminClientAsync(factory, Ct);
            using var grant = await admin.PostAsJsonAsync(
                "api/v1/roles/Operator/permissions",
                new { key = "roles.write" },
                Ct
            );
            grant.EnsureSuccessStatusCode();
            var email = $"operator-{Guid.NewGuid():N}@example.com";
            const string temporary = "Temporary1!!";
            using var created = await admin.PostAsJsonAsync(
                "api/v1/users",
                new { email, temporaryPassword = temporary },
                Ct
            );
            created.EnsureSuccessStatusCode();
            var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>(Ct);
            var userId = createdBody.GetProperty("id").GetGuid();
            using var assigned = await admin.PostAsJsonAsync(
                $"api/v1/users/{userId:D}/roles",
                new { role = "Operator" },
                Ct
            );
            assigned.EnsureSuccessStatusCode();
            using var anonymous = factory.CreateClient();
            using var setup = await anonymous.PostAsJsonAsync(
                "api/v1/auth/setup-password",
                new
                {
                    email,
                    currentSecret = temporary,
                    newPassword = "Operator-Password2@@",
                },
                Ct
            );
            setup.EnsureSuccessStatusCode();
            return await AuthTestHelper.LoginAsync(factory, email, "Operator-Password2@@", Ct);
        }
    }
}
