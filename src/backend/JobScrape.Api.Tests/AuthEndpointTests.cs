using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.Identity;
using JobScrape.Domain.Identity;
using JobScrape.Infrastructure.Configuration;
using JobScrape.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace JobScrape.Api.Tests
{
    /// <summary>
    /// The auth edge over HTTP: anonymous rejection, bad-credential 401,
    /// lockout 403, the setup-password flow, role-gated admin routes, the
    /// idempotency replay, and the login rate-limit shape. Seeding goes
    /// through scoped repositories; every assertion reads HTTP responses.
    /// </summary>
    public sealed class AuthEndpointTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact]
        public async Task AnonymousCallToProtectedRouteIs401WithCode()
        {
            await using var factory = new ApiFactory();
            using var client = factory.CreateClient();

            using var response = await client.GetAsync("api/v1/search-profiles", Ct);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("UNAUTHENTICATED", body.GetProperty("code").GetString());
            Assert.Equal(
                "application/problem+json",
                response.Content.Headers.ContentType!.MediaType
            );
        }

        [Fact]
        public async Task HealthStaysAnonymous()
        {
            await using var factory = new ApiFactory();
            using var client = factory.CreateClient();

            using var response = await client.GetAsync("api/v1/health", Ct);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task LoginWithBadPasswordIs401InvalidCredentials()
        {
            await using var factory = new ApiFactory();
            using var admin = await AuthTestHelper.CreateAdminClientAsync(factory, Ct);
            using var client = factory.CreateClient();

            using var response = await client.PostAsJsonAsync(
                "api/v1/auth/login",
                new { email = "admin@example.com", password = "Wrong-Password1!!" },
                Ct
            );

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("INVALID_CREDENTIALS", body.GetProperty("code").GetString());
        }

        [Fact]
        public async Task LockedAccountIs403()
        {
            await using var factory = new ApiFactory();
            using var admin = await AuthTestHelper.CreateAdminClientAsync(factory, Ct);
            using var setup = factory.CreateClient();
            var email = $"locked-{Guid.NewGuid():N}@example.com";
            using (
                var created = await admin.PostAsJsonAsync(
                    "api/v1/users",
                    new { email, temporaryPassword = "Temp-Secret1!!" },
                    Ct
                )
            )
            {
                created.EnsureSuccessStatusCode();
            }

            using (
                var setupResponse = await setup.PostAsJsonAsync(
                    "api/v1/auth/setup-password",
                    new
                    {
                        email,
                        currentSecret = "Temp-Secret1!!",
                        newPassword = "Final-Password2@@",
                    },
                    Ct
                )
            )
            {
                setupResponse.EnsureSuccessStatusCode();
            }

            using var attacker = factory.CreateClient();
            for (var attempt = 0; attempt < 5; attempt++)
            {
                using var denied = await attacker.PostAsJsonAsync(
                    "api/v1/auth/login",
                    new { email, password = "Wrong-Password1!!" },
                    Ct
                );
                Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
            }

            using var locked = await attacker.PostAsJsonAsync(
                "api/v1/auth/login",
                new { email, password = "Final-Password2@@" },
                Ct
            );

            Assert.Equal(HttpStatusCode.Forbidden, locked.StatusCode);
            var body = await locked.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("ACCOUNT_LOCKED", body.GetProperty("code").GetString());
        }

        [Fact]
        public async Task ExpiredLockoutNoLongerBlocksLogin()
        {
            await using var factory = new ApiFactory();
            var email = $"expired-lock-{Guid.NewGuid():N}@example.com";
            const string password = "Final-Password2@@";
            using (var scope = factory.Services.CreateScope())
            {
                var provider = scope.ServiceProvider;
                var clock = provider.GetRequiredService<TimeProvider>();
                var user = User.Create(email, PasswordHash.Create(password), clock.GetUtcNow());
                user.CompleteFirstLogin(PasswordHash.Create(password), clock.GetUtcNow());
                user.RecordFailedLogin(
                    clock.GetUtcNow().AddMinutes(-16),
                    5,
                    TimeSpan.FromMinutes(15)
                );
                await provider.GetRequiredService<IUserRepository>().AddAsync(user, Ct);
                await provider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
            }

            using var client = factory.CreateClient();
            using var response = await client.PostAsJsonAsync(
                "api/v1/auth/login",
                new { email, password },
                Ct
            );

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<LoginResponse>(Ct);
            Assert.NotNull(body);
            Assert.False(body!.MustChangePassword);
            Assert.NotEmpty(body.Token);
        }

        [Fact]
        public async Task SetupPasswordFlowFlipsFlagAndReturnsUsableToken()
        {
            await using var factory = new ApiFactory();
            using var admin = await AuthTestHelper.CreateAdminClientAsync(factory, Ct);
            var email = $"first-{Guid.NewGuid():N}@example.com";
            using (
                var created = await admin.PostAsJsonAsync(
                    "api/v1/users",
                    new { email, temporaryPassword = "Temp-Secret1!!" },
                    Ct
                )
            )
            {
                created.EnsureSuccessStatusCode();
            }

            using var pending = factory.CreateClient();
            using (
                var blocked = await pending.PostAsJsonAsync(
                    "api/v1/auth/login",
                    new { email, password = "Temp-Secret1!!" },
                    Ct
                )
            )
            {
                Assert.Equal(HttpStatusCode.Unauthorized, blocked.StatusCode);
                var blockedBody = await blocked.Content.ReadFromJsonAsync<JsonElement>(Ct);
                Assert.Equal("INVALID_CREDENTIALS", blockedBody.GetProperty("code").GetString());
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
                    Ct
                )
            )
            {
                Assert.Equal(HttpStatusCode.OK, setup.StatusCode);
                var login = await setup.Content.ReadFromJsonAsync<LoginResponse>(Ct);
                Assert.NotNull(login);
                Assert.False(login!.MustChangePassword);
                Assert.NotEmpty(login.Token);
            }

            using var signedIn = await AuthTestHelper.LoginAsync(
                factory,
                email,
                "Final-Password2@@",
                Ct
            );
            using var me = await signedIn.GetAsync("api/v1/auth/me", Ct);

            Assert.Equal(HttpStatusCode.OK, me.StatusCode);
            var meBody = await me.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal(email, meBody.GetProperty("email").GetString());
        }

        [Fact]
        public async Task DoubleSetupReturns409PasswordSetupComplete()
        {
            await using var factory = new ApiFactory();
            using var admin = await AuthTestHelper.CreateAdminClientAsync(factory, Ct);
            var email = $"double-setup-{Guid.NewGuid():N}@example.com";
            using (
                var created = await admin.PostAsJsonAsync(
                    "api/v1/users",
                    new { email, temporaryPassword = "Temp-Secret1!!" },
                    Ct
                )
            )
            {
                created.EnsureSuccessStatusCode();
            }

            using var anonymous = factory.CreateClient();
            using (
                var first = await anonymous.PostAsJsonAsync(
                    "api/v1/auth/setup-password",
                    new
                    {
                        email,
                        currentSecret = "Temp-Secret1!!",
                        newPassword = "Final-Password2@@",
                    },
                    Ct
                )
            )
            {
                Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            }

            using var second = await anonymous.PostAsJsonAsync(
                "api/v1/auth/setup-password",
                new
                {
                    email,
                    currentSecret = "Final-Password2@@",
                    newPassword = "Another-Password3##",
                },
                Ct
            );

            Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
            var body = await second.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("PASSWORD_SETUP_COMPLETE", body.GetProperty("code").GetString());
        }

        [Fact]
        public async Task NonAdminCannotReachAdminRoutes()
        {
            await using var factory = new ApiFactory();
            using var admin = await AuthTestHelper.CreateAdminClientAsync(factory, Ct);
            var email = $"op-{Guid.NewGuid():N}@example.com";

            using (
                var created = await admin.PostAsJsonAsync(
                    "api/v1/users",
                    new { email, temporaryPassword = "Temp-Secret1!!" },
                    Ct
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
                    Ct
                )
            )
            {
                setup.EnsureSuccessStatusCode();
            }

            using var viewer = await AuthTestHelper.LoginAsync(
                factory,
                email,
                "Final-Password2@@",
                Ct
            );
            using var denied = await viewer.GetAsync("api/v1/users", Ct);

            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            var body = await denied.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("ACCESS_DENIED", body.GetProperty("code").GetString());
        }

        [Fact]
        public async Task AdminCanReachAdminRoutes()
        {
            await using var factory = new ApiFactory();
            using var admin = await AuthTestHelper.CreateAdminClientAsync(factory, Ct);

            using var users = await admin.GetAsync("api/v1/users", Ct);
            using var roles = await admin.GetAsync("api/v1/roles", Ct);

            Assert.Equal(HttpStatusCode.OK, users.StatusCode);
            Assert.Equal(HttpStatusCode.OK, roles.StatusCode);
        }

        [Fact]
        public async Task IdempotencyReplayReturnsIdenticalBodyWithoutDuplicating()
        {
            await using var factory = new ApiFactory();
            using var admin = await AuthTestHelper.CreateAdminClientAsync(factory, Ct);
            using var replayClient = factory.CreateClient();
            replayClient.DefaultRequestHeaders.Authorization = admin
                .DefaultRequestHeaders
                .Authorization;
            var key = Guid.NewGuid().ToString("N");
            var email = $"idem-{Guid.NewGuid():N}@example.com";
            replayClient.DefaultRequestHeaders.Add("Idempotency-Key", key);

            using var first = await replayClient.PostAsJsonAsync(
                "api/v1/users",
                new { email, temporaryPassword = "Temp-Secret1!!" },
                Ct
            );
            var firstBody = await first.Content.ReadAsStringAsync(Ct);

            using var second = await replayClient.PostAsJsonAsync(
                "api/v1/users",
                new { email, temporaryPassword = "Temp-Secret1!!" },
                Ct
            );
            var secondBody = await second.Content.ReadAsStringAsync(Ct);

            Assert.Equal(first.StatusCode, second.StatusCode);
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
            Assert.Equal(HttpStatusCode.Created, second.StatusCode);
            Assert.Equal(firstBody, secondBody);

            using var list = await admin.GetAsync("api/v1/users", Ct);
            var users = await list.Content.ReadFromJsonAsync<UserListResponse>(Ct);
            Assert.Equal(1, users!.Users.Count(user => user.Email == email));
        }

        [Fact]
        public async Task LoginRateLimitRenders429Problem()
        {
            await using var factory = new ApiFactory();
            using var client = factory.CreateClient();
            HttpResponseMessage? limited = null;
            var firstLimitedAttempt = -1;
            try
            {
                for (var attempt = 0; attempt < 12; attempt++)
                {
                    limited?.Dispose();
                    limited = await client.PostAsJsonAsync(
                        "api/v1/auth/login",
                        new { email = "nobody@example.com", password = "Wrong-Password1!!" },
                        Ct
                    );
                    if (limited.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        firstLimitedAttempt = attempt;
                        break;
                    }
                }

                Assert.Equal(10, firstLimitedAttempt);
                Assert.Equal(HttpStatusCode.TooManyRequests, limited!.StatusCode);
                Assert.True(limited.Headers.TryGetValues("Retry-After", out var retryAfter));
                Assert.Equal("60", Assert.Single(retryAfter));
                var body = await limited.Content.ReadFromJsonAsync<JsonElement>(Ct);
                Assert.Equal("RATE_LIMITED", body.GetProperty("code").GetString());
                Assert.Equal(
                    "application/problem+json",
                    limited.Content.Headers.ContentType!.MediaType
                );
            }
            finally
            {
                limited?.Dispose();
            }
        }

        [Fact]
        public async Task LoginIpCapIsNotBypassedByTrailingSlashOrDistinctEmails()
        {
            await using var factory = new ApiFactory();
            using var client = factory.CreateClient();
            var statuses = new List<HttpStatusCode>();

            for (var attempt = 0; attempt < 12; attempt++)
            {
                using var response = await client.PostAsJsonAsync(
                    "api/v1/auth/login/",
                    new
                    {
                        email = $"distinct-{attempt}-{Guid.NewGuid():N}@example.com",
                        password = "Wrong-Password1!!",
                    },
                    Ct
                );
                statuses.Add(response.StatusCode);

                if (attempt < 10)
                {
                    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
                    var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
                    Assert.Equal("INVALID_CREDENTIALS", body.GetProperty("code").GetString());
                }
            }

            Assert.Equal(HttpStatusCode.TooManyRequests, statuses[10]);
            Assert.Equal(HttpStatusCode.TooManyRequests, statuses[11]);

            using var limited = await client.PostAsJsonAsync(
                "api/v1/auth/login/",
                new
                {
                    email = $"distinct-final-{Guid.NewGuid():N}@example.com",
                    password = "Wrong-Password1!!",
                },
                Ct
            );
            Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
            Assert.True(limited.Headers.TryGetValues("Retry-After", out var retryAfter));
            Assert.Equal("60", Assert.Single(retryAfter));
        }

        [Fact]
        public async Task AnonymousMatrixGrantsOnlyHealthAndAuth()
        {
            await using var factory = new ApiFactory();
            using var client = factory.CreateClient();
            var protectedRoutes = new[]
            {
                "api/v1/search-profiles",
                "api/v1/runs",
                "api/v1/sessions",
                "api/v1/sources",
                "api/v1/users",
                "api/v1/roles",
            };

            foreach (var route in protectedRoutes)
            {
                using var denied = await client.GetAsync(route, Ct);

                Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
                var body = await denied.Content.ReadFromJsonAsync<JsonElement>(Ct);
                Assert.Equal("UNAUTHENTICATED", body.GetProperty("code").GetString());
            }

            using var health = await client.GetAsync("api/v1/health", Ct);
            Assert.Equal(HttpStatusCode.OK, health.StatusCode);

            using var login = await client.PostAsJsonAsync("api/v1/auth/login", new { }, Ct);
            using var setup = await client.PostAsJsonAsync(
                "api/v1/auth/setup-password",
                new { },
                Ct
            );
            Assert.Equal(HttpStatusCode.BadRequest, login.StatusCode);
            var loginBody = await login.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("VALIDATION_FAILED", loginBody.GetProperty("code").GetString());
            Assert.True(loginBody.GetProperty("errors").TryGetProperty("Email", out _));
            Assert.Equal(HttpStatusCode.BadRequest, setup.StatusCode);
            var setupBody = await setup.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("VALIDATION_FAILED", setupBody.GetProperty("code").GetString());
            var setupErrors = setupBody.GetProperty("errors");
            Assert.True(setupErrors.TryGetProperty("Email", out _));
            Assert.True(setupErrors.TryGetProperty("CurrentSecret", out _));
            Assert.True(setupErrors.TryGetProperty("NewPassword", out _));
        }

        [Fact]
        public async Task ChangedPasswordRevokesTheOldToken()
        {
            await using var factory = new ApiFactory();
            using var admin = await AuthTestHelper.CreateAdminClientAsync(factory, Ct);
            var email = $"changed-{Guid.NewGuid():N}@example.com";
            using (
                var created = await admin.PostAsJsonAsync(
                    "api/v1/users",
                    new { email, temporaryPassword = "Temp-Secret1!!" },
                    Ct
                )
            )
            {
                created.EnsureSuccessStatusCode();
            }

            using var setup = factory.CreateClient();
            using (
                var setupResponse = await setup.PostAsJsonAsync(
                    "api/v1/auth/setup-password",
                    new
                    {
                        email,
                        currentSecret = "Temp-Secret1!!",
                        newPassword = "Final-Password2@@",
                    },
                    Ct
                )
            )
            {
                setupResponse.EnsureSuccessStatusCode();
            }

            using var before = await AuthTestHelper.LoginAsync(
                factory,
                email,
                "Final-Password2@@",
                Ct
            );
            using (
                var changed = await before.PostAsJsonAsync(
                    "api/v1/auth/change-password",
                    new
                    {
                        currentPassword = "Final-Password2@@",
                        newPassword = "Newer-Password3##",
                    },
                    Ct
                )
            )
            {
                changed.EnsureSuccessStatusCode();
            }

            using var stale = await before.GetAsync("api/v1/auth/me", Ct);
            Assert.Equal(HttpStatusCode.Unauthorized, stale.StatusCode);
            var staleBody = await stale.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("SESSION_REVOKED", staleBody.GetProperty("code").GetString());

            using var fresh = await AuthTestHelper.LoginAsync(
                factory,
                email,
                "Newer-Password3##",
                Ct
            );
            using var me = await fresh.GetAsync("api/v1/auth/me", Ct);
            Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        }

        [Fact]
        public async Task LockoutRevokesTheOldToken()
        {
            await using var factory = new ApiFactory();
            using var admin = await AuthTestHelper.CreateAdminClientAsync(factory, Ct);
            var email = $"locked-out-{Guid.NewGuid():N}@example.com";
            using (
                var created = await admin.PostAsJsonAsync(
                    "api/v1/users",
                    new { email, temporaryPassword = "Temp-Secret1!!" },
                    Ct
                )
            )
            {
                created.EnsureSuccessStatusCode();
            }

            using var setup = factory.CreateClient();
            using (
                var setupResponse = await setup.PostAsJsonAsync(
                    "api/v1/auth/setup-password",
                    new
                    {
                        email,
                        currentSecret = "Temp-Secret1!!",
                        newPassword = "Final-Password2@@",
                    },
                    Ct
                )
            )
            {
                setupResponse.EnsureSuccessStatusCode();
            }

            using var before = await AuthTestHelper.LoginAsync(
                factory,
                email,
                "Final-Password2@@",
                Ct
            );
            using var attacker = factory.CreateClient();
            for (var attempt = 0; attempt < 5; attempt++)
            {
                using var denied = await attacker.PostAsJsonAsync(
                    "api/v1/auth/login",
                    new { email, password = "Wrong-Password1!!" },
                    Ct
                );
                Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
            }

            using var stale = await before.GetAsync("api/v1/auth/me", Ct);
            Assert.Equal(HttpStatusCode.Unauthorized, stale.StatusCode);
            var staleBody = await stale.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("SESSION_REVOKED", staleBody.GetProperty("code").GetString());
        }

        [Fact]
        public async Task RealSetupTokenWorksWhereTheForgedMustChangePasswordTokenIsDenied()
        {
            await using var factory = new ApiFactory();
            using var admin = await AuthTestHelper.CreateAdminClientAsync(factory, Ct);
            var email = $"attribution-{Guid.NewGuid():N}@example.com";
            using (
                var created = await admin.PostAsJsonAsync(
                    "api/v1/users",
                    new { email, temporaryPassword = "Temp-Secret1!!" },
                    Ct
                )
            )
            {
                created.EnsureSuccessStatusCode();
            }

            // The real issuance path refuses a pending account outright.
            using var pendingLogin = factory.CreateClient();
            using (
                var rejected = await pendingLogin.PostAsJsonAsync(
                    "api/v1/auth/login",
                    new { email, password = "Temp-Secret1!!" },
                    Ct
                )
            )
            {
                Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
            }

            // Mint a must-change token for the pending account while it is
            // pending, so the sub, token version, and signature all match.
            string forged;
            using (var scope = factory.Services.CreateScope())
            {
                var provider = scope.ServiceProvider;
                var pending = await provider
                    .GetRequiredService<IUserRepository>()
                    .GetByEmailAsync(email, Ct);
                Assert.True(pending!.MustChangePassword);
                forged = new JwtTokenService(
                    Options.Create(
                        new JwtOptions
                        {
                            Secret = AuthTestHelper.TestJwtSecret,
                            Issuer = "jobscrape",
                            Audience = "jobscrape-api",
                        }
                    )
                ).CreateToken(
                    pending,
                    [],
                    [PermissionKey.From("profiles.read")],
                    provider.GetRequiredService<TimeProvider>().GetUtcNow()
                );
            }

            using var forgedClient = factory.CreateClient();
            forgedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                forged
            );
            using (var denied = await forgedClient.GetAsync("api/v1/search-profiles", Ct))
            {
                Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
                var deniedBody = await denied.Content.ReadFromJsonAsync<JsonElement>(Ct);
                Assert.Equal("ACCESS_DENIED", deniedBody.GetProperty("code").GetString());
            }

            // The real temp-secret setup flow signs in for the same account.
            using var setupClient = factory.CreateClient();
            using var setup = await setupClient.PostAsJsonAsync(
                "api/v1/auth/setup-password",
                new
                {
                    email,
                    currentSecret = "Temp-Secret1!!",
                    newPassword = "Final-Password2@@",
                },
                Ct
            );
            setup.EnsureSuccessStatusCode();
            var real = await setup.Content.ReadFromJsonAsync<LoginResponse>(Ct);
            Assert.NotNull(real);
            Assert.False(real!.MustChangePassword);

            using var realClient = factory.CreateClient();
            realClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                real.Token
            );
            using var allowed = await realClient.GetAsync("api/v1/search-profiles", Ct);
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        [Fact]
        public async Task MustChangePasswordTokenIsDeniedOutsideAuth()
        {
            await using var factory = new ApiFactory();
            var email = $"pending-{Guid.NewGuid():N}@example.com";
            using (var scope = factory.Services.CreateScope())
            {
                var provider = scope.ServiceProvider;
                var clock = provider.GetRequiredService<TimeProvider>();
                var user = User.CreateSeedAdmin(
                    email,
                    PasswordHash.Create("Temp-Secret1!!"),
                    clock.GetUtcNow()
                );
                await provider.GetRequiredService<IUserRepository>().AddAsync(user, Ct);
                await provider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
            }

            var tokens = new JwtTokenService(
                Options.Create(
                    new JwtOptions
                    {
                        Secret = AuthTestHelper.TestJwtSecret,
                        Issuer = "jobscrape",
                        Audience = "jobscrape-api",
                    }
                )
            );
            string token;
            using (var scope = factory.Services.CreateScope())
            {
                var provider = scope.ServiceProvider;
                var clock = provider.GetRequiredService<TimeProvider>();
                var user = await provider
                    .GetRequiredService<IUserRepository>()
                    .GetByEmailAsync(email, Ct);
                token = tokens.CreateToken(
                    user!,
                    [],
                    [PermissionKey.From("profiles.read")],
                    clock.GetUtcNow()
                );
            }

            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                token
            );

            using var denied = await client.GetAsync("api/v1/search-profiles", Ct);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            var deniedBody = await denied.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("ACCESS_DENIED", deniedBody.GetProperty("code").GetString());

            using var me = await client.GetAsync("api/v1/auth/me", Ct);
            Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        }

        [Fact]
        public async Task AdminWithZeroPermissionsPassesPermissionPolicies()
        {
            await using var factory = new ApiFactory();
            var email = $"stripped-{Guid.NewGuid():N}@example.com";
            const string password = "Final-Password2@@";
            using (var scope = factory.Services.CreateScope())
            {
                var provider = scope.ServiceProvider;
                var roleRepository = provider.GetRequiredService<IRoleRepository>();
                var adminRole = await roleRepository.GetByNameAsync(RoleName.Admin, Ct);
                if (adminRole is null)
                {
                    adminRole = Role.Create(RoleId.New(), RoleName.Admin);
                    await roleRepository.AddAsync(adminRole, Ct);
                }

                foreach (var key in PermissionCatalog.Keys)
                {
                    adminRole.Revoke(key);
                }

                var clock = provider.GetRequiredService<TimeProvider>();
                var user = User.CreateSeedAdmin(
                    email,
                    PasswordHash.Create("Temporary1!!"),
                    clock.GetUtcNow()
                );
                user.AssignRole(adminRole.Id);
                user.CompleteFirstLogin(PasswordHash.Create(password), clock.GetUtcNow());
                await provider.GetRequiredService<IUserRepository>().AddAsync(user, Ct);
                await provider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
            }

            using var client = await AuthTestHelper.LoginAsync(factory, email, password, Ct);

            using var meResponse = await client.GetAsync("api/v1/auth/me", Ct);
            Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
            var me = await meResponse.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Contains(
                "Admin",
                me.GetProperty("roles").EnumerateArray().Select(value => value.GetString())
            );
            Assert.Empty(me.GetProperty("permissions").EnumerateArray());

            using var users = await client.GetAsync("api/v1/users", Ct);
            Assert.Equal(HttpStatusCode.OK, users.StatusCode);

            using var roles = await client.GetAsync("api/v1/roles", Ct);
            Assert.Equal(HttpStatusCode.OK, roles.StatusCode);

            using var database = await client.GetAsync("api/v1/db/tables", Ct);
            Assert.Equal(HttpStatusCode.OK, database.StatusCode);
        }

        [Fact]
        public async Task IdempotencyKeyReusedWithADifferentBodyIs409()
        {
            await using var factory = new ApiFactory();
            using var admin = await AuthTestHelper.CreateAdminClientAsync(factory, Ct);
            using var replayClient = factory.CreateClient();
            replayClient.DefaultRequestHeaders.Authorization = admin
                .DefaultRequestHeaders
                .Authorization;
            var key = Guid.NewGuid().ToString("N");
            var firstEmail = $"idem-first-{Guid.NewGuid():N}@example.com";
            var secondEmail = $"idem-second-{Guid.NewGuid():N}@example.com";
            replayClient.DefaultRequestHeaders.Add("Idempotency-Key", key);

            using var first = await replayClient.PostAsJsonAsync(
                "api/v1/users",
                new { email = firstEmail, temporaryPassword = "Temp-Secret1!!" },
                Ct
            );
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);

            using var second = await replayClient.PostAsJsonAsync(
                "api/v1/users",
                new { email = secondEmail, temporaryPassword = "Temp-Secret1!!" },
                Ct
            );
            Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
            var body = await second.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("IDEMPOTENCY_KEY_REUSED", body.GetProperty("code").GetString());

            using var list = await admin.GetAsync("api/v1/users", Ct);
            var users = await list.Content.ReadFromJsonAsync<UserListResponse>(Ct);
            Assert.DoesNotContain(users!.Users, user => user.Email == secondEmail);
        }

        [Fact]
        public async Task LoginResponsesAreNeverReplayed()
        {
            await using var factory = new ApiFactory();
            using var admin = await AuthTestHelper.CreateAdminClientAsync(factory, Ct);
            var email = $"replay-{Guid.NewGuid():N}@example.com";
            using (
                var created = await admin.PostAsJsonAsync(
                    "api/v1/users",
                    new { email, temporaryPassword = "Temp-Secret1!!" },
                    Ct
                )
            )
            {
                created.EnsureSuccessStatusCode();
            }

            using var setup = factory.CreateClient();
            using (
                var setupResponse = await setup.PostAsJsonAsync(
                    "api/v1/auth/setup-password",
                    new
                    {
                        email,
                        currentSecret = "Temp-Secret1!!",
                        newPassword = "Final-Password2@@",
                    },
                    Ct
                )
            )
            {
                setupResponse.EnsureSuccessStatusCode();
            }

            var key = Guid.NewGuid().ToString("N");
            using var firstClient = factory.CreateClient();
            using var secondClient = factory.CreateClient();
            firstClient.DefaultRequestHeaders.Add("Idempotency-Key", key);
            secondClient.DefaultRequestHeaders.Add("Idempotency-Key", key);

            using var first = await firstClient.PostAsJsonAsync(
                "api/v1/auth/login",
                new { email, password = "Final-Password2@@" },
                Ct
            );
            using var second = await secondClient.PostAsJsonAsync(
                "api/v1/auth/login",
                new { email, password = "Final-Password2@@" },
                Ct
            );
            var firstLogin = await first.Content.ReadFromJsonAsync<LoginResponse>(Ct);
            var secondLogin = await second.Content.ReadFromJsonAsync<LoginResponse>(Ct);

            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            Assert.NotEqual(firstLogin!.Token, secondLogin!.Token);
        }

        [Fact]
        public async Task MePollingDoesNotConsumeTheLoginBudget()
        {
            await using var factory = new ApiFactory();
            using var admin = await AuthTestHelper.CreateAdminClientAsync(factory, Ct);
            var email = $"me-poll-{Guid.NewGuid():N}@example.com";
            using (
                var created = await admin.PostAsJsonAsync(
                    "api/v1/users",
                    new { email, temporaryPassword = "Temp-Secret1!!" },
                    Ct
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
                    Ct
                )
            )
            {
                setup.EnsureSuccessStatusCode();
            }

            using var meClient = await AuthTestHelper.LoginAsync(
                factory,
                email,
                "Final-Password2@@",
                Ct
            );
            for (var i = 0; i < 11; i++)
            {
                using var me = await meClient.GetAsync("api/v1/auth/me", Ct);
                Assert.Equal(HttpStatusCode.OK, me.StatusCode);
            }

            // /me is on the global limiter only. If it shared the login
            // budget the next sign-in would be 429; it must succeed.
            using var loginClient = factory.CreateClient();
            using var login = await loginClient.PostAsJsonAsync(
                "api/v1/auth/login",
                new { email, password = "Final-Password2@@" },
                Ct
            );
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        }

        [Fact]
        public async Task AnonymousMcpRequestIs401WithCode()
        {
            await using var factory = new ApiFactory();
            using var client = factory.CreateClient();
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
                            clientInfo = new { name = "anonymous-mcp-test", version = "1.0.0" },
                        },
                    }
                ),
            };
            request.Headers.Accept.ParseAdd("application/json");
            request.Headers.Accept.ParseAdd("text/event-stream");

            using var response = await client.SendAsync(request, Ct);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("UNAUTHENTICATED", body.GetProperty("code").GetString());
        }
    }
}
