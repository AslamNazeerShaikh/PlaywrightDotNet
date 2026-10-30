using JobScrape.Application.Results;
using Xunit;

namespace JobScrape.Application.Tests
{
    public sealed class ResultOfTTests
    {
        [Fact]
        public void OkCarriesValue()
        {
            var result = Result.Ok<string>("ok");

            Assert.True(result.IsSuccess);
            Assert.False(result.IsFailure);
            Assert.Equal("ok", result.Value);
        }

        [Fact]
        public void FailCarriesError()
        {
            var error = new ErrorInfo(
                "PROFILE_NOT_FOUND",
                "Not found",
                "Missing.",
                ErrorKind.NotFound
            );

            var result = Result.Fail<string>(error);

            Assert.True(result.IsFailure);
            Assert.Same(error, result.Error);
        }

        [Fact]
        public void ValueOnFailureThrows()
        {
            var error = new ErrorInfo("INTERNAL_ERROR", "Title", "Detail", ErrorKind.Unexpected);
            var result = Result.Fail<string>(error);

            Assert.Throws<InvalidOperationException>(() => result.Value);
        }

        [Fact]
        public void ErrorOnSuccessThrows()
        {
            var result = Result.Ok<string>("ok");

            Assert.Throws<InvalidOperationException>(() => result.Error);
        }
    }
}
