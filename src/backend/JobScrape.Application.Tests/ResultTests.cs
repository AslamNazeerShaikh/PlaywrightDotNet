using JobScrape.Application.Results;
using Xunit;

namespace JobScrape.Application.Tests
{
    public sealed class ResultTests
    {
        [Fact]
        public void Ok_is_success_without_error()
        {
            var result = Result.Ok();

            Assert.True(result.IsSuccess);
            Assert.False(result.IsFailure);
        }

        [Fact]
        public void Fail_carries_error()
        {
            var error = new Error("INTERNAL_ERROR", "Title", "Detail", ErrorKind.Unexpected);

            var result = Result.Fail(error);

            Assert.True(result.IsFailure);
            Assert.False(result.IsSuccess);
            Assert.Same(error, result.Error);
        }

        [Fact]
        public void Error_on_success_throws()
        {
            var result = Result.Ok();

            Assert.Throws<InvalidOperationException>(() => result.Error);
        }

        [Fact]
        public void Fail_with_null_error_throws()
        {
            Assert.Throws<ArgumentNullException>(() => Result.Fail(null!));
        }
    }
}
