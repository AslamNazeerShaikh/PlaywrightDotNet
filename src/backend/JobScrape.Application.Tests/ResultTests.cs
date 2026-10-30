using JobScrape.Application.Results;
using Xunit;

namespace JobScrape.Application.Tests
{
    public sealed class ResultTests
    {
        [Fact]
        public void OkIsSuccessWithoutError()
        {
            var result = Result.Ok();

            Assert.True(result.IsSuccess);
            Assert.False(result.IsFailure);
        }

        [Fact]
        public void FailCarriesError()
        {
            var error = new ErrorInfo("INTERNAL_ERROR", "Title", "Detail", ErrorKind.Unexpected);

            var result = Result.Fail(error);

            Assert.True(result.IsFailure);
            Assert.False(result.IsSuccess);
            Assert.Same(error, result.Error);
        }

        [Fact]
        public void ErrorOnSuccessThrows()
        {
            var result = Result.Ok();

            Assert.Throws<InvalidOperationException>(() => result.Error);
        }

        [Fact]
        public void FailWithNullErrorThrows()
        {
            Assert.Throws<ArgumentNullException>(() => Result.Fail(null!));
        }

        [Fact]
        public void GenericOkCarriesValue()
        {
            var result = Result.Ok<string>("ok");

            Assert.True(result.IsSuccess);
            Assert.Equal("ok", result.Value);
        }

        [Fact]
        public void GenericFailCarriesError()
        {
            var error = new ErrorInfo("PROFILE_NOT_FOUND", "Not found", "Missing.", ErrorKind.NotFound);

            var result = Result.Fail<string>(error);

            Assert.True(result.IsFailure);
            Assert.Same(error, result.Error);
        }

        [Fact]
        public void GenericOkWithNullValueThrows()
        {
            Assert.Throws<ArgumentNullException>(() => Result.Ok<string>(null!));
        }

        [Fact]
        public void GenericFailWithNullErrorThrows()
        {
            Assert.Throws<ArgumentNullException>(() => Result.Fail<string>(null!));
        }
    }
}
