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

        private static async Task<HttpResponseMessage> PostInitializeAsync(
            HttpClient client,
            CancellationToken cancellationToken
        )
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
            {
                Content = JsonContent.Create(
                    new
                    {
                        jsonrpc = "2.0",
                        id = 1,
                        method = "initialize",
                        @params = new
                        {
                            protocolVersion = "2025-06-18",
                            capabilities = new { },
                            clientInfo = new { name = "quota-test", version = "1.0.0" },
                        },
                    }
                ),
            };
            request.Headers.Accept.ParseAdd("application/json");
            request.Headers.Accept.ParseAdd("text/event-stream");
            return await client.SendAsync(request, cancellationToken);
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
        public async Task McpScopeExhaustionIs429()
        {
            await using var factory = new ApiFactory();
            using var admin = await factory.CreateAdminClientAsync(Ct);
            using (
                var updated = await admin.PutAsJsonAsync(
                    "api/v1/quotas/Mcp",
                    new { limit = 1, window = "00:01:00" },
                    Ct
                )
            )
            {
                Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
            }

            using var first = await PostInitializeAsync(admin, Ct);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, first.StatusCode);

            using var second = await PostInitializeAsync(admin, Ct);
            Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
            Assert.Equal("application/problem+json", second.Content.Headers.ContentType!.MediaType);
            Assert.True(second.Headers.Contains("Retry-After"));
            var body = await second.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("QUOTA_EXCEEDED", body.GetProperty("code").GetString());
        }

        [Fact]
        public async Task UpdateWithNegativeLimitIs400()
        {
            await using var factory = new ApiFactory();
            using var admin = await factory.CreateAdminClientAsync(Ct);

            using var response = await admin.PutAsJsonAsync(
                "api/v1/quotas/Api",
                new { limit = -5, window = "00:01:00" },
                Ct
            );

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("VALIDATION_FAILED", body.GetProperty("code").GetString());
        }

        [Fact]
        public async Task UpdateWithOver24HourWindowIs400()
        {
            await using var factory = new ApiFactory();
            using var admin = await factory.CreateAdminClientAsync(Ct);

            using var response = await admin.PutAsJsonAsync(
                "api/v1/quotas/Api",
                new { limit = 5, window = "25:00:00" },
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

        [Fact]
        public async Task QuotaCountersResetOnRestartWhileDefinitionsPersist()
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                $"jobscrape-quota-restart-{Guid.NewGuid():N}"
            );
            try
            {
                string token;
                await using (
                    var first = new ApiFactory
                    {
                        StorageRoot = root,
                        DeleteStorageOnDispose = false,
                    }
                )
                {
                    using var admin = await first.CreateAdminClientAsync(Ct);
                    token = admin.DefaultRequestHeaders.Authorization!.Parameter!;
                    using var updated = await admin.PutAsJsonAsync(
                        "api/v1/quotas/Api",
                        new { limit = 1, window = "00:01:00" },
                        Ct
                    );
                    Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
                }

                // Same database file, new host: the in-memory counter is gone
                // while the persisted definition still caps the scope at one.
                await using (
                    var second = new ApiFactory
                    {
                        StorageRoot = root,
                        DeleteStorageOnDispose = false,
                    }
                )
                {
                    using var client = second.CreateClient();
                    client.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                    using var firstRead = await client.GetAsync("api/v1/users", Ct);
                    Assert.Equal(HttpStatusCode.OK, firstRead.StatusCode);

                    using var secondRead = await client.GetAsync("api/v1/users", Ct);
                    Assert.Equal(HttpStatusCode.TooManyRequests, secondRead.StatusCode);
                    Assert.True(secondRead.Headers.Contains("Retry-After"));
                    var limitBody = await secondRead.Content.ReadFromJsonAsync<JsonElement>(Ct);
                    Assert.Equal("QUOTA_EXCEEDED", limitBody.GetProperty("code").GetString());

                    // Definition reads stay exempt even while Api is spent.
                    using var quotasFirst = await client.GetAsync("api/v1/quotas", Ct);
                    using var quotasSecond = await client.GetAsync("api/v1/quotas", Ct);
                    Assert.Equal(HttpStatusCode.OK, quotasFirst.StatusCode);
                    Assert.Equal(HttpStatusCode.OK, quotasSecond.StatusCode);
                }

                // A third host resets the counter again; the limit=1
                // definition survived both restarts.
                await using (
                    var third = new ApiFactory
                    {
                        StorageRoot = root,
                        DeleteStorageOnDispose = false,
                    }
                )
                {
                    using var client = third.CreateClient();
                    client.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                    using var read = await client.GetAsync("api/v1/users", Ct);
                    Assert.Equal(HttpStatusCode.OK, read.StatusCode);

                    using var quotas = await client.GetAsync("api/v1/quotas", Ct);
                    quotas.EnsureSuccessStatusCode();
                    var body = await quotas.Content.ReadFromJsonAsync<JsonElement>(Ct);
                    var api = body.GetProperty("quotas")
                        .EnumerateArray()
                        .Single(quota => quota.GetProperty("scope").GetString() == "Api");
                    Assert.Equal(1, api.GetProperty("limit").GetInt32());
                }
            }
            finally
            {
                for (var attempt = 0; attempt < 5; attempt++)
                {
                    try
                    {
                        if (Directory.Exists(root))
                        {
                            Directory.Delete(root, recursive: true);
                        }

                        break;
                    }
                    catch (IOException) when (attempt < 4)
                    {
                        Thread.Sleep(100);
                    }
                }
            }
        }
    }
}
