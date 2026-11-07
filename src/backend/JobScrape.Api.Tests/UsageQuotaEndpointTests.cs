using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace JobScrape.Api.Tests
{
    /// <summary>
    /// The usage quota edge over HTTP: seeded defaults, admin updates, the
    /// fixed-window 429 with its stable code, admin-only gating, and the
    /// health plus auth exemptions.
    /// </summary>
    public sealed class UsageQuotaEndpointTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private static async Task<HttpClient> CreateViewerClientAsync(
            ApiFactory factory,
            CancellationToken cancellationToken
        )
        {
            using var admin = await factory.CreateAdminClientAsync(cancellationToken);
            var email = $"viewer-{Guid.NewGuid():N}@example.com";

            using (
                var created = await admin.PostAsJsonAsync(
                    "api/v1/users",
                    new { email, temporaryPassword = "Temp-Secret1!!" },
                    cancellationToken
                )
            )
            {
                created.EnsureSuccessStatusCode();
            }

            using var setupClient = factory.CreateClient();
            using (
                var setup = await setupClient.PostAsJsonAsync(
                    "api/v1/auth/setup-password",
                    new
                    {
                        email,
                        currentSecret = "Temp-Secret1!!",
                        newPassword = "Final-Password2@@",
                    },
                    cancellationToken
                )
            )
            {
                setup.EnsureSuccessStatusCode();
            }

            return await AuthTestHelper.LoginAsync(
                factory,
                email,
                "Final-Password2@@",
                cancellationToken
            );
        }

        [Fact]
        public async Task AdminListsSeededDefaults()
        {
            await using var factory = new ApiFactory();
            using var admin = await factory.CreateAdminClientAsync(Ct);

            using var response = await admin.GetAsync("api/v1/quotas", Ct);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            var quotas = body.GetProperty("quotas").EnumerateArray().ToList();
            Assert.Equal(2, quotas.Count);
            var api = quotas.Single(quota => quota.GetProperty("scope").GetString() == "Api");
            Assert.Equal(10000, api.GetProperty("limit").GetInt32());
            Assert.Equal(
                TimeSpan.FromHours(24),
                TimeSpan.Parse(api.GetProperty("window").GetString()!, CultureInfo.InvariantCulture)
            );
            var mcp = quotas.Single(quota => quota.GetProperty("scope").GetString() == "Mcp");
            Assert.Equal(1000, mcp.GetProperty("limit").GetInt32());
        }

        [Fact]
        public async Task AdminLowersLimitThenSecondCallIs429()
        {
            await using var factory = new ApiFactory();
            using var admin = await factory.CreateAdminClientAsync(Ct);

            using var updated = await admin.PutAsJsonAsync(
                "api/v1/quotas/Api",
                new { limit = 1, window = "00:01:00" },
                Ct
            );
            Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

            using var response = await admin.GetAsync("api/v1/users", Ct);

            Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
            Assert.Equal(
                "application/problem+json",
                response.Content.Headers.ContentType!.MediaType
            );
            Assert.True(response.Headers.Contains("Retry-After"));
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("QUOTA_EXCEEDED", body.GetProperty("code").GetString());
        }

        [Fact]
        public async Task ExhaustedQuotaLeavesHealthAndAuthExempt()
        {
            await using var factory = new ApiFactory();
            using var admin = await factory.CreateAdminClientAsync(Ct);
            using (
                var updated = await admin.PutAsJsonAsync(
                    "api/v1/quotas/Api",
                    new { limit = 1, window = "00:01:00" },
                    Ct
                )
            )
            {
                Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
            }

            using (var exhausted = await admin.GetAsync("api/v1/users", Ct))
            {
                Assert.Equal(HttpStatusCode.TooManyRequests, exhausted.StatusCode);
            }

            using var anonymous = factory.CreateClient();
            using var health = await anonymous.GetAsync("api/v1/health", Ct);
            Assert.Equal(HttpStatusCode.OK, health.StatusCode);

            using var login = await anonymous.PostAsJsonAsync(
                "api/v1/auth/login",
                new { email = "nobody@example.com", password = "Wrong-Password1!!" },
                Ct
            );
            Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
            var body = await login.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("INVALID_CREDENTIALS", body.GetProperty("code").GetString());
        }

        [Fact]
        public async Task NonAdminCannotReachQuotas()
        {
            await using var factory = new ApiFactory();
            using var viewer = await CreateViewerClientAsync(factory, Ct);

            using var listed = await viewer.GetAsync("api/v1/quotas", Ct);
            Assert.Equal(HttpStatusCode.Forbidden, listed.StatusCode);
            var listedBody = await listed.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("ACCESS_DENIED", listedBody.GetProperty("code").GetString());

            using var updated = await viewer.PutAsJsonAsync(
                "api/v1/quotas/Api",
                new { limit = 5, window = "00:01:00" },
                Ct
            );
            Assert.Equal(HttpStatusCode.Forbidden, updated.StatusCode);
        }

        [Fact]
        public async Task UpdateUnknownScopeIs404()
        {
            await using var factory = new ApiFactory();
            using var admin = await factory.CreateAdminClientAsync(Ct);

            using var response = await admin.PutAsJsonAsync(
                "api/v1/quotas/Nope",
                new { limit = 5, window = "00:01:00" },
                Ct
            );

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("UNKNOWN_QUOTA_SCOPE", body.GetProperty("code").GetString());
        }

        [Fact]
        public async Task UpdateWithBadLimitIs400()
        {
            await using var factory = new ApiFactory();
            using var admin = await factory.CreateAdminClientAsync(Ct);

            using var response = await admin.PutAsJsonAsync(
                "api/v1/quotas/Api",
                new { limit = 0, window = "00:01:00" },
                Ct
            );

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("VALIDATION_FAILED", body.GetProperty("code").GetString());
        }

        [Fact]
        public async Task UpdateWithBadWindowIs400()
        {
            await using var factory = new ApiFactory();
            using var admin = await factory.CreateAdminClientAsync(Ct);

            using var response = await admin.PutAsJsonAsync(
                "api/v1/quotas/Api",
                new { limit = 5, window = "not-a-window" },
                Ct
            );

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("VALIDATION_FAILED", body.GetProperty("code").GetString());
        }
    }
}
