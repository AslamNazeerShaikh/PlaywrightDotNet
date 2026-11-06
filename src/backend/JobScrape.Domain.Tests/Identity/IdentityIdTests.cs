using JobScrape.Domain.Identity;
using Xunit;

namespace JobScrape.Domain.Tests.Identity
{
    public sealed class IdentityIdTests
    {
        private static readonly Guid Known = new("6f9619ff-8b86-d011-b42d-00c04fc964ff");

        [Fact]
        public void NewProducesDistinctNonEmptyIds()
        {
            Assert.NotEqual(UserId.New(), UserId.New());
            Assert.NotEqual(RoleId.New(), RoleId.New());
            Assert.NotEqual(Guid.Empty, UserId.New().Value);
            Assert.NotEqual(Guid.Empty, RoleId.New().Value);
        }

        [Fact]
        public void FromPreservesValueAndEquality()
        {
            Assert.Equal(Known, UserId.From(Known).Value);
            Assert.Equal(UserId.From(Known), UserId.From(Known));
            Assert.Equal(RoleId.From(Known), RoleId.From(Known));
        }

        [Fact]
        public void FromRejectsEmptyGuid()
        {
            Assert.Throws<DomainException>(() => UserId.From(Guid.Empty));
            Assert.Throws<DomainException>(() => RoleId.From(Guid.Empty));
        }

        [Fact]
        public void ToStringIsTheGuid()
        {
            Assert.Equal(Known.ToString(), UserId.From(Known).ToString());
            Assert.Equal(Known.ToString(), RoleId.From(Known).ToString());
        }

        [Fact]
        public void PermissionKeyNormalizesCaseAndWhitespace()
        {
            Assert.Equal("profiles.write", PermissionKey.From("  Profiles.Write ").Value);
            Assert.Equal("profiles.write", PermissionKey.From("profiles.write").ToString());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("profiles")]
        [InlineData("has space.write")]
        [InlineData(" Caps.Write! ")]
        public void PermissionKeyRejectsMalformedKeys(string? raw)
        {
            Assert.Throws<DomainException>(() => PermissionKey.From(raw));
        }
    }
}
