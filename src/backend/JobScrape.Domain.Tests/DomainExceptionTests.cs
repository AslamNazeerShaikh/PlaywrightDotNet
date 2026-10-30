using Xunit;

namespace JobScrape.Domain.Tests
{
    public sealed class DomainExceptionTests
    {
        [Fact]
        public void Preserves_message()
        {
            var exception = new DomainException("broken invariant");

            Assert.Equal("broken invariant", exception.Message);
        }

        [Fact]
        public void Preserves_inner_exception()
        {
            var inner = new InvalidOperationException("root cause");

            var exception = new DomainException("broken invariant", inner);

            Assert.Same(inner, exception.InnerException);
        }
    }
}
