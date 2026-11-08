using JobScrape.Domain.Access;
using Xunit;

namespace JobScrape.Domain.Tests.Access
{
    public sealed class PermissionDecisionTests
    {
        [Fact]
        public void MustChangePasswordDeniesEvenWithPermission()
        {
            Assert.False(
                PermissionDecision.IsAllowed(
                    mustChangePassword: true,
                    permissions: ["runs.write"],
                    permission: "runs.write"
                )
            );
        }

        [Fact]
        public void AllowsWhenPermissionIsGrantedAndFirstLoginIsComplete()
        {
            Assert.True(
                PermissionDecision.IsAllowed(
                    mustChangePassword: false,
                    permissions: ["profiles.read", "runs.write"],
                    permission: "runs.write"
                )
            );
        }

        [Fact]
        public void DeniesWhenPermissionIsAbsent()
        {
            Assert.False(
                PermissionDecision.IsAllowed(
                    mustChangePassword: false,
                    permissions: ["profiles.read"],
                    permission: "runs.write"
                )
            );
        }

        [Fact]
        public void DeniesWhenNoPermissionsAreGranted()
        {
            Assert.False(
                PermissionDecision.IsAllowed(
                    mustChangePassword: false,
                    permissions: [],
                    permission: "runs.write"
                )
            );
        }

        [Fact]
        public void ComparisonIsOrdinal()
        {
            Assert.False(
                PermissionDecision.IsAllowed(
                    mustChangePassword: false,
                    permissions: ["Runs.Write"],
                    permission: "runs.write"
                )
            );
        }

        [Fact]
        public void RejectsNullArguments()
        {
            Assert.Throws<ArgumentNullException>(() =>
                PermissionDecision.IsAllowed(false, null!, "runs.write")
            );
            Assert.Throws<ArgumentNullException>(() =>
                PermissionDecision.IsAllowed(false, ["runs.write"], null!)
            );
        }
    }
}
