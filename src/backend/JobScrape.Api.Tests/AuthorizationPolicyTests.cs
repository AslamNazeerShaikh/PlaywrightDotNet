using JobScrape.Api.Http.Auth;
using JobScrape.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace JobScrape.Api.Tests
{
    public sealed class AuthorizationPolicyTests
    {
        [Fact]
        public void PermissionPoliciesMatchTheCatalog()
        {
            var catalog = PermissionCatalog.Keys.Select(key => key.Value).ToList();

            Assert.Equal(catalog, AuthPolicies.PermissionPolicies);
        }

        [Fact]
        public async Task EveryPermissionPolicyIsRegistered()
        {
            await using var factory = new ApiFactory();
            var provider = factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();

            foreach (var policy in AuthPolicies.PermissionPolicies)
            {
                Assert.NotNull(await provider.GetPolicyAsync(policy));
            }

            Assert.NotNull(await provider.GetPolicyAsync(AuthPolicies.RequireAdmin));
        }
    }
}
