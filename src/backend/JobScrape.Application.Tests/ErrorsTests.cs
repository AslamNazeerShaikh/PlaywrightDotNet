using JobScrape.Application.Results;
using Xunit;

namespace JobScrape.Application.Tests
{
    public sealed class ErrorsTests
    {
        [Fact]
        public void ValidationFailedCarriesFieldErrors()
        {
            var fields = new Dictionary<string, string[]>
            {
                ["id"] = ["A profile id is required."],
            };

            var error = Errors.ValidationFailed("The request contains invalid fields.", fields);

            Assert.Equal("VALIDATION_FAILED", error.Code);
            Assert.Equal(ErrorKind.Validation, error.Kind);
            Assert.NotNull(error.Metadata);
        }

        [Fact]
        public void RunErrorsCarryTheirCodesKindsAndIds()
        {
            var runId = Guid.NewGuid();

            var missing = Errors.RunNotFound(runId);
            Assert.Equal(("RUN_NOT_FOUND", ErrorKind.NotFound), (missing.Code, missing.Kind));
            Assert.Equal(runId, missing.Metadata!["runId"]);

            var busy = Errors.RunInProgress(runId);
            Assert.Equal(("RUN_IN_PROGRESS", ErrorKind.Conflict), (busy.Code, busy.Kind));
            Assert.Contains(runId.ToString(), busy.Detail, StringComparison.Ordinal);
            Assert.Contains(
                "starting",
                Errors.RunInProgress(null).Detail,
                StringComparison.Ordinal
            );

            var stopped = Errors.RunNotStarted();
            Assert.Equal(("RUN_NOT_STARTED", ErrorKind.Unavailable), (stopped.Code, stopped.Kind));
        }

        [Fact]
        public void ValidationFailedRejectsNullDetail()
        {
            Assert.Throws<ArgumentNullException>(() =>
                Errors.ValidationFailed(null!, new Dictionary<string, string[]>())
            );
        }

        [Fact]
        public void ValidationFailedRejectsNullErrors()
        {
            Assert.Throws<ArgumentNullException>(() => Errors.ValidationFailed("Detail.", null!));
        }

        [Fact]
        public void ProfileNotFoundCarriesId()
        {
            var id = Guid.NewGuid();

            var error = Errors.ProfileNotFound(id);

            Assert.Equal("PROFILE_NOT_FOUND", error.Code);
            Assert.Equal(ErrorKind.NotFound, error.Kind);
            Assert.NotNull(error.Metadata);
        }
    }
}
