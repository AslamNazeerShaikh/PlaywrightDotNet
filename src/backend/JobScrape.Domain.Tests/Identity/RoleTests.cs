using JobScrape.Domain.Identity;
using Xunit;

namespace JobScrape.Domain.Tests.Identity
{
    public sealed class RoleTests
    {
        [Fact]
        public void NewRoleStartsWithNoPermissions()
        {
            var role = Role.Create(RoleId.New(), RoleName.Operator);

            Assert.Equal(RoleName.Operator, role.Name);
            Assert.Empty(role.PermissionKeys);
        }

        [Fact]
        public void GrantedPermissionsAccumulateInOrder()
        {
            var role = Role.Create(RoleId.New(), RoleName.Operator);
            var read = PermissionKey.From("runs.read");
            var write = PermissionKey.From("runs.write");

            role.Grant(read);
            role.Grant(write);

            Assert.Equal([read, write], role.PermissionKeys);
        }

        [Fact]
        public void DuplicateGrantIsRejected()
        {
            var role = Role.Create(RoleId.New(), RoleName.Operator);
            role.Grant(PermissionKey.From("runs.read"));

            Assert.Throws<DomainException>(() => role.Grant(PermissionKey.From("runs.read")));
            Assert.Single(role.PermissionKeys);
        }

        [Fact]
        public void RevokeRemovesThePermission()
        {
            var role = Role.Create(RoleId.New(), RoleName.Operator);
            var key = PermissionKey.From("runs.read");
            role.Grant(key);

            role.Revoke(key);

            Assert.Empty(role.PermissionKeys);
        }

        [Fact]
        public void RevokeOfNeverGrantedPermissionIsANoOp()
        {
            var role = Role.Create(RoleId.New(), RoleName.Viewer);

            role.Revoke(PermissionKey.From("runs.write"));

            Assert.Empty(role.PermissionKeys);
        }

        [Fact]
        public void RegrantAfterRevokeSucceeds()
        {
            var role = Role.Create(RoleId.New(), RoleName.Operator);
            var key = PermissionKey.From("runs.read");
            role.Grant(key);
            role.Revoke(key);

            role.Grant(key);

            Assert.Equal([key], role.PermissionKeys);
        }
    }
}
