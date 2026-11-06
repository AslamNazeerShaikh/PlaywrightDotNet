using JobScrape.Domain.Identity;
using Xunit;

namespace JobScrape.Domain.Tests.Identity
{
    public sealed class UserTests
    {
        private const string Password = "Correct-Horse-99!";
        private const string OtherPassword = "Another-Good-42?";
        private static readonly TimeSpan Lockout = TimeSpan.FromMinutes(15);
        private static readonly DateTimeOffset Now =
            new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);

        [Fact]
        public void SeedAdminStartsAwaitingFirstLogin()
        {
            var admin = User.CreateSeedAdmin("Admin@Example.com", PasswordHash.Create(Password), Now);

            Assert.Equal("admin@example.com", admin.Email);
            Assert.True(admin.MustChangePassword);
            Assert.Equal(0, admin.FailedAttempts);
            Assert.Null(admin.LockedUntilUtc);
            Assert.Empty(admin.Roles);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("no-at-sign")]
        [InlineData("a@b")]
        [InlineData("a @b.com")]
        public void SeedAdminRejectsBadEmails(string? email)
        {
            Assert.Throws<DomainException>(
                () => User.CreateSeedAdmin(email, PasswordHash.Create(Password), Now)
            );
        }

        [Fact]
        public void FirstLoginReplacesThePasswordAndClearsTheFlag()
        {
            var admin = User.CreateSeedAdmin("admin@example.com", PasswordHash.Create(Password), Now);

            admin.CompleteFirstLogin(PasswordHash.Create(OtherPassword), Now.AddMinutes(1));

            Assert.False(admin.MustChangePassword);
            Assert.True(admin.PasswordHash.Verify(OtherPassword));
            Assert.False(admin.PasswordHash.Verify(Password));
            admin.Authenticate(OtherPassword, Now.AddMinutes(2), 3, Lockout);
        }

        [Fact]
        public void SecondFirstLoginSetupIsRejected()
        {
            var admin = User.CreateSeedAdmin("admin@example.com", PasswordHash.Create(Password), Now);
            admin.CompleteFirstLogin(PasswordHash.Create(OtherPassword), Now);

            var rejected = Assert.Throws<DomainException>(
                () => admin.CompleteFirstLogin(PasswordHash.Create(Password), Now)
            );

            Assert.False(admin.PasswordHash.Verify(Password));
            Assert.DoesNotContain(Password, rejected.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AuthenticateBeforeFirstLoginDemandsAReset()
        {
            var admin = User.CreateSeedAdmin("admin@example.com", PasswordHash.Create(Password), Now);

            var failure = Assert.Throws<DomainException>(
                () => admin.Authenticate(Password, Now, 3, Lockout)
            );

            Assert.Contains("PASSWORD_RESET_REQUIRED", failure.Message, StringComparison.Ordinal);
            Assert.Equal(0, admin.FailedAttempts);
        }

        [Fact]
        public void WrongPasswordFailsWithoutLockingBelowTheLimit()
        {
            var user = ReadyUser();

            var failure = Assert.Throws<DomainException>(
                () => user.Authenticate("Wrong-Password-1!", Now, 3, Lockout)
            );

            Assert.Contains("INVALID_CREDENTIALS", failure.Message, StringComparison.Ordinal);
            Assert.Equal(1, user.FailedAttempts);
            Assert.False(user.IsLockedOut(Now));
        }

        [Fact]
        public void LockoutTripsAfterMaxAttemptsAndReleasesWithTime()
        {
            var user = ReadyUser();

            Assert.Throws<DomainException>(() => user.Authenticate("Wrong-Password-1!", Now, 3, Lockout));
            Assert.Throws<DomainException>(() => user.Authenticate("Wrong-Password-1!", Now, 3, Lockout));
            Assert.Throws<DomainException>(() => user.Authenticate("Wrong-Password-1!", Now, 3, Lockout));

            Assert.True(user.IsLockedOut(Now));
            var locked = Assert.Throws<DomainException>(
                () => user.Authenticate(Password, Now, 3, Lockout)
            );
            Assert.Contains("ACCOUNT_LOCKED", locked.Message, StringComparison.Ordinal);

            Assert.False(user.IsLockedOut(Now + Lockout + TimeSpan.FromSeconds(1)));
            user.Authenticate(Password, Now + Lockout + TimeSpan.FromSeconds(1), 3, Lockout);
            Assert.Equal(0, user.FailedAttempts);
        }

        [Fact]
        public void SuccessResetsTheFailureCount()
        {
            var user = ReadyUser();

            Assert.Throws<DomainException>(() => user.Authenticate("Wrong-Password-1!", Now, 3, Lockout));
            user.Authenticate(Password, Now, 3, Lockout);

            Assert.Equal(0, user.FailedAttempts);
        }

        [Fact]
        public void TokenVersionBumpsOnSecretsAndLockoutOnly()
        {
            var user = ReadyUser();

            Assert.Equal(1, user.TokenVersion);
            user.RecordFailedLogin(Now, 3, Lockout);

            Assert.Equal(1, user.TokenVersion);
            user.RecordFailedLogin(Now, 3, Lockout);
            user.RecordFailedLogin(Now, 3, Lockout);

            Assert.Equal(2, user.TokenVersion);
            user.ChangePassword(PasswordHash.Create("Newer-Password3##"));

            Assert.Equal(3, user.TokenVersion);
        }

        [Fact]
        public void NonPositiveMaxAttemptsIsRejected()
        {
            var user = ReadyUser();

            Assert.Throws<DomainException>(() => user.RecordFailedLogin(Now, 0, Lockout));
        }

        [Fact]
        public void DuplicateRoleAssignmentIsRejected()
        {
            var user = ReadyUser();
            var role = RoleId.New();
            user.AssignRole(role);

            Assert.Throws<DomainException>(() => user.AssignRole(role));
            Assert.Equal([role], user.Roles);
        }

        [Fact]
        public void RevokeRemovesTheRoleAndIgnoresUnknownOnes()
        {
            var user = ReadyUser();
            var role = RoleId.New();
            user.AssignRole(role);

            user.RevokeRole(RoleId.New());

            Assert.Equal([role], user.Roles);
            user.RevokeRole(role);
            Assert.Empty(user.Roles);
        }

        /// <summary>
        /// Builds a user past first login, ready to authenticate.
        /// </summary>
        /// <returns>The ready user.</returns>
        private static User ReadyUser()
        {
            var user = User.CreateSeedAdmin("user@example.com", PasswordHash.Create(Password), Now);
            user.CompleteFirstLogin(PasswordHash.Create(Password), Now);
            return user;
        }
    }
}
