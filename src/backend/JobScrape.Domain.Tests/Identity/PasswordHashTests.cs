using JobScrape.Domain.Identity;
using Xunit;

namespace JobScrape.Domain.Tests.Identity
{
    public sealed class PasswordHashTests
    {
        private const string Good = "Correct-Horse-99!";

        [Fact]
        public void CreatedHashVerifiesTheSamePassword()
        {
            var hash = PasswordHash.Create(Good);

            Assert.True(hash.Verify(Good));
        }

        [Fact]
        public void WrongPasswordDoesNotVerify()
        {
            var hash = PasswordHash.Create(Good);

            Assert.False(hash.Verify("Wrong-Password-1!"));
        }

        [Fact]
        public void NullOrEmptyCandidateNeverVerifies()
        {
            var hash = PasswordHash.Create(Good);

            Assert.False(hash.Verify(null));
            Assert.False(hash.Verify(string.Empty));
        }

        [Fact]
        public void SamePasswordProducesDifferentSalts()
        {
            var first = PasswordHash.Create(Good);
            var second = PasswordHash.Create(Good);

            Assert.NotEqual(first, second);
            Assert.True(first.Verify(Good));
            Assert.True(second.Verify(Good));
        }

        [Fact]
        public void RestoredHashVerifiesLikeTheOriginal()
        {
            var original = PasswordHash.Create(Good);
            var restored = PasswordHash.Restore(original.Salt, original.Hash, original.Iterations);

            Assert.Equal(original, restored);
            Assert.True(restored.Verify(Good));
            Assert.False(restored.Verify("Wrong-Password-1!"));
        }

        [Fact]
        public void RestoreRejectsBadSizes()
        {
            var good = PasswordHash.Create(Good);

            Assert.Throws<DomainException>(() =>
                PasswordHash.Restore(new byte[8], good.Hash, good.Iterations)
            );
            Assert.Throws<DomainException>(() =>
                PasswordHash.Restore(good.Salt, new byte[8], good.Iterations)
            );
            Assert.Throws<DomainException>(() => PasswordHash.Restore(good.Salt, good.Hash, 0));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Short-1!a")]
        [InlineData("all-lowercase-99!")]
        [InlineData("ALL-UPPERCASE-99!")]
        [InlineData("No-Digits-Here!!")]
        [InlineData("NoSpecial1234ab")]
        public void CreateRejectsWeakPasswords(string? weak)
        {
            Assert.Throws<DomainException>(() => PasswordHash.Create(weak));
        }

        [Theory]
        [InlineData("Short-1!a")]
        [InlineData("all-lowercase-99!")]
        public void RejectionReasonNeverEchoesThePassword(string? weak)
        {
            var message = Assert.Throws<DomainException>(() => PasswordHash.Create(weak)).Message;

            Assert.DoesNotContain(weak ?? string.Empty, message, StringComparison.Ordinal);
        }
    }
}
