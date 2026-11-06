using System.IO;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;
using JobScrape.Api.Http;
using JobScrape.Application.Abstractions;
using JobScrape.Application.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace JobScrape.Api.Tests
{
    public sealed class IdempotencyAndQuotaEdgeTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        [Fact]
        public async Task ParallelSameKeyPostsExecuteOnce()
        {
            await using var factory = new ApiFactory();
            using var admin = await AuthTestHelper.CreateAdminClientAsync(factory, Ct);
            var key = Guid.NewGuid().ToString("N");
            var email = $"parallel-{Guid.NewGuid():N}@example.com";

            using var c1 = factory.CreateClient();
            c1.DefaultRequestHeaders.Authorization = admin.DefaultRequestHeaders.Authorization;
            c1.DefaultRequestHeaders.Add("Idempotency-Key", key);
            using var c2 = factory.CreateClient();
            c2.DefaultRequestHeaders.Authorization = admin.DefaultRequestHeaders.Authorization;
            c2.DefaultRequestHeaders.Add("Idempotency-Key", key);

            var t1 = c1.PostAsJsonAsync(
                "api/v1/users",
                new { email, temporaryPassword = "Temp-Secret1!!" },
                Ct
            );
            var t2 = c2.PostAsJsonAsync(
                "api/v1/users",
                new { email, temporaryPassword = "Temp-Secret1!!" },
                Ct
            );
            await Task.WhenAll(t1, t2);

            Assert.Equal(HttpStatusCode.Created, t1.Result.StatusCode);
            Assert.Equal(HttpStatusCode.Created, t2.Result.StatusCode);

            var body1 = await t1.Result.Content.ReadAsByteArrayAsync(Ct);
            var body2 = await t2.Result.Content.ReadAsByteArrayAsync(Ct);
            Assert.True(body1.SequenceEqual(body2));

            using var list = await admin.GetAsync("api/v1/users", Ct);
            var users = await list.Content.ReadFromJsonAsync<JsonElement>(Ct);
            var count = users
                .GetProperty("users")
                .EnumerateArray()
                .Count(u => u.GetProperty("email").GetString() == email);
            Assert.Equal(1, count);
        }

        [Fact]
        public async Task OversizeIdempotencyKeyReturns400()
        {
            await using var factory = new ApiFactory();
            using var admin = await AuthTestHelper.CreateAdminClientAsync(factory, Ct);
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = admin.DefaultRequestHeaders.Authorization;
            client.DefaultRequestHeaders.Add("Idempotency-Key", new string('a', 129));

            var email = $"oversize-{Guid.NewGuid():N}@example.com";
            using var response = await client.PostAsJsonAsync(
                "api/v1/users",
                new { email, temporaryPassword = "Temp-Secret1!!" },
                Ct
            );

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("VALIDATION_FAILED", problem.GetProperty("code").GetString());

            using var list = await admin.GetAsync("api/v1/users", Ct);
            var users = await list.Content.ReadFromJsonAsync<JsonElement>(Ct);
            var count = users
                .GetProperty("users")
                .EnumerateArray()
                .Count(u => u.GetProperty("email").GetString() == email);
            Assert.Equal(0, count);

            // Anonymous requests never engage idempotency: an oversize key
            // passes through to the auth gate instead of being validated.
            using var anon = factory.CreateClient();
            anon.DefaultRequestHeaders.Add("Idempotency-Key", new string('a', 129));
            using var anonResponse = await anon.PostAsJsonAsync(
                "api/v1/users",
                new
                {
                    email = $"anon-oversize-{Guid.NewGuid():N}@example.com",
                    temporaryPassword = "Temp-Secret1!!",
                },
                Ct
            );
            Assert.Equal(HttpStatusCode.Unauthorized, anonResponse.StatusCode);
        }

        [Fact]
        public async Task QuotaReadsAreExempt()
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

            using var first = await admin.GetAsync("api/v1/quotas", Ct);
            using var second = await admin.GetAsync("api/v1/quotas", Ct);
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        }

        [Fact]
        public async Task QuotaMutatingPathConsumes()
        {
            await using var factory = new ApiFactory();
            using var admin = await factory.CreateAdminClientAsync(Ct);
            using (
                var updated = await admin.PutAsJsonAsync(
                    "api/v1/quotas/Api",
                    new { limit = 3, window = "00:01:00" },
                    Ct
                )
            )
            {
                Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
            }

            // Reads stay exempt and do not consume.
            using var read1 = await admin.GetAsync("api/v1/quotas", Ct);
            using var read2 = await admin.GetAsync("api/v1/quotas", Ct);
            Assert.Equal(HttpStatusCode.OK, read1.StatusCode);
            Assert.Equal(HttpStatusCode.OK, read2.StatusCode);

            // A mutating path consumes a slot.
            using var mutate = await admin.PutAsJsonAsync(
                "api/v1/quotas/Api",
                new { limit = 3, window = "00:01:00" },
                Ct
            );
            Assert.Equal(HttpStatusCode.OK, mutate.StatusCode);

            using var exhausted = await admin.GetAsync("api/v1/users", Ct);
            Assert.Equal(HttpStatusCode.OK, exhausted.StatusCode);

            using var over = await admin.GetAsync("api/v1/users", Ct);
            Assert.Equal(HttpStatusCode.TooManyRequests, over.StatusCode);
        }

        [Fact]
        public async Task ReplayDoesNotBurnQuota()
        {
            await using var factory = new ApiFactory();
            using var admin = await factory.CreateAdminClientAsync(Ct);
            using (
                var updated = await admin.PutAsJsonAsync(
                    "api/v1/quotas/Api",
                    new { limit = 3, window = "00:01:00" },
                    Ct
                )
            )
            {
                Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
            }

            var key = Guid.NewGuid().ToString("N");
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = admin.DefaultRequestHeaders.Authorization;
            client.DefaultRequestHeaders.Add("Idempotency-Key", key);
            var email = $"replayquota-{Guid.NewGuid():N}@example.com";

            using var first = await client.PostAsJsonAsync(
                "api/v1/users",
                new { email, temporaryPassword = "Temp-Secret1!!" },
                Ct
            );
            using var replay = await client.PostAsJsonAsync(
                "api/v1/users",
                new { email, temporaryPassword = "Temp-Secret1!!" },
                Ct
            );
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
            Assert.Equal(HttpStatusCode.Created, replay.StatusCode);

            // The replay must not consume an extra slot: two slots used so
            // far (the quota PUT and the single execution), one remains.
            using var fresh = await admin.GetAsync("api/v1/users", Ct);
            Assert.Equal(HttpStatusCode.OK, fresh.StatusCode);

            using var over = await admin.GetAsync("api/v1/users", Ct);
            Assert.Equal(HttpStatusCode.TooManyRequests, over.StatusCode);
        }

        [Fact]
        public async Task QuotaRefundsOn404()
        {
            await using var factory = new ApiFactory();
            using var admin = await factory.CreateAdminClientAsync(Ct);
            using (
                var updated = await admin.PutAsJsonAsync(
                    "api/v1/quotas/Api",
                    new { limit = 2, window = "00:01:00" },
                    Ct
                )
            )
            {
                Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
            }

            var missing = Guid.NewGuid();
            using var first = await admin.DeleteAsync($"api/v1/users/{missing}/roles/Admin", Ct);
            using var second = await admin.DeleteAsync($"api/v1/users/{missing}/roles/Admin", Ct);
            Assert.Equal(HttpStatusCode.NotFound, first.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
        }

        [Fact]
        public async Task IdempotencyWaitTimeoutReturns503()
        {
            await using var factory = new ApiFactory
            {
                TestServices = services =>
                    services.Configure<IdempotencyOptions>(options =>
                        options.CompletionWaitTimeout = TimeSpan.FromMilliseconds(50)
                    ),
            };
            using var admin = await AuthTestHelper.CreateAdminClientAsync(factory, Ct);
            var token = admin.DefaultRequestHeaders.Authorization!.Parameter!;
            var subject = new JwtSecurityTokenHandler().ReadJwtToken(token).Subject;

            var key = Guid.NewGuid().ToString("N");
            using (var scope = factory.Services.CreateScope())
            {
                var store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
                var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();
                var entry = new IdempotencyEntry(
                    subject,
                    "POST /api/v1/users",
                    key,
                    "hash",
                    0,
                    "application/json",
                    [],
                    clock.GetUtcNow()
                );
                await store.TryReserveAsync(entry, Ct);
            }

            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = admin.DefaultRequestHeaders.Authorization;
            client.DefaultRequestHeaders.Add("Idempotency-Key", key);
            using var response = await client.PostAsJsonAsync(
                "api/v1/users",
                new
                {
                    email = $"timeout-{Guid.NewGuid():N}@example.com",
                    temporaryPassword = "Temp-Secret1!!",
                },
                Ct
            );
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Equal("IDEMPOTENCY_IN_PROGRESS", problem.GetProperty("code").GetString());
        }

        [Fact]
        public async Task SecondContenderFailsSemaphoreWaitReturns503()
        {
            RequestDelegate next = async context =>
            {
                await Task.Delay(TimeSpan.FromSeconds(2), context.RequestAborted);
            };
            var middleware = new IdempotencyMiddleware(
                next,
                Options.Create(
                    new IdempotencyOptions
                    {
                        CompletionWaitTimeout = TimeSpan.FromMilliseconds(50),
                    }
                )
            );
            var store = new FakeIdempotencyStore();
            var clock = TimeProvider.System;

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddProblemDetails();
            var serviceProvider = services.BuildServiceProvider();

            var ctx1 = new DefaultHttpContext();
            ctx1.User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    [new Claim(IdentityClaimTypes.Subject, "sub")],
                    "Bearer"
                )
            );
            ctx1.Request.Method = "POST";
            ctx1.Request.Path = "/api/v1/users";
            ctx1.Request.Headers.Append("Idempotency-Key", "same-key");
            ctx1.Request.Body = new MemoryStream();
            ctx1.Response.Body = new MemoryStream();

            var ctx2 = new DefaultHttpContext();
            ctx2.User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    [new Claim(IdentityClaimTypes.Subject, "sub")],
                    "Bearer"
                )
            );
            ctx2.Request.Method = "POST";
            ctx2.Request.Path = "/api/v1/users";
            ctx2.Request.Headers["Idempotency-Key"] = "same-key";
            ctx2.Request.Body = new MemoryStream();
            ctx2.Response.Body = new MemoryStream();

            ctx1.RequestServices = serviceProvider;
            ctx2.RequestServices = serviceProvider;

            var first = middleware.InvokeAsync(ctx1, store, clock);
            await Task.Delay(TimeSpan.FromMilliseconds(50), Ct);
            var second = middleware.InvokeAsync(ctx2, store, clock);

            var finished = await Task.WhenAny(second, Task.Delay(TimeSpan.FromSeconds(2), Ct));
            Assert.Same(second, finished);

            await first;
            Assert.Equal(StatusCodes.Status503ServiceUnavailable, ctx2.Response.StatusCode);
        }

        private sealed class FakeIdempotencyStore : IIdempotencyStore
        {
            public Task<IdempotencyEntry?> GetAsync(
                string ownerKey,
                string endpoint,
                string key,
                CancellationToken cancellationToken
            ) => Task.FromResult<IdempotencyEntry?>(null);

            public Task SaveAsync(IdempotencyEntry entry, CancellationToken cancellationToken) =>
                Task.CompletedTask;

            public Task<bool> TryReserveAsync(
                IdempotencyEntry entry,
                CancellationToken cancellationToken
            ) => Task.FromResult(true);

            public Task RemoveAsync(
                string ownerKey,
                string endpoint,
                string key,
                CancellationToken cancellationToken
            ) => Task.CompletedTask;
        }
    }
}
