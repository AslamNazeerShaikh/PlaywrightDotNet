using JobScrape.Domain.Identity;
using Xunit;

namespace JobScrape.Domain.Tests.Identity
{
    public sealed class PermissionCatalogTests
    {
        private static readonly string[] ExpectedKeys =
        [
            "profiles.read",
            "profiles.write",
            "runs.read",
            "runs.write",
            "users.read",
            "users.write",
            "roles.read",
            "roles.write",
        ];

        [Fact]
        public void V1KeySetIsStable()
        {
            Assert.Equal(ExpectedKeys, PermissionCatalog.Keys.Select(key => key.Value));
        }

        [Fact]
        public void EveryKeyHasADescription()
        {
            foreach (var key in PermissionCatalog.Keys)
            {
                Assert.False(
                    string.IsNullOrWhiteSpace(PermissionCatalog.Describe(key))
                );
            }
        }

        [Fact]
        public void ContainsRecognizesKnownKeysOnly()
        {
            Assert.True(PermissionCatalog.Contains(PermissionKey.From("runs.write")));
            Assert.False(PermissionCatalog.Contains(PermissionKey.From("unknown.scope")));
        }

        [Fact]
        public void DescribeRejectsUnknownKeys()
        {
            Assert.Throws<DomainException>(
                () => PermissionCatalog.Describe(PermissionKey.From("unknown.scope"))
            );
        }
    }
}
