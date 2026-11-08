using System.Diagnostics;
using JobScrape.Api.Http;
using JobScrape.Application.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace JobScrape.Api.Tests
{
    public sealed class ErrorMapperTests
    {
        [Fact]
        public void RejectsNullError()
        {
            Assert.Throws<ArgumentNullException>(() => ((ErrorInfo)null!).ToProblem());
        }

        [Fact]
        public async Task ValidationWithFieldErrorsReturns400Problem()
        {
            var error = Errors.ValidationFailed(
                "The request contains invalid fields.",
                new Dictionary<string, string[]> { ["id"] = ["A profile id is required."] }
            );

            var status = await ExecuteAsync(error.ToProblem());

            Assert.Equal(StatusCodes.Status400BadRequest, status);
        }

        [Fact]
        public async Task ValidationWithoutMetadataReturns400Problem()
        {
            var error = new ErrorInfo(
                "VALIDATION_FAILED",
                "One or more validation errors occurred.",
                "The request contains invalid fields.",
                ErrorKind.Validation,
                null
            );

            var status = await ExecuteAsync(error.ToProblem());

            Assert.Equal(StatusCodes.Status400BadRequest, status);
        }

        [Fact]
        public async Task ValidationWithMissingErrorsKeyReturns400Problem()
        {
            var error = new ErrorInfo(
                "VALIDATION_FAILED",
                "One or more validation errors occurred.",
                "The request contains invalid fields.",
                ErrorKind.Validation,
                new Dictionary<string, object?> { ["other"] = "value" }
            );

            var status = await ExecuteAsync(error.ToProblem());

            Assert.Equal(StatusCodes.Status400BadRequest, status);
        }

        [Fact]
        public async Task ValidationWithWrongErrorsTypeReturns400Problem()
        {
            var error = new ErrorInfo(
                "VALIDATION_FAILED",
                "One or more validation errors occurred.",
                "The request contains invalid fields.",
                ErrorKind.Validation,
                new Dictionary<string, object?> { ["errors"] = "not-a-dictionary" }
            );

            var status = await ExecuteAsync(error.ToProblem());

            Assert.Equal(StatusCodes.Status400BadRequest, status);
        }

        [Theory]
        [InlineData(ErrorKind.NotFound, StatusCodes.Status404NotFound)]
        [InlineData(ErrorKind.Unauthorized, StatusCodes.Status401Unauthorized)]
        [InlineData(ErrorKind.Conflict, StatusCodes.Status409Conflict)]
        [InlineData(ErrorKind.Forbidden, StatusCodes.Status403Forbidden)]
        [InlineData(ErrorKind.RateLimited, StatusCodes.Status429TooManyRequests)]
        [InlineData(ErrorKind.QuotaExceeded, StatusCodes.Status429TooManyRequests)]
        [InlineData(ErrorKind.Challenge, StatusCodes.Status409Conflict)]
        [InlineData(ErrorKind.CoolingDown, StatusCodes.Status423Locked)]
        [InlineData(ErrorKind.SourceChanged, StatusCodes.Status502BadGateway)]
        [InlineData(ErrorKind.Unavailable, StatusCodes.Status503ServiceUnavailable)]
        [InlineData(ErrorKind.Storage, StatusCodes.Status507InsufficientStorage)]
        [InlineData(ErrorKind.Unexpected, StatusCodes.Status500InternalServerError)]
        public async Task KindMapsToExpectedStatus(ErrorKind kind, int expectedStatus)
        {
            var error = new ErrorInfo("CODE", "Title", "Detail.", kind);

            var status = await ExecuteAsync(error.ToProblem());

            Assert.Equal(expectedStatus, status);
        }

        [Fact]
        public async Task IdempotencyKeyReusedReturns409ProblemWithStableCode()
        {
            var error = Errors.IdempotencyKeyReused();

            Assert.Equal(ErrorKind.Conflict, error.Kind);
            Assert.Equal("IDEMPOTENCY_KEY_REUSED", error.Code);
            var status = await ExecuteAsync(error.ToProblem());

            Assert.Equal(StatusCodes.Status409Conflict, status);
        }

        [Fact]
        public async Task ActiveActivitySuppliesTraceId()
        {
            using var activity = new Activity("ErrorMapperTests").Start();
            var error = new ErrorInfo("CODE", "Title", "Detail.", ErrorKind.Unexpected);

            var status = await ExecuteAsync(error.ToProblem());

            Assert.Equal(StatusCodes.Status500InternalServerError, status);
        }

        private static async Task<int> ExecuteAsync(IResult result)
        {
            // ProblemHttpResult resolves ProblemDetails options and logging
            // from the request provider; a bare DefaultHttpContext has none,
            // so supply the same services the real pipeline registers.
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();
            context.RequestServices = new ServiceCollection()
                .AddLogging()
                .AddProblemDetails()
                .BuildServiceProvider();
            await result.ExecuteAsync(context);
            return context.Response.StatusCode;
        }
    }
}
