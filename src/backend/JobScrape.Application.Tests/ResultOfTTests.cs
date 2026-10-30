using JobScrape.Application.Results;
using Xunit;

namespace JobScrape.Application.Tests
{
    public sealed class ResultOfTTests
    {
        [Fact]
        public void Ok_carries_value()
        {
            var result = Result<string>.Ok("ok");

            Assert.True(result.IsSuccess);
            Assert.False(result.IsFailure);
            Assert.Equal("ok", result.Value);
        }

        [Fact]
        public void Fail_carries_error()
        {
            var error = new Error("PROFILE_NOT_FOUND", "Not found", "Missing.", ErrorKind.NotFound);

            var result = Result<string>.Fail(error);

            Assert.True(result.IsFailure);
            Assert.Same(error, result.Error);
        }

        [Fact]
        public void Value_on_failure_throws()
        {
            var error = new Error("INTERNAL_ERROR", "Title", "Detail", ErrorKind.Unexpected);
            var result = Result<string>.Fail(error);

            Assert.Throws<InvalidOperationException>(() => result.Value);
        }

        [Fact]
        public void Error_on_success_throws()
        {
            var result = Result<string>.Ok("ok");

            Assert.Throws<InvalidOperationException>(() => result.Error);
        }

        [Fact]
        public void Ok_with_null_value_throws()
        {
            Assert.Throws<ArgumentNullException>(() => Result<string>.Ok(null!));
        }

        [Fact]
        public void Fail_with_null_error_throws()
        {
            Assert.Throws<ArgumentNullException>(() => Result<string>.Fail(null!));
        }
    }
}
