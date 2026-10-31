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
