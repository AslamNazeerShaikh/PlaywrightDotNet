using System.Net.Http.Headers;
using System.Net.Http.Json;
using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.Identity;
using JobScrape.Domain.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace JobScrape.Api.Tests
{
    /// <summary>
    /// Seeds identity rows directly (arrange) so every test still exercises
    /// login and the protected endpoints over HTTP (act and assert). The
    /// login token is cached per factory: one sign-in per app instance,
    /// so shared-fixture classes never trip the login rate limit.
    /// </summary>
    internal static class AuthTestHelper
    {
        /// <summary>
        /// The signing secret the test host runs with. Mirrors the
        /// <c>JobScrape:Jwt:Secret</c> override in <see cref="ApiFactory"/>.
        /// </summary>
        internal const string TestJwtSecret = "test-only-secret-that-is-at-least-32-bytes!!";

        private const string SeedPassword = "Seed-Secret1!!";
        private const string FinalPassword = "Final-Password2@@";
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<
            object,
            Task<string>
        > Tokens = new();
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<
            object,
            System.Runtime.CompilerServices.StrongBox<Guid>
        > AdminIds = new();

        /// <summary>
        /// Seeds an active admin with every catalog permission and returns a
        /// client carrying its bearer token.
        /// </summary>
        /// <param name="factory">The app under test.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>An authenticated admin client.</returns>
        internal static async Task<HttpClient> CreateAdminClientAsync(
            this WebApplicationFactory<Program> factory,
            CancellationToken cancellationToken
        )
        {
            var token = await Tokens.GetValue(
                factory,
                _ => SeedAndLoginAsync(factory, cancellationToken)
            );
            var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                token
            );
            return client;
        }

        /// <summary>
        /// Seeds one active admin with every catalog permission and signs in
        /// over HTTP. Runs once per factory; later callers reuse the token.
        /// </summary>
        /// <param name="factory">The app under test.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The bearer token.</returns>
        private static async Task<string> SeedAndLoginAsync(
            WebApplicationFactory<Program> factory,
            CancellationToken cancellationToken
        )
        {
            var email = $"admin-{Guid.NewGuid():N}@example.com";
            using (var scope = factory.Services.CreateScope())
            {
                var provider = scope.ServiceProvider;
                var roles = provider.GetRequiredService<IRoleRepository>();
                var admin = await roles.GetByNameAsync(RoleName.Admin, cancellationToken);
                if (admin is null)
                {
                    admin = Role.Create(RoleId.New(), RoleName.Admin);
                    await roles.AddAsync(admin, cancellationToken);
                }

                foreach (var key in PermissionCatalog.Keys)
                {
                    if (!admin.PermissionKeys.Contains(key))
                    {
                        admin.Grant(key);
                    }
                }

                var clock = provider.GetRequiredService<TimeProvider>();
                var user = User.CreateSeedAdmin(
                    email,
                    PasswordHash.Create(SeedPassword),
                    clock.GetUtcNow()
                );
                user.AssignRole(admin.Id);
                user.CompleteFirstLogin(PasswordHash.Create(FinalPassword), clock.GetUtcNow());
                await provider
                    .GetRequiredService<IUserRepository>()
                    .AddAsync(user, cancellationToken);
                await provider
                    .GetRequiredService<IUnitOfWork>()
                    .SaveChangesAsync(cancellationToken);
                AdminIds.Add(
                    factory,
                    new System.Runtime.CompilerServices.StrongBox<Guid>(user.Id.Value)
                );
            }

            using var client = factory.CreateClient();
            using var response = await client.PostAsJsonAsync(
                "api/v1/auth/login",
                new { email, password = FinalPassword },
                cancellationToken
            );
            response.EnsureSuccessStatusCode();
            var login = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken);
            return login!.Token;
        }

        /// <summary>
        /// Returns the id of the admin user seeded by
        /// <see cref="CreateAdminClientAsync"/> for this factory. Call after
        /// the client exists so the seed has run.
        /// </summary>
        /// <param name="factory">The app under test.</param>
        /// <returns>The seeded admin's user id.</returns>
        internal static Guid GetSeededAdminId(this WebApplicationFactory<Program> factory)
        {
            return AdminIds.TryGetValue(factory, out var id)
                ? id.Value
                : throw new InvalidOperationException(
                    "The admin client has not been created for this factory yet."
                );
        }

        /// <summary>
        /// Logs in over HTTP and returns a client carrying the bearer token.
        /// </summary>
        /// <param name="factory">The app under test.</param>
        /// <param name="email">The account email.</param>
        /// <param name="password">The clear-text password.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>An authenticated client.</returns>
        internal static async Task<HttpClient> LoginAsync(
            WebApplicationFactory<Program> factory,
            string email,
            string password,
            CancellationToken cancellationToken
        )
        {
            var client = factory.CreateClient();
            using var response = await client.PostAsJsonAsync(
                "api/v1/auth/login",
                new { email, password },
                cancellationToken
            );
            response.EnsureSuccessStatusCode();
            var login = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                login!.Token
            );
            return client;
        }
    }
}
